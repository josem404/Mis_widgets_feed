using System.Text.Json;
using MisWidgets.Feed.Core;

namespace MisWidgets.Feed.Tests;

public sealed class BridgeProtocolTests
{
    private static readonly Guid RequestId = Guid.Parse("169f4214-8e67-4f08-9ee7-d0706ab3adf4");

    [Fact]
    public void ParseRequest_AcceptsCanonicalPing()
    {
        string json = $$"""
            {
              "version": 1,
              "type": "diagnostics.ping",
              "requestId": "{{RequestId:D}}",
              "payload": {
                "sentAtUtc": "2026-09-18T09:30:00.000Z"
              }
            }
            """;

        BridgeParseResult result = BridgeProtocol.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorResponse);
        Assert.Equal(RequestId, result.Request!.RequestId);
        Assert.Equal(DateTimeOffset.Parse("2026-09-18T09:30:00Z"), result.Request.SentAtUtc);
    }

    [Theory]
    [InlineData(2, "unsupported_version")]
    [InlineData(1, "unsupported_type", "native.execute")]
    public void ParseRequest_RejectsUnsupportedMessages(int version, string expectedCode, string type = "diagnostics.ping")
    {
        string json = $$"""
            {
              "version": {{version}},
              "type": "{{type}}",
              "requestId": "{{RequestId:D}}",
              "payload": {
                "sentAtUtc": "2026-09-18T09:30:00.000Z"
              }
            }
            """;

        BridgeParseResult result = BridgeProtocol.ParseRequest(json);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, ReadErrorCode(result.ErrorResponse!));
    }

    [Fact]
    public void ParseRequest_RejectsUnknownProperties()
    {
        string json = $$"""
            {
              "version": 1,
              "type": "diagnostics.ping",
              "requestId": "{{RequestId:D}}",
              "payload": {
                "sentAtUtc": "2026-09-18T09:30:00.000Z"
              },
              "command": "anything"
            }
            """;

        BridgeParseResult result = BridgeProtocol.ParseRequest(json);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_request", ReadErrorCode(result.ErrorResponse!));
    }

    [Fact]
    public void ParseRequest_RejectsOversizedMessages()
    {
        string json = new('x', FeedIdentifiers.MaximumMessageCharacters + 1);

        BridgeParseResult result = BridgeProtocol.ParseRequest(json);

        Assert.False(result.IsSuccess);
        Assert.Equal("message_too_large", ReadErrorCode(result.ErrorResponse!));
    }

    [Fact]
    public void CreatePong_PreservesCorrelationAndUtcTimestamp()
    {
        string response = BridgeProtocol.CreatePong(
            RequestId,
            new DateTimeOffset(2026, 9, 18, 11, 45, 30, TimeSpan.FromHours(2)));

        using JsonDocument document = JsonDocument.Parse(response);
        JsonElement root = document.RootElement;

        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("diagnostics.pong", root.GetProperty("type").GetString());
        Assert.Equal(RequestId.ToString("D"), root.GetProperty("requestId").GetString());
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal(
            "2026-09-18T09:45:30.0000000Z",
            root.GetProperty("payload").GetProperty("providerUtc").GetString());
    }

    private static string ReadErrorCode(string response)
    {
        using JsonDocument document = JsonDocument.Parse(response);
        return document.RootElement.GetProperty("error").GetProperty("code").GetString()!;
    }
}

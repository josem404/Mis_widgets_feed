using System.Xml.Linq;
using System.Text.Json;
using MisWidgets.Feed.Core;

namespace MisWidgets.Feed.Tests;

public sealed class ManifestContractTests
{
    [Fact]
    public void Manifest_UsesStableProtocolIdentifiersAndPublishedUri()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Package.appxmanifest");
        XDocument document = XDocument.Load(path);

        XElement identity = RequiredElement(document, "Identity");
        XElement appExtension = RequiredElement(document, "AppExtension");
        XElement provider = RequiredElement(document, "FeedProvider");
        XElement definition = RequiredElement(document, "Definition");

        Assert.Equal(FeedIdentifiers.PackageName, RequiredAttribute(identity, "Name"));
        Assert.Equal(
            "com.microsoft.windows.widgets.feeds",
            RequiredAttribute(appExtension, "Name"));
        Assert.Equal(FeedIdentifiers.FeedProviderId, RequiredAttribute(provider, "Id"));
        Assert.Equal(FeedIdentifiers.FeedDefinitionId, RequiredAttribute(definition, "Id"));
        Assert.Equal(
            "https://josem404.github.io/Mis_widgets_feed/",
            RequiredAttribute(definition, "ContentUri"));
    }

    [Fact]
    public void Manifest_UsesOnePermanentClsidForComAndFeedActivation()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Package.appxmanifest");
        XDocument document = XDocument.Load(path);

        string comClassId = RequiredAttribute(RequiredElement(document, "Class"), "Id");
        string activationClassId = RequiredAttribute(RequiredElement(document, "CreateInstance"), "ClassId");

        Assert.True(Guid.TryParse(comClassId, out _));
        Assert.Equal(comClassId, activationClassId, ignoreCase: true);
    }

    [Fact]
    public void Manifest_PublicFolderExistsAndIdentifiesTheSameFeed()
    {
        string manifestPath = Path.Combine(AppContext.BaseDirectory, "Package.appxmanifest");
        XDocument document = XDocument.Load(manifestPath);
        XElement appExtension = RequiredElement(document, "AppExtension");

        string publicFolder = RequiredAttribute(appExtension, "PublicFolder");
        string descriptorPath = Path.Combine(
            AppContext.BaseDirectory,
            publicFolder,
            "feed-provider.json");
        Assert.True(File.Exists(descriptorPath), $"No existe {descriptorPath}.");

        using JsonDocument descriptor = JsonDocument.Parse(File.ReadAllText(descriptorPath));
        Assert.Equal(
            FeedIdentifiers.FeedProviderId,
            descriptor.RootElement.GetProperty("providerId").GetString());
        Assert.Equal(
            FeedIdentifiers.FeedDefinitionId,
            descriptor.RootElement.GetProperty("feedIds")[0].GetString());
    }

    private static XElement RequiredElement(XDocument document, string localName) =>
        document.Descendants().Single(element => element.Name.LocalName == localName);

    private static string RequiredAttribute(XElement element, string localName) =>
        element.Attributes().Single(attribute => attribute.Name.LocalName == localName).Value;
}

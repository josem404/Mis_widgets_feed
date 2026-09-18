namespace MisWidgets.Feed.Core;

/// <summary>
/// Stable identifiers shared by the native provider, protocol and package manifest.
/// Changing any value after deployment creates a different logical feed.
/// </summary>
public static class FeedIdentifiers
{
    public const string PackageName = "MisWidgets.Feed.Provider";
    public const string ApplicationId = "App";
    public const string FeedProviderId = "MisWidgetsFeedProvider";
    public const string FeedDefinitionId = "MisWidgets_Dashboard_Feed";
    public const int BridgeVersion = 1;
    public const int MaximumMessageCharacters = 8 * 1024;
}

using MisWidgets.Feed.Core;

namespace MisWidgets.Feed.Provider;

internal static class ProviderProcess
{
    private static readonly object Gate = new();
    private static ProviderLifetime? _lifetime;

    public static void Attach(ProviderLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        lock (Gate)
        {
            _lifetime = lifetime;
        }
    }

    public static void Detach(ProviderLifetime lifetime)
    {
        lock (Gate)
        {
            if (ReferenceEquals(_lifetime, lifetime))
            {
                _lifetime = null;
            }
        }
    }

    public static void RequestStop()
    {
        lock (Gate)
        {
            _lifetime?.RequestStop();
        }
    }
}

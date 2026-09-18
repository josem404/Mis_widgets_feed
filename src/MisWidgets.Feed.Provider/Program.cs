using Microsoft.Windows.Widgets.Feeds.Providers;
using MisWidgets.Feed.Core;
using MisWidgets.Feed.Provider.Com;
using MisWidgets.Feed.Provider.Diagnostics;

namespace MisWidgets.Feed.Provider;

public static class Program
{
    [MTAThread]
    public static int Main(string[] args)
    {
        // Tanto la activacion COM como --selftest usan proyecciones WinRT. En particular,
        // FeedManager.GetDefault() necesita que CsWinRT haya instalado sus wrappers antes de
        // ejecutar cualquiera de los dos caminos.
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (args.Contains(ProviderConstants.SelfTestArgument, StringComparer.OrdinalIgnoreCase))
        {
            return SelfTest.Run();
        }

        if (!args.Contains(ProviderConstants.ComActivationArgument, StringComparer.OrdinalIgnoreCase))
        {
            return 0;
        }

        uint registrationCookie = 0;
        using var lifetime = new ProviderLifetime();

        try
        {
            ProviderProcess.Attach(lifetime);

            var factory = new FeedProviderFactory<FeedProvider>();
            ClassObject.Register(typeof(FeedProvider).GUID, factory, out registrationCookie);
            LocalLog.Info("Servidor COM del feed registrado.");

            LogExistingFeeds();
            lifetime.Wait();
            LocalLog.Info("El host no conserva feeds habilitados; finalizando el provider.");
            return 0;
        }
        catch (Exception exception)
        {
            LocalLog.Error("Fallo fatal del proceso provider.", exception);
            return 1;
        }
        finally
        {
            ProviderProcess.Detach(lifetime);
            if (registrationCookie != 0)
            {
                int result = ClassObject.Revoke(registrationCookie);
                if (result != 0)
                {
                    LocalLog.Warn($"CoRevokeClassObject devolvio 0x{result:X8}.");
                }
            }
        }
    }

    private static void LogExistingFeeds()
    {
        IReadOnlyList<FeedProviderInfo> providers = FeedManager.GetDefault().GetEnabledFeedProviders();
        foreach (FeedProviderInfo provider in providers)
        {
            string feedIds = string.Join(",", provider.EnabledFeedDefinitionIds ?? []);
            LocalLog.Info($"Provider habilitado al iniciar: {provider.FeedProviderDefinitionId}; feeds={feedIds}.");
        }
    }
}

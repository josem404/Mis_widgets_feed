using System.Text.Json;
using Microsoft.Windows.Widgets.Feeds.Providers;
using MisWidgets.Feed.Core;
using Windows.ApplicationModel;
using Windows.Storage;

namespace MisWidgets.Feed.Provider.Diagnostics;

internal static class SelfTest
{
    public static int Run()
    {
        var lines = new List<string>
        {
            $"Mis Widgets Feed self-test — {DateTimeOffset.Now:O}",
        };

        var problems = new List<string>();
        Check("Identidad del paquete", CheckPackageIdentity, lines, problems);
        Check("LocalState escribible", CheckLocalState, lines, problems);
        Check("PublicFolder empaquetada", CheckPublicFolder, lines, problems);
        Check("Protocolo diagnostics.ping", CheckProtocol, lines, problems);
        Check("FeedManager disponible", CheckFeedManager, lines, problems);

        lines.Add(string.Empty);
        lines.Add(problems.Count == 0
            ? "Sin problemas detectados."
            : $"Problemas detectados: {problems.Count}.");

        string report = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        try
        {
            string path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "selftest.txt");
            File.WriteAllText(path, report);
        }
        catch (Exception exception)
        {
            LocalLog.Error("No se pudo escribir selftest.txt.", exception);
            return 1;
        }

        return problems.Count == 0 ? 0 : 1;
    }

    private static string CheckPackageIdentity()
    {
        string name = Package.Current.Id.Name;
        return string.Equals(name, FeedIdentifiers.PackageName, StringComparison.Ordinal)
            ? name
            : throw new InvalidOperationException($"Identidad inesperada: {name}.");
    }

    private static string CheckLocalState()
    {
        string path = Path.Combine(ApplicationData.Current.LocalFolder.Path, ".selftest-write");
        File.WriteAllText(path, "ok");
        File.Delete(path);
        return ApplicationData.Current.LocalFolder.Path;
    }

    private static string CheckPublicFolder()
    {
        string path = Path.Combine(
            Package.Current.InstalledLocation.Path,
            "Public",
            "feed-provider.json");
        using JsonDocument descriptor = JsonDocument.Parse(File.ReadAllText(path));

        string providerId = descriptor.RootElement.GetProperty("providerId").GetString() ?? string.Empty;
        string feedId = descriptor.RootElement.GetProperty("feedIds")[0].GetString() ?? string.Empty;
        if (!string.Equals(providerId, FeedIdentifiers.FeedProviderId, StringComparison.Ordinal) ||
            !string.Equals(feedId, FeedIdentifiers.FeedDefinitionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("El descriptor publico no coincide con el manifiesto.");
        }

        return path;
    }

    private static string CheckProtocol()
    {
        Guid requestId = Guid.NewGuid();
        string request = JsonSerializer.Serialize(new
        {
            version = FeedIdentifiers.BridgeVersion,
            type = "diagnostics.ping",
            requestId = requestId.ToString("D"),
            payload = new { sentAtUtc = DateTimeOffset.UtcNow.ToString("O") },
        });

        BridgeParseResult parsed = BridgeProtocol.ParseRequest(request);
        if (!parsed.IsSuccess || parsed.Request!.RequestId != requestId)
        {
            throw new InvalidOperationException("El protocolo no preservo requestId.");
        }

        using JsonDocument pong = JsonDocument.Parse(BridgeProtocol.CreatePong(requestId, DateTimeOffset.UtcNow));
        if (!pong.RootElement.GetProperty("ok").GetBoolean())
        {
            throw new InvalidOperationException("diagnostics.pong no indico exito.");
        }

        return $"v{FeedIdentifiers.BridgeVersion}; limite={FeedIdentifiers.MaximumMessageCharacters}";
    }

    private static string CheckFeedManager()
    {
        FeedManager? manager = FeedManager.GetDefault();
        if (manager is null)
        {
            throw new InvalidOperationException("FeedManager.GetDefault devolvio null.");
        }

        // En la implementacion preview, antes de que el usuario habilite el primer feed la
        // proyeccion puede devolver null aunque la firma publica sea FeedProviderInfo[]. Es el
        // equivalente practico de una coleccion vacia, no un fallo de activacion de la API.
        FeedProviderInfo[]? providers = manager.GetEnabledFeedProviders();
        return $"Providers habilitados: {providers?.Length ?? 0}";
    }

    private static void Check(
        string label,
        Func<string> action,
        List<string> lines,
        List<string> problems)
    {
        try
        {
            lines.Add($"[OK] {label}: {action()}");
        }
        catch (Exception exception)
        {
            string problem = $"{label}: {exception.GetType().Name}: {exception.Message}";
            lines.Add($"[ERROR] {problem}");
            problems.Add(problem);
        }
    }
}

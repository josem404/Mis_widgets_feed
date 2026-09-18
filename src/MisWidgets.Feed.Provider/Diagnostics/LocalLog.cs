using Windows.Storage;

namespace MisWidgets.Feed.Provider.Diagnostics;

internal static class LocalLog
{
    private const long MaximumBytes = 1024 * 1024;
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message) => Write("WARN", message, null);
    public static void Error(string message, Exception exception) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Gate)
            {
                string directory = GetLogDirectory();
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "feed.log");
                RotateIfNeeded(path);

                string exceptionText = exception is null
                    ? string.Empty
                    : $" | {exception.GetType().Name}: {exception.Message}";
                string line = $"{DateTimeOffset.UtcNow:O} [{level}] {message}{exceptionText}{Environment.NewLine}";
                File.AppendAllText(path, line);
            }
        }
        catch
        {
            // El diagnóstico nunca debe derribar el servidor COM.
        }
    }

    private static string GetLogDirectory()
    {
        try
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, "logs");
        }
        catch
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MisWidgets.Feed.Provider",
                "logs");
        }
    }

    private static void RotateIfNeeded(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length < MaximumBytes)
        {
            return;
        }

        string backup = path + ".1";
        File.Move(path, backup, overwrite: true);
    }
}

namespace PoeAncientsPriceHelper;

// Single source of truth for local configuration, caches and diagnostics. Local (not Roaming): the
// calibration region is monitor-specific and must not roam between machines. Tests bypass this
// entirely by passing an explicit dir to ConfigStore/IconCache.
internal static class AppPaths
{
    private static readonly Lazy<string> LazyDataDir = new(() =>
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PoeAncientsPriceHelper");
        Directory.CreateDirectory(dir);
        return dir;
    });

    public static string DataDir => LazyDataDir.Value;

    // Records a fatal exception to crash.log and returns the path so the caller can point the user at
    // it. A launch-time crash otherwise leaves no console output because this is a WinExe.
    // Best-effort: returns null if the file couldn't be written; logging must never throw.
    public static string? LogCrash(string context, Exception? ex)
    {
        try
        {
            var path = Path.Combine(DataDir, "crash.log");
            File.AppendAllText(path,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  [{context}] {ex?.ToString() ?? "(no exception object)"}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}");
            return path;
        }
        catch { return null; }
    }
}

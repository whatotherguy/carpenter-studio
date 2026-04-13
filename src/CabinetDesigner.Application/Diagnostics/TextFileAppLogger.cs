using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace CabinetDesigner.Application.Diagnostics;

public sealed class TextFileAppLogger : IAppLogger
{
    private const int RetentionDays = 30;

    private readonly string _logDirectory;
    private readonly bool _mirrorToDebug;
    private readonly object _fileLock = new();

    public TextFileAppLogger()
        : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CarpenterStudio",
                "logs"),
            mirrorToDebug:
#if DEBUG
            true
#else
            false
#endif
        )
    {
    }

    internal TextFileAppLogger(string logDirectory, bool mirrorToDebug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);

        _logDirectory = logDirectory;
        _mirrorToDebug = mirrorToDebug;

        TryInitialize();
    }

    public void Log(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            Directory.CreateDirectory(_logDirectory);

            var filePath = Path.Combine(_logDirectory, $"app-{entry.Timestamp.UtcDateTime:yyyyMMdd}.log");
            var line = Format(entry);

            lock (_fileLock)
            {
                File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
            }

            if (_mirrorToDebug)
            {
                Debug.WriteLine(line);
            }
        }
        catch
        {
            // Logging must never alter control flow.
        }
    }

    private void TryInitialize()
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            PruneOldLogs();
        }
        catch
        {
            // Best-effort only. Logging failures must not block startup.
        }
    }

    private void PruneOldLogs()
    {
        foreach (var filePath in Directory.EnumerateFiles(_logDirectory, "app-*.log"))
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var datePart = fileName["app-".Length..];

            if (!DateTime.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            if (date < DateTime.UtcNow.Date.AddDays(-RetentionDays))
            {
                File.Delete(filePath);
            }
        }
    }

    private static string Format(LogEntry entry)
    {
        var builder = new StringBuilder();
        builder.Append('[').Append(entry.Timestamp.ToString("O", CultureInfo.InvariantCulture)).Append("] ");
        builder.Append('[').Append(entry.Level).Append("] ");
        builder.Append('[').Append(entry.Category).Append("] ");
        builder.Append(Sanitize(entry.Message));

        if (entry.Properties is not null && entry.Properties.Count > 0)
        {
            builder.Append(" {");
            var orderedProperties = entry.Properties.OrderBy(property => property.Key, StringComparer.Ordinal);
            builder.Append(string.Join(", ", orderedProperties.Select(property => $"{property.Key}={Sanitize(property.Value)}")));
            builder.Append('}');
        }

        if (!string.IsNullOrWhiteSpace(entry.CommandId))
        {
            builder.Append(" commandId=").Append(entry.CommandId);
        }

        if (!string.IsNullOrWhiteSpace(entry.StageNumber))
        {
            builder.Append(" stage=").Append(entry.StageNumber);
        }

        if (entry.Exception is not null)
        {
            builder.Append(" exception=").Append(Sanitize(entry.Exception.ToString()));
        }

        return builder.ToString();
    }

    private static string Sanitize(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}

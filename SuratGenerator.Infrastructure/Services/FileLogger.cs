using System.Text;
using System.Text.RegularExpressions;
using SuratGenerator.Core.Services;

namespace SuratGenerator.Infrastructure.Services;

public sealed class FileLogger : IAppLogger, IDisposable
{
    private const long MaxFileBytes = 2 * 1024 * 1024;
    private static readonly Regex SensitivePattern = new(
        "(?i)(client_secret|client-secret|access_token|refresh_token|id_token|authorization|password|token|code)(?<sep>[\"'=:\\s]+)(?<value>[^\\s,;\"'}&]+)",
        RegexOptions.Compiled);

    private readonly object _gate = new();
    private readonly string _path;
    private bool _disposed;

    public FileLogger(string? path = null)
    {
        _path = string.IsNullOrWhiteSpace(path) ? AppPaths.LogFilePath : path;
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            RotateIfNeeded();
        }
        catch
        {
        }
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Warning(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    public void Dispose()
    {
        _disposed = true;
    }

    private void Write(string level, string message, Exception? exception)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var builder = new StringBuilder();
            builder.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            builder.Append(" [");
            builder.Append(level);
            builder.Append("] ");
            builder.AppendLine(Sanitize(message ?? string.Empty));

            if (exception is not null)
            {
                builder.AppendLine(Sanitize(exception.ToString()));
            }

            lock (_gate)
            {
                File.AppendAllText(_path, builder.ToString(), Encoding.UTF8);
                RotateIfNeeded();
            }
        }
        catch
        {
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_path) || new FileInfo(_path).Length <= MaxFileBytes)
        {
            return;
        }

        for (var index = 2; index >= 1; index--)
        {
            var source = index == 1 ? _path : _path + "." + (index - 1);
            var destination = _path + "." + index;
            if (!File.Exists(source))
            {
                continue;
            }

            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            File.Move(source, destination);
        }
    }

    internal static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return SensitivePattern.Replace(value, match =>
            match.Groups["sep"].Value + "[REDACTED]");
    }
}

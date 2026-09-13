using System.IO;
using Microsoft.Extensions.Logging;

namespace WooCommerceProductManager.Helpers;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly object _sync = new();

    public FileLoggerProvider(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _logDirectory, _sync);

    public void Dispose()
    {
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _logDirectory;
        private readonly object _sync;

        public FileLogger(string categoryName, string logDirectory, object sync)
        {
            _categoryName = categoryName;
            _logDirectory = logDirectory;
            _sync = sync;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = SensitiveDataRedactor.Redact(formatter(state, exception));
            var exceptionText = exception is null
                ? string.Empty
                : Environment.NewLine + SensitiveDataRedactor.Redact(exception.ToString());

            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{logLevel}] {_categoryName}: {message}{exceptionText}";
            var filePath = Path.Combine(_logDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");

            lock (_sync)
            {
                File.AppendAllText(filePath, line + Environment.NewLine);
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}

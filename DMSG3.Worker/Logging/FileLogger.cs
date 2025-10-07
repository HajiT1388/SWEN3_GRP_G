namespace DMSG3.Worker.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    private readonly LogLevel _minLevel;
    private bool _disposed;

    public FileLoggerProvider(string directory, string filenameBase, LogLevel minLevel = LogLevel.Information)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{filenameBase}.txt");
        var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(fs) { AutoFlush = true };
        _minLevel = minLevel;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _writer, _lock, _minLevel);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _writer.Flush(); _writer.Dispose(); } catch { }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly StreamWriter _writer;
        private readonly object _lock;
        private readonly LogLevel _minLevel;

        public FileLogger(string category, StreamWriter writer, object theLock, LogLevel minLevel)
        {
            _category = category;
            _writer = writer;
            _lock = theLock;
            _minLevel = minLevel;
        }

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var msg = formatter(state, exception);
            var line = $"[{ts}] {logLevel,-11} {eventId.Id,4} {_category} :: {msg}";

            lock (_lock)
            {
                _writer.WriteLine(line);
                if (exception != null)
                {
                    _writer.WriteLine(exception.ToString());
                }
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
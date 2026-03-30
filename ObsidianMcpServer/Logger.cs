using Microsoft.Extensions.Logging;

namespace ObsidianMcpServer
{
    internal class Logger : ILogger
    {
        private readonly string _logPath;
        private readonly string _logFileName;

        public Logger()
        {
            _logFileName = AppDomain.CurrentDomain.FriendlyName + ".log";
            _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log", _logFileName);
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            throw new NotImplementedException();
        }

        public void Log(string message)
        {
            using var sw = new StreamWriter(File.Open(_logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            sw.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            throw new NotImplementedException();
        }
    }
}

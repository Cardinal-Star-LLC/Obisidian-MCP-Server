using Microsoft.Extensions.Logging;

namespace ObsidianMcpServer
{
    internal interface IMcpLogger : ILogger
    {
        public void Log(string message);
    }
}

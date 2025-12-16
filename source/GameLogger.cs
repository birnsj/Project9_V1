using System;

namespace Project9
{
    /// <summary>
    /// Game logger implementation that writes to both console and LogOverlay
    /// </summary>
    public class GameLogger : ILogger
    {
        private readonly LogOverlay? _logOverlay;
        
        public GameLogger(LogOverlay? logOverlay = null)
        {
            _logOverlay = logOverlay;
        }
        
        public void LogDebug(string message)
        {
            WriteLog(message, LogLevel.Debug);
        }
        
        public void LogInfo(string message)
        {
            WriteLog(message, LogLevel.Info);
        }
        
        public void LogWarning(string message)
        {
            WriteLog(message, LogLevel.Warning);
        }
        
        public void LogError(string message)
        {
            WriteLog(message, LogLevel.Error);
        }
        
        private void WriteLog(string message, LogLevel level)
        {
            // Write to log overlay (which also writes to console)
            LogOverlay.Log(message, level);
        }
    }
}


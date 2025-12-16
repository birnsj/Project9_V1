namespace Project9
{
    /// <summary>
    /// Interface for logging functionality
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Log a debug message
        /// </summary>
        void LogDebug(string message);
        
        /// <summary>
        /// Log an info message
        /// </summary>
        void LogInfo(string message);
        
        /// <summary>
        /// Log a warning message
        /// </summary>
        void LogWarning(string message);
        
        /// <summary>
        /// Log an error message
        /// </summary>
        void LogError(string message);
    }
}




using System.Diagnostics;

namespace MauiWifiManager.Helpers
{
    /// <summary>
    /// Provides logging functionality for Wi-Fi manager operations.
    /// </summary>
    internal static class WifiLogger
    {
        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void LogInfo(string message)
        {
            Debug.WriteLine($"[WifiManager:INFO] {message}");
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void LogWarning(string message)
        {
            Debug.WriteLine($"[WifiManager:WARNING] {message}");
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void LogError(string message)
        {
            Debug.WriteLine($"[WifiManager:ERROR] {message}");
        }

        /// <summary>
        /// Logs an error with exception details.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The exception to log.</param>
        public static void LogError(string message, Exception exception)
        {
            Debug.WriteLine($"[WifiManager:ERROR] {message}");
            Debug.WriteLine($"[WifiManager:ERROR] Exception: {exception.Message}");
            Debug.WriteLine($"[WifiManager:ERROR] StackTrace: {exception.StackTrace}");
        }

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        [Conditional("DEBUG")]
        public static void LogDebug(string message)
        {
            Debug.WriteLine($"[WifiManager:DEBUG] {message}");
        }
    }
}

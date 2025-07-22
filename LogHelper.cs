/*******************************************************************************
 * LogHelper.cs
 * 
 * Description:
 *     Utility class that provides centralized logging functionality for the
 *     HwMonitor application. Handles writing diagnostic information to both
 *     console output and a persistent log file on disk.
 * 
 * Key Features:
 *     - Simple API for consistent logging across the application
 *     - File-based logging with timestamps
 *     - Error logging with full exception details
 *     - Fail-safe design that prevents logging errors from crashing the app
 * 
 * Dependencies:
 *     - System.IO for file operations
 * 
 * Notes:
 *     The logger automatically initializes a new log file on application startup
 *     and provides static methods for logging regular messages and errors
 *     throughout the application.
 *******************************************************************************/

using System;
using System.IO;

namespace HardwareMonitorApp
{
    public static class LogHelper
    {
        private static readonly string LogFilePath = Path.Combine(
            AppContext.BaseDirectory, 
            "hwmon_log.txt");
            
        static LogHelper()
        {
            try
            {
                // Start with a clean log file
                File.WriteAllText(LogFilePath, $"=== Hardware Monitor Log {DateTime.Now} ===\r\n");
                Log("LogHelper initialized");
            }
            catch (Exception ex)
            {
                // Can't do much if we can't write to the log
                Console.WriteLine($"Failed to initialize log file: {ex.Message}");
            }
        }
            
        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\r\n");
                Console.WriteLine(message);
            }
            catch
            {
                // Silently fail if we can't write to the log
            }
        }
            
        public static void LogError(string context, Exception ex)
        {
            try
            {
                string message = $"ERROR in {context}: {ex.Message}\r\n{ex.StackTrace}";
                Log(message);
            }
            catch
            {
                // Last-resort error handling
            }
        }
    }
}

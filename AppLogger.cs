using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DshAntigravityLauncher
{
    public static class AppLogger
    {
        private static readonly object _lock = new object();
        private static string? _logFilePath;

        public static string LogFilePath
        {
            get
            {
                if (_logFilePath == null)
                {
                    _logFilePath = Path.Combine(AppContext.BaseDirectory, "lastsession.log");
                }
                return _logFilePath;
            }
        }

        public static void Initialize()
        {
            lock (_lock)
            {
                try
                {
                    // Create or overwrite lastsession.log for the current startup session
                    using var fs = new FileStream(LogFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    using var writer = new StreamWriter(fs, Encoding.UTF8);
                    writer.WriteLine("================================================================================");
                    writer.WriteLine("DSH Antigravity Launcher - Last Session Log");
                    writer.WriteLine($"Started At: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"OS Version: {Environment.OSVersion}");
                    writer.WriteLine($"App Directory: {AppContext.BaseDirectory}");
                    writer.WriteLine($"Log File: {LogFilePath}");
                    writer.WriteLine("================================================================================");
                    writer.WriteLine();
                    writer.Flush();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AppLogger] Failed to initialize log file: {ex.Message}");
                }
            }
        }

        public static void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formattedLine = $"[{timestamp}] {message}";

            Debug.WriteLine(formattedLine);

            lock (_lock)
            {
                try
                {
                    using var fs = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using var writer = new StreamWriter(fs, Encoding.UTF8);
                    writer.WriteLine(formattedLine);
                    writer.Flush();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AppLogger] Failed to write log: {ex.Message}");
                }
            }
        }

        public static void OpenLogFile()
        {
            try
            {
                if (!File.Exists(LogFilePath))
                {
                    Initialize();
                }

                var psi = new ProcessStartInfo
                {
                    FileName = LogFilePath,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLogger] Failed to open log file: {ex.Message}");
            }
        }
    }
}

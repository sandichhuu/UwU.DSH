using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DshAntigravityLauncher
{
    public class AppSettings
    {
        public List<string> StartupCommands { get; set; } = new List<string>();
        public List<string> ServiceCommands { get; set; } = new List<string>();
    }

    public static class SettingsManager
    {
        private static string? _settingsFilePath;

        public static string SettingsFilePath
        {
            get
            {
                if (_settingsFilePath == null)
                {
                    _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "settings.ini");
                }
                return _settingsFilePath;
            }
        }

        public static readonly string DefaultSettingsContent =
            "; ===================================================================" + Environment.NewLine +
            "; DSH Antigravity Launcher - Settings" + Environment.NewLine +
            "; File này tự động được tạo khi mở ứng dụng lần đầu." + Environment.NewLine +
            "; Bạn có thể chỉnh sửa các dòng lệnh bên dưới để áp dụng cho lần mở sau." + Environment.NewLine +
            "; Các dòng bắt đầu bằng dấu ; hoặc # là chú thích (comment/bỏ qua)." + Environment.NewLine +
            "; ===================================================================" + Environment.NewLine +
            Environment.NewLine +
            "[Startup]" + Environment.NewLine +
            "; Các lệnh chạy tuần tự khi khởi động (chờ hoàn thành trước khi mở dịch vụ)" + Environment.NewLine +
            "command1 = npm config set allow-scripts=better-sqlite3 --location=user" + Environment.NewLine +
            "command2 = npm install -g antigravity-claude-proxy@latest" + Environment.NewLine +
            "command3 = npm uninstall -g codex-claude-proxy && npm install -g git+https://github.com/sandichhuu/codex-claude-proxy.git" + Environment.NewLine +
            Environment.NewLine +
            "[Services]" + Environment.NewLine +
            "; Các lệnh khởi chạy dịch vụ ngầm (chạy đồng thời ở chế độ nền trong suốt phiên làm việc)" + Environment.NewLine +
            "service1 = call antigravity-claude-proxy start" + Environment.NewLine +
            "service2 = call codex-claude-proxy start" + Environment.NewLine +
            "service3 = call npx -y @deepseek-ai/dsh web --no-open" + Environment.NewLine;

        public static void EnsureCreated()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    File.WriteAllText(SettingsFilePath, DefaultSettingsContent, Encoding.UTF8);
                    AppLogger.Log($"[Settings] Created default settings.ini at: {SettingsFilePath}");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log($"[Settings] Error creating settings.ini: {ex.Message}");
            }
        }

        public static AppSettings LoadSettings()
        {
            EnsureCreated();

            var settings = new AppSettings();

            if (!File.Exists(SettingsFilePath))
            {
                AppLogger.Log("[Settings] settings.ini not found, using default startup and service commands.");
                return GetDefaultSettings();
            }

            try
            {
                string[] lines = File.ReadAllLines(SettingsFilePath, Encoding.UTF8);
                string currentSection = "Startup";

                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();

                    // Skip empty lines or comments
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("//"))
                    {
                        continue;
                    }

                    // Section header [SectionName]
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    // Parse command line
                    string command = line;
                    int eqIndex = line.IndexOf('=');
                    if (eqIndex > 0)
                    {
                        string keyPart = line.Substring(0, eqIndex).Trim();
                        // If the key has no spaces, it is a key = value line like: command1 = npm ...
                        if (!keyPart.Contains(' ') && !keyPart.Contains('\t'))
                        {
                            command = line.Substring(eqIndex + 1).Trim();
                        }
                    }

                    // Remove wrapping quotes if present
                    if (command.StartsWith("\"") && command.EndsWith("\"") && command.Length >= 2)
                    {
                        command = command.Substring(1, command.Length - 2).Trim();
                    }

                    if (string.IsNullOrWhiteSpace(command))
                    {
                        continue;
                    }

                    if (currentSection.Equals("Services", StringComparison.OrdinalIgnoreCase) ||
                        currentSection.Equals("Background", StringComparison.OrdinalIgnoreCase) ||
                        currentSection.Equals("BackgroundServices", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.ServiceCommands.Add(command);
                    }
                    else
                    {
                        settings.StartupCommands.Add(command);
                    }
                }

                AppLogger.Log($"[Settings] Successfully loaded {settings.StartupCommands.Count} startup command(s) and {settings.ServiceCommands.Count} service command(s) from {SettingsFilePath}");
            }
            catch (Exception ex)
            {
                AppLogger.Log($"[Settings] Failed to parse settings.ini: {ex.Message}");
                return GetDefaultSettings();
            }

            return settings;
        }

        public static AppSettings GetDefaultSettings()
        {
            var settings = new AppSettings();
            settings.StartupCommands.Add("npm config set allow-scripts=better-sqlite3 --location=user");
            settings.StartupCommands.Add("npm install -g antigravity-claude-proxy@latest");
            settings.StartupCommands.Add("npm uninstall -g codex-claude-proxy && npm install -g git+https://github.com/sandichhuu/codex-claude-proxy.git");

            settings.ServiceCommands.Add("call antigravity-claude-proxy start");
            settings.ServiceCommands.Add("call codex-claude-proxy start");
            settings.ServiceCommands.Add("call npx -y @deepseek-ai/dsh web --no-open");
            return settings;
        }

        public static void OpenSettingsFile()
        {
            try
            {
                EnsureCreated();

                var psi = new ProcessStartInfo
                {
                    FileName = SettingsFilePath,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                AppLogger.Log($"[Settings] Failed to open settings.ini: {ex.Message}");
            }
        }
    }
}

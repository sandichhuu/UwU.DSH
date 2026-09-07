using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DshAntigravityLauncher
{
    public class AppSettings
    {
        public List<string> StartupCommands { get; set; } = new List<string>();
        public List<string> ServiceCommands { get; set; } = new List<string>();
    }

    public static class SettingsManager
    {
        public const string DshmarketPluginCommand = "npx -y @deepseek-ai/dsh plugin --profile web add dshmarket";

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
            "; Hỗ trợ thêm tự động command4, command5, ... và service4, service5, ..." + Environment.NewLine +
            "; ===================================================================" + Environment.NewLine +
            Environment.NewLine +
            "[Startup]" + Environment.NewLine +
            "; Các lệnh chạy tuần tự khi khởi động (chờ hoàn thành trước khi mở dịch vụ)" + Environment.NewLine +
            "command1 = npm config set allow-scripts=better-sqlite3 --location=user" + Environment.NewLine +
            "command2 = npm install -g uwu-x-proxy@latest" + Environment.NewLine +
            $"command3 = {DshmarketPluginCommand}" + Environment.NewLine +
            Environment.NewLine +
            "[Services]" + Environment.NewLine +
            "; Các lệnh khởi chạy dịch vụ ngầm (chạy đồng thời ở chế độ nền trong suốt phiên làm việc)" + Environment.NewLine +
            "service1 = call npx uwu-x-proxy" + Environment.NewLine +
            "service2 = call npx -y @deepseek-ai/dsh web --no-open" + Environment.NewLine;

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

        public static string SanitizeCommand(string? command)
        {
            if (string.IsNullOrWhiteSpace(command)) return string.Empty;

            string trimmed = command.Trim();

            // Remove wrapping quotes if present
            if (trimmed.StartsWith("\"") && trimmed.EndsWith("\"") && trimmed.Length >= 2)
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
            }

            // Fix accidental space after @ in npm scoped package names, e.g.:
            // "@ deepseek-ai/dsh" -> "@deepseek-ai/dsh"
            string sanitized = Regex.Replace(trimmed, @"@\s+([a-zA-Z0-9_\-\.]+/[a-zA-Z0-9_\-\.]+)", "@$1");

            return sanitized;
        }

        public static void EnsureDshmarketPluginConfigured(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;

                string text = File.ReadAllText(filePath, Encoding.UTF8);

                // If already present anywhere in settings, do nothing
                if (text.Contains("plugin --profile web add dshmarket", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var lines = File.ReadAllLines(filePath, Encoding.UTF8).ToList();
                int startupSectionIndex = -1;
                int nextSectionIndex = -1;
                int lastStartupCommandIndex = -1;
                int maxCommandNumber = 0;

                for (int i = 0; i < lines.Count; i++)
                {
                    string trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        string sectionName = trimmed.Substring(1, trimmed.Length - 2).Trim();
                        if (sectionName.Equals("Startup", StringComparison.OrdinalIgnoreCase))
                        {
                            startupSectionIndex = i;
                        }
                        else if (startupSectionIndex >= 0 && nextSectionIndex < 0)
                        {
                            nextSectionIndex = i;
                        }
                    }
                    else if (startupSectionIndex >= 0 && nextSectionIndex < 0)
                    {
                        if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith(";") && !trimmed.StartsWith("#"))
                        {
                            lastStartupCommandIndex = i;
                            var match = Regex.Match(trimmed, @"^command\s*(\d+)", RegexOptions.IgnoreCase);
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                            {
                                if (num > maxCommandNumber) maxCommandNumber = num;
                            }
                        }
                    }
                }

                int newCommandNumber = maxCommandNumber > 0 ? maxCommandNumber + 1 : 4;
                string newCommandLine = $"command{newCommandNumber} = {DshmarketPluginCommand}";

                int insertIndex = lastStartupCommandIndex >= 0 ? lastStartupCommandIndex + 1 :
                                  (nextSectionIndex >= 0 ? nextSectionIndex : lines.Count);

                lines.Insert(insertIndex, newCommandLine);
                File.WriteAllLines(filePath, lines, Encoding.UTF8);

                AppLogger.Log($"[Settings] Automatically added dshmarket plugin command as command{newCommandNumber} to {filePath}");
            }
            catch (Exception ex)
            {
                AppLogger.Log($"[Settings] Warning: Could not auto-add dshmarket command to settings.ini: {ex.Message}");
            }
        }

        private class ParsedCommandItem
        {
            public int Order { get; set; }
            public int LineIndex { get; set; }
            public string Command { get; set; } = string.Empty;
        }

        public static AppSettings LoadSettings()
        {
            EnsureCreated();
            EnsureDshmarketPluginConfigured(SettingsFilePath);

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

                var startupItems = new List<ParsedCommandItem>();
                var serviceItems = new List<ParsedCommandItem>();

                // Matches keys like: command1 = ..., command 4 = ..., service1 = ..., service 5 = ..., cmd2 = ...
                var keyValRegex = new Regex(@"^([a-zA-Z_][a-zA-Z0-9_\-\.]*)(?:\s+(\d+))?\s*=(.*)$", RegexOptions.Compiled);

                for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
                {
                    string rawLine = lines[lineIdx];
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

                    string command = line;
                    int order = 1000 + lineIdx; // Default order based on file position

                    var match = keyValRegex.Match(line);
                    if (match.Success)
                    {
                        string keyPart = match.Groups[1].Value.Trim();
                        string indexPart = match.Groups[2].Value;
                        command = match.Groups[3].Value.Trim();

                        if (!string.IsNullOrEmpty(indexPart) && int.TryParse(indexPart, out int parsedIdx))
                        {
                            order = parsedIdx;
                        }
                        else
                        {
                            // Check if keyPart ends with digits, e.g. command4, service12
                            var digitMatch = Regex.Match(keyPart, @"\d+$");
                            if (digitMatch.Success && int.TryParse(digitMatch.Value, out int keyIdx))
                            {
                                order = keyIdx;
                            }
                        }
                    }

                    command = SanitizeCommand(command);

                    if (string.IsNullOrWhiteSpace(command))
                    {
                        continue;
                    }

                    var item = new ParsedCommandItem
                    {
                        Order = order,
                        LineIndex = lineIdx,
                        Command = command
                    };

                    if (currentSection.Equals("Services", StringComparison.OrdinalIgnoreCase) ||
                        currentSection.Equals("Background", StringComparison.OrdinalIgnoreCase) ||
                        currentSection.Equals("BackgroundServices", StringComparison.OrdinalIgnoreCase))
                    {
                        serviceItems.Add(item);
                    }
                    else
                    {
                        startupItems.Add(item);
                    }
                }

                // Sort items numerically by Order (1, 2, 3, 4, 5, ...), then by LineIndex
                settings.StartupCommands = startupItems
                    .OrderBy(x => x.Order)
                    .ThenBy(x => x.LineIndex)
                    .Select(x => x.Command)
                    .ToList();

                settings.ServiceCommands = serviceItems
                    .OrderBy(x => x.Order)
                    .ThenBy(x => x.LineIndex)
                    .Select(x => x.Command)
                    .ToList();

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
            settings.StartupCommands.Add("npm install -g uwu-x-proxy@latest");
            settings.StartupCommands.Add(DshmarketPluginCommand);

            settings.ServiceCommands.Add("call npx uwu-x-proxy");
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


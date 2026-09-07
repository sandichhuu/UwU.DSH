using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DshAntigravityLauncher
{
    public partial class MainWindow : Window
    {
        private readonly System.Collections.Generic.List<Process> _serviceProcesses = new System.Collections.Generic.List<Process>();
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        private string? _dshTokenUrl = null;
        private bool _dshLoaded = false;
        private bool _xProxyLoaded = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize session log
                AppLogger.Initialize();
                AppLogger.Log("DSH Proxies Launcher starting up...");

                // Initialize WebViews concurrently
                var initDshTask = WebDsh.EnsureCoreWebView2Async();
                var initXProxyTask = WebXProxy.EnsureCoreWebView2Async();

                await Task.WhenAll(initDshTask, initXProxyTask);

                // Default active tab is DSH -> set Source for DSH only (prevents double refresh & unnecessary background loading)
                WebDsh.Source = new Uri("http://127.0.0.1:3080/");
                _dshLoaded = true;

                // Start startup process sequence
                await StartServicesSequenceAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Log($"[ERROR] Initialization error: {ex.Message}");
                UpdateStatus($"Initialization error: {ex.Message}", isError: true);
            }
        }

        private async Task StartServicesSequenceAsync()
        {
            // 0. Check if Node.js is installed
            UpdateStatus("Checking Node.js environment...");
            bool isNodeInstalled = await IsNodeInstalledAsync();

            if (!isNodeInstalled)
            {
                UpdateStatus("❌ Node.js is not installed on this system!", isError: true);
                AppLogger.Log("[ERROR] Node.js is not installed on this system! Showing download dialog...");

                // Show Node.js missing download window
                var downloadWindow = new NodeDownloadWindow
                {
                    Owner = this
                };
                downloadWindow.ShowDialog();

                // Shutdown application if window closes
                Application.Current.Shutdown();
                return;
            }

            // 1. Load configuration from settings.ini (created automatically on first launch)
            AppLogger.Log("[CONFIG] Loading startup and service configuration from settings.ini...");
            var settings = SettingsManager.LoadSettings();

            // 2. Run startup commands sequentially from settings.ini
            int totalStartup = settings.StartupCommands.Count;
            if (totalStartup > 0)
            {
                AppLogger.Log($"[STARTUP] Executing {totalStartup} startup command(s)...");
                for (int i = 0; i < totalStartup; i++)
                {
                    string cmd = settings.StartupCommands[i];
                    string shortDisplay = cmd.Length > 60 ? cmd.Substring(0, 57) + "..." : cmd;
                    UpdateStatus($"Startup [{i + 1}/{totalStartup}]: {shortDisplay}");

                    bool success = await RunStartupCommandAsync(cmd, i + 1, totalStartup);
                    if (!success)
                    {
                        UpdateStatus($"Warning: Command [{i + 1}] returned non-zero code, continuing...", isError: false);
                    }
                }
                AppLogger.Log("[STARTUP] Completed all startup commands.");
            }
            else
            {
                AppLogger.Log("[STARTUP] No startup commands found in settings.ini. Continuing directly to services.");
            }

            // 3. Start background services from settings.ini
            UpdateStatus("Starting background services from settings.ini...");
            StartBackgroundServicesProcess(settings.ServiceCommands);

            // 4. Poll service health to update status
            _ = MonitorServicesHealthAsync(settings.ServiceCommands);
        }

        private Task<bool> IsNodeInstalledAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    AppLogger.Log("[NODE CHECK] Checking Node.js installation (cmd.exe /c node -v)...");
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c node -v",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using (var proc = Process.Start(psi))
                    {
                        if (proc != null)
                        {
                            string output = proc.StandardOutput.ReadToEnd();
                            string err = proc.StandardError.ReadToEnd();
                            proc.WaitForExit(3000);

                            if (!string.IsNullOrWhiteSpace(output))
                            {
                                AppLogger.Log($"[NODE CHECK STDOUT] {output.Trim()}");
                            }
                            if (!string.IsNullOrWhiteSpace(err))
                            {
                                AppLogger.Log($"[NODE CHECK STDERR] {err.Trim()}");
                            }

                            bool isOk = proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) && output.Trim().StartsWith("v");
                            AppLogger.Log($"[NODE CHECK] Result: {(isOk ? $"Installed ({output.Trim()})" : "Not Installed / Failed")}");
                            return isOk;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Log($"[NODE CHECK] Error: {ex.Message}");
                }
                return false;
            });
        }

        private Task<bool> RunStartupCommandAsync(string command, int stepIndex, int totalSteps)
        {
            return Task.Run(() =>
            {
                try
                {
                    string commandToRun = SettingsManager.SanitizeCommand(command);
                    AppLogger.Log($"--------------------------------------------------------------------------------");
                    AppLogger.Log($"[STARTUP STEP {stepIndex}/{totalSteps}] Executing: cmd.exe /c {commandToRun}");

                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {commandToRun}",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using var proc = new Process { StartInfo = psi };
                    proc.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            AppLogger.Log($"[STEP {stepIndex} STDOUT] {e.Data}");
                        }
                    };
                    proc.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            AppLogger.Log($"[STEP {stepIndex} STDERR] {e.Data}");
                        }
                    };

                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();

                    AppLogger.Log($"[STARTUP STEP {stepIndex}/{totalSteps}] Finished with exit code: {proc.ExitCode}");
                    return proc.ExitCode == 0;
                }
                catch (Exception ex)
                {
                    AppLogger.Log($"[STARTUP STEP {stepIndex}/{totalSteps}] Exception running command: {ex.Message}");
                    return false;
                }
            });
        }

        private void StartBackgroundServicesProcess(System.Collections.Generic.List<string> serviceCommands)
        {
            try
            {
                KillExistingServicesProcess();

                AppLogger.Log($"--------------------------------------------------------------------------------");
                AppLogger.Log($"[SERVICES] Starting {serviceCommands.Count} background service(s)...");

                int idx = 0;
                foreach (string cmd in serviceCommands)
                {
                    idx++;
                    int serviceNum = idx;
                    string commandToRun = SettingsManager.SanitizeCommand(cmd);

                    AppLogger.Log($"[SERVICE #{serviceNum}] Launching: cmd.exe /c {commandToRun}");

                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {commandToRun}",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                    proc.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            AppLogger.Log($"[SERVICE #{serviceNum} STDOUT] {e.Data}");

                            // Detect DSH web token URL if present
                            int tokenIdx = e.Data.IndexOf("http://127.0.0.1:3080/?token=", StringComparison.OrdinalIgnoreCase);
                            if (tokenIdx < 0)
                            {
                                tokenIdx = e.Data.IndexOf("http://localhost:3080/?token=", StringComparison.OrdinalIgnoreCase);
                            }

                            if (tokenIdx >= 0)
                            {
                                string rawUrl = e.Data.Substring(tokenIdx).Trim();
                                _dshTokenUrl = rawUrl;
                                AppLogger.Log($"[DSH] Detected token URL: {_dshTokenUrl}");
                                Dispatcher.Invoke(() =>
                                {
                                    if (WebDsh != null)
                                    {
                                        WebDsh.Source = new Uri(_dshTokenUrl);
                                        _dshLoaded = true;
                                    }
                                });
                            }
                        }
                    };

                    proc.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            AppLogger.Log($"[SERVICE #{serviceNum} STDERR] {e.Data}");
                        }
                    };

                    proc.Exited += (s, e) =>
                    {
                        AppLogger.Log($"[SERVICE #{serviceNum}] Process exited (PID {proc.Id}, ExitCode: {proc.ExitCode})");
                    };

                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    _serviceProcesses.Add(proc);

                    AppLogger.Log($"[SERVICE #{serviceNum}] Started with PID {proc.Id}");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log($"[SERVICES] Failed to launch services: {ex.Message}");
                UpdateStatus($"Failed to launch services: {ex.Message}", isError: true);
            }
        }

        private async Task MonitorServicesHealthAsync(System.Collections.Generic.List<string>? serviceCommands = null)
        {
            int attempts = 0;
            var commands = serviceCommands ?? new System.Collections.Generic.List<string>();
            int totalServices = commands.Count;

            // Determine which known endpoints to check based on configured commands
            bool expectDsh = commands.Count == 0 || System.Linq.Enumerable.Any(commands, c => c.Contains("dsh", StringComparison.OrdinalIgnoreCase) || c.Contains("3080"));
            bool expectXProxy = commands.Count == 0 || System.Linq.Enumerable.Any(commands, c => c.Contains("uwu-x-proxy", StringComparison.OrdinalIgnoreCase) || c.Contains("3081"));

            bool dshReady = !expectDsh;
            bool xProxyReady = !expectXProxy;

            while (attempts < 30) // Wait up to 60 seconds
            {
                attempts++;

                if (expectDsh && !dshReady)
                {
                    dshReady = await CheckUrlHealthAsync("http://127.0.0.1:3080/");
                }

                if (expectXProxy && !xProxyReady)
                {
                    xProxyReady = await CheckUrlHealthAsync("http://localhost:3081/");
                }

                if (dshReady && xProxyReady)
                {
                    Dispatcher.Invoke(() =>
                    {
                        string statusMsg;
                        if (totalServices > 2)
                        {
                            statusMsg = $"✅ All {totalServices} background services are online & running (DSH & X-Proxy active)!";
                        }
                        else
                        {
                            statusMsg = "✅ DSH (3080) & X-Proxy (3081) are online & running!";
                        }
                        UpdateStatus(statusMsg, isError: false, isSuccess: true);
                        // Refresh ONLY the currently active/visible tab
                        RefreshActiveTab();
                    });
                    return;
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        var active = new System.Collections.Generic.List<string>();
                        var waiting = new System.Collections.Generic.List<string>();

                        if (expectDsh) { if (dshReady) active.Add("DSH (3080)"); else waiting.Add("DSH (3080)"); }
                        if (expectXProxy) { if (xProxyReady) active.Add("X-Proxy (3081)"); else waiting.Add("X-Proxy (3081)"); }

                        string onlinePart = active.Count > 0 ? $"Online: {string.Join(", ", active)}. " : "";
                        string waitingPart = waiting.Count > 0 ? $"Waiting for: {string.Join(", ", waiting)}..." : "Waiting for services...";
                        UpdateStatus($"{onlinePart}{waitingPart}");
                    });
                }

                await Task.Delay(2000);
            }

            Dispatcher.Invoke(() =>
            {
                UpdateStatus("Services taking longer to respond. Click 'Reload' if pages are still loading.", isError: false);
            });
        }

        private void RefreshActiveTab()
        {
            if (WebDsh.Visibility == Visibility.Visible)
            {
                if (!string.IsNullOrEmpty(_dshTokenUrl))
                {
                    WebDsh.Source = new Uri(_dshTokenUrl);
                }
                else
                {
                    WebDsh.Reload();
                }
            }
            else if (WebXProxy.Visibility == Visibility.Visible)
            {
                WebXProxy.Reload();
            }
        }

        private async Task<bool> CheckUrlHealthAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                return response.IsSuccessStatusCode || ((int)response.StatusCode < 500);
            }
            catch
            {
                return false;
            }
        }

        private void UpdateStatus(string message, bool isError = false, bool isSuccess = false)
        {
            AppLogger.Log($"[STATUS] {message}");

            Dispatcher.Invoke(() =>
            {
                TxtStatus.Text = message;
                if (isError)
                {
                    StatusBanner.Background = new SolidColorBrush(Color.FromRgb(127, 29, 29)); // Dark red
                    StatusBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(185, 28, 28));
                }
                else if (isSuccess)
                {
                    StatusBanner.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59)); // Dark green
                    StatusBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(4, 120, 87));
                }
                else
                {
                    StatusBanner.Background = new SolidColorBrush(Color.FromRgb(30, 27, 75)); // Dark indigo
                    StatusBanner.BorderBrush = new SolidColorBrush(Color.FromRgb(55, 48, 163));
                }
            });
        }

        private void BtnTabDsh_Click(object sender, RoutedEventArgs e)
        {
            BtnTabDsh.Style = (Style)FindResource("TabButtonActiveStyle");
            BtnTabXProxy.Style = (Style)FindResource("TabButtonStyle");

            WebDsh.Visibility = Visibility.Visible;
            WebXProxy.Visibility = Visibility.Collapsed;

            if (!_dshLoaded)
            {
                string targetUrl = !string.IsNullOrEmpty(_dshTokenUrl) ? _dshTokenUrl : "http://127.0.0.1:3080/";
                WebDsh.Source = new Uri(targetUrl);
                _dshLoaded = true;
            }
        }

        private void BtnTabXProxy_Click(object sender, RoutedEventArgs e)
        {
            BtnTabXProxy.Style = (Style)FindResource("TabButtonActiveStyle");
            BtnTabDsh.Style = (Style)FindResource("TabButtonStyle");

            WebXProxy.Visibility = Visibility.Visible;
            WebDsh.Visibility = Visibility.Collapsed;

            // Lazy load X-Proxy tab only when opened
            if (!_xProxyLoaded)
            {
                WebXProxy.Source = new Uri("http://localhost:3081/");
                _xProxyLoaded = true;
            }
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            RefreshActiveTab();
        }

        private void BtnToggleHeader_Click(object sender, RoutedEventArgs e)
        {
            if (HeaderPanel.Visibility == Visibility.Visible)
            {
                HeaderPanel.Visibility = Visibility.Collapsed;
                BtnToggleHeaderFooter.Content = "👁️ Show Header";
            }
            else
            {
                HeaderPanel.Visibility = Visibility.Visible;
                BtnToggleHeaderFooter.Content = "👁️ Hide Header";
            }
        }

        private async void BtnRestartServices_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Log("[USER] Clicked Restart Services button.");
            UpdateStatus("Restarting background services...");
            await StartServicesSequenceAsync();
        }

        private async void BtnStopServices_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Log("[USER] Clicked Stop Services button.");
            await StopServicesSequenceAsync();
        }

        private async Task StopServicesSequenceAsync()
        {
            UpdateStatus("Stopping services (DSH & X-Proxy)...");

            // Kill DSH / background service process trees (covers `npx uwu-x-proxy`)
            KillExistingServicesProcess();
            await Task.CompletedTask;

            UpdateStatus("🛑 Services stopped (DSH & X-Proxy processes terminated).", isError: true);
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            AppLogger.Log("[APPLICATION] MainWindow is closing. Terminating services...");
            KillExistingServicesProcess();
            AppLogger.Log("[APPLICATION] Session terminated.");
        }

        private void KillExistingServicesProcess()
        {
            try
            {
                if (_serviceProcesses.Count > 0)
                {
                    AppLogger.Log($"[SHUTDOWN] Terminating {_serviceProcesses.Count} active service process(es)...");
                }

                foreach (var proc in _serviceProcesses)
                {
                    try
                    {
                        if (proc != null && !proc.HasExited)
                        {
                            AppLogger.Log($"[SHUTDOWN] Killing process tree for PID {proc.Id}...");
                            var psi = new ProcessStartInfo
                            {
                                FileName = "taskkill",
                                Arguments = $"/F /T /PID {proc.Id}",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            };
                            using var killProc = Process.Start(psi);
                            killProc?.WaitForExit(2000);

                            proc.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Log($"[SHUTDOWN] Error killing process: {ex.Message}");
                    }
                }
                _serviceProcesses.Clear();
            }
            catch (Exception ex)
            {
                AppLogger.Log($"[SHUTDOWN] Error clearing service processes: {ex.Message}");
            }
        }

        private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Log("[USER] Clicked Open Settings button.");
            SettingsManager.OpenSettingsFile();
        }

        private void BtnOpenLog_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Log("[USER] Clicked Open Log button.");
            AppLogger.OpenLogFile();
        }
    }
}

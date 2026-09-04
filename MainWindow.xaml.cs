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
        private bool _proxyLoaded = false;
        private bool _codexProxyLoaded = false;

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
                // Initialize WebViews concurrently
                var initDshTask = WebDsh.EnsureCoreWebView2Async();
                var initProxyTask = WebProxy.EnsureCoreWebView2Async();
                var initCodexTask = WebCodexProxy.EnsureCoreWebView2Async();

                await Task.WhenAll(initDshTask, initProxyTask, initCodexTask);

                // Default active tab is DSH -> set Source for DSH only (prevents double refresh & unnecessary background loading)
                WebDsh.Source = new Uri("http://127.0.0.1:3080/");
                _dshLoaded = true;

                // Start startup process sequence
                await StartServicesSequenceAsync();
            }
            catch (Exception ex)
            {
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

            // 1. First-run setup: npm config set allow-scripts=better-sqlite3 --location=user
            await EnsureFirstRunConfigAsync();

            // 2. Install Antigravity Proxy from npm and replace Codex Proxy with the requested Git build.
            UpdateStatus("Step 1/2: Installing Antigravity Proxy and Codex Proxy from Git...");
            bool installSuccess = await RunNpmInstallAsync();
            bool installCodexSuccess = await RunNpmInstallCodexFromGitAsync();

            if (!installSuccess || !installCodexSuccess)
            {
                UpdateStatus("Warning: npm install returned an error or warning, proceeding to start services...", isError: false);
            }

            // 3. Start background services
            UpdateStatus("Step 2/2: Starting background services (DSH Web, Antigravity Proxy & Codex Proxy)...");
            StartBackgroundServicesProcess();

            // 4. Poll service health to update status
            _ = MonitorServicesHealthAsync();
        }

        private Task<bool> IsNodeInstalledAsync()
        {
            return Task.Run(() =>
            {
                try
                {
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
                            proc.WaitForExit(3000);
                            return proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) && output.Trim().StartsWith("v");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Node check error: {ex.Message}");
                }
                return false;
            });
        }

        private string GetFirstRunFlagPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "DshAntigravityLauncher");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, "allow_scripts_configured.flag");
        }

        private async Task EnsureFirstRunConfigAsync()
        {
            string flagPath = GetFirstRunFlagPath();
            if (!File.Exists(flagPath))
            {
                UpdateStatus("First launch detected: Executing npm config set allow-scripts=better-sqlite3 --location=user...");
                bool success = await RunNpmConfigSetAllowScriptsAsync();
                if (success)
                {
                    try
                    {
                        File.WriteAllText(flagPath, $"Configured allow-scripts=better-sqlite3 on {DateTime.Now}");
                        Debug.WriteLine("First-run flag saved successfully.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to write first-run flag: {ex.Message}");
                    }
                }
                else
                {
                    UpdateStatus("Warning: First-run npm config set returned exit code != 0, continuing...", isError: false);
                }
            }
        }

        private Task<bool> RunNpmConfigSetAllowScriptsAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c npm config set allow-scripts=better-sqlite3 --location=user",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using (var proc = Process.Start(psi))
                    {
                        if (proc != null)
                        {
                            proc.WaitForExit();
                            return proc.ExitCode == 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"npm config set error: {ex.Message}");
                }
                return false;
            });
        }

        private Task<bool> RunNpmInstallAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c npm install -g antigravity-claude-proxy@latest",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using (var proc = Process.Start(psi))
                    {
                        if (proc != null)
                        {
                            proc.WaitForExit();
                            return proc.ExitCode == 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"npm install error: {ex.Message}");
                }
                return false;
            });
        }

        private Task<bool> RunNpmInstallCodexFromGitAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c npm uninstall -g codex-claude-proxy && npm install -g git+https://github.com/sandichhuu/codex-claude-proxy.git",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using (var proc = Process.Start(psi))
                    {
                        if (proc != null)
                        {
                            proc.WaitForExit();
                            return proc.ExitCode == 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"npm uninstall/install Codex Git package error: {ex.Message}");
                }
                return false;
            });
        }

        private void StartBackgroundServicesProcess()
        {
            try
            {
                KillExistingServicesProcess();

                // 1. Antigravity Proxy (daemon service)
                var psiProxy = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c call antigravity-claude-proxy start",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                var procProxy = Process.Start(psiProxy);
                if (procProxy != null) _serviceProcesses.Add(procProxy);

                // 2. Codex Proxy (foreground service)
                var psiCodex = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c call codex-claude-proxy start",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                var procCodex = Process.Start(psiCodex);
                if (procCodex != null) _serviceProcesses.Add(procCodex);

                // 3. DeepSeek Harness Web (foreground service)
                var psiDsh = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c call npx -y @deepseek-ai/dsh web --no-open",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                var procDsh = new Process { StartInfo = psiDsh };
                procDsh.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine($"[DSH StdOut] {e.Data}");
                        // Example line: dsh web: http://127.0.0.1:3080/?token=65RRWFzZLJwFdTc90TJozzYOndn_WMtqUdfvJWbqvDg
                        int tokenIdx = e.Data.IndexOf("http://127.0.0.1:3080/?token=", StringComparison.OrdinalIgnoreCase);
                        if (tokenIdx >= 0)
                        {
                            string rawUrl = e.Data.Substring(tokenIdx).Trim();
                            _dshTokenUrl = rawUrl;
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
                procDsh.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine($"[DSH StdErr] {e.Data}");
                    }
                };

                procDsh.Start();
                procDsh.BeginOutputReadLine();
                procDsh.BeginErrorReadLine();
                _serviceProcesses.Add(procDsh);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to launch services: {ex.Message}", isError: true);
            }
        }

        private async Task MonitorServicesHealthAsync()
        {
            int attempts = 0;
            bool dshReady = false;
            bool proxyReady = false;
            bool codexReady = false;

            while (attempts < 30) // Wait up to 60 seconds
            {
                attempts++;

                if (!dshReady)
                {
                    dshReady = await CheckUrlHealthAsync("http://127.0.0.1:3080/");
                }

                if (!proxyReady)
                {
                    proxyReady = await CheckUrlHealthAsync("http://localhost:8080/");
                }

                if (!codexReady)
                {
                    codexReady = await CheckUrlHealthAsync("http://localhost:8081/");
                }

                if (dshReady && proxyReady && codexReady)
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateStatus("✅ DSH (3080), Antigravity Proxy (8080) & Codex Proxy (8081) are online & running!", isError: false, isSuccess: true);
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

                        if (dshReady) active.Add("DSH (3080)"); else waiting.Add("DSH (3080)");
                        if (proxyReady) active.Add("Proxy (8080)"); else waiting.Add("Proxy (8080)");
                        if (codexReady) active.Add("Codex (8081)"); else waiting.Add("Codex (8081)");

                        UpdateStatus($"Online: {string.Join(", ", active)}. Waiting for: {string.Join(", ", waiting)}...");
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
            else if (WebProxy.Visibility == Visibility.Visible)
            {
                WebProxy.Reload();
            }
            else if (WebCodexProxy.Visibility == Visibility.Visible)
            {
                WebCodexProxy.Reload();
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
            BtnTabProxy.Style = (Style)FindResource("TabButtonStyle");
            BtnTabCodexProxy.Style = (Style)FindResource("TabButtonStyle");

            WebDsh.Visibility = Visibility.Visible;
            WebProxy.Visibility = Visibility.Collapsed;
            WebCodexProxy.Visibility = Visibility.Collapsed;

            if (!_dshLoaded)
            {
                string targetUrl = !string.IsNullOrEmpty(_dshTokenUrl) ? _dshTokenUrl : "http://127.0.0.1:3080/";
                WebDsh.Source = new Uri(targetUrl);
                _dshLoaded = true;
            }
        }

        private void BtnTabProxy_Click(object sender, RoutedEventArgs e)
        {
            BtnTabProxy.Style = (Style)FindResource("TabButtonActiveStyle");
            BtnTabDsh.Style = (Style)FindResource("TabButtonStyle");
            BtnTabCodexProxy.Style = (Style)FindResource("TabButtonStyle");

            WebProxy.Visibility = Visibility.Visible;
            WebDsh.Visibility = Visibility.Collapsed;
            WebCodexProxy.Visibility = Visibility.Collapsed;

            // Lazy load Antigravity Proxy tab only when opened
            if (!_proxyLoaded)
            {
                WebProxy.Source = new Uri("http://localhost:8080/");
                _proxyLoaded = true;
            }
        }

        private void BtnTabCodexProxy_Click(object sender, RoutedEventArgs e)
        {
            BtnTabCodexProxy.Style = (Style)FindResource("TabButtonActiveStyle");
            BtnTabDsh.Style = (Style)FindResource("TabButtonStyle");
            BtnTabProxy.Style = (Style)FindResource("TabButtonStyle");

            WebCodexProxy.Visibility = Visibility.Visible;
            WebDsh.Visibility = Visibility.Collapsed;
            WebProxy.Visibility = Visibility.Collapsed;

            // Lazy load Codex Proxy tab only when opened
            if (!_codexProxyLoaded)
            {
                WebCodexProxy.Source = new Uri("http://localhost:8081/");
                _codexProxyLoaded = true;
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
            UpdateStatus("Restarting background services...");
            await StartServicesSequenceAsync();
        }

        private async void BtnStopServices_Click(object sender, RoutedEventArgs e)
        {
            await StopServicesSequenceAsync();
        }

        private async Task StopServicesSequenceAsync()
        {
            UpdateStatus("Stopping services (DSH, Antigravity Proxy & Codex Proxy)...");

            // 1. Kill DSH / background service process trees
            KillExistingServicesProcess();

            // 2. Execute antigravity-claude-proxy stop
            await RunProxyStopAsync();

            UpdateStatus("🛑 Services stopped (DSH & proxy processes terminated).", isError: true);
        }

        private Task<bool> RunProxyStopAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c antigravity-claude-proxy stop",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using (var proc = Process.Start(psi))
                    {
                        if (proc != null)
                        {
                            proc.WaitForExit(3000);
                            return proc.ExitCode == 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"antigravity-claude-proxy stop error: {ex.Message}");
                }
                return false;
            });
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            KillExistingServicesProcess();
            _ = RunProxyStopAsync();
        }

        private void KillExistingServicesProcess()
        {
            try
            {
                foreach (var proc in _serviceProcesses)
                {
                    try
                    {
                        if (proc != null && !proc.HasExited)
                        {
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
                        Debug.WriteLine($"Error killing process: {ex.Message}");
                    }
                }
                _serviceProcesses.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error clearing service processes: {ex.Message}");
            }
        }
    }
}

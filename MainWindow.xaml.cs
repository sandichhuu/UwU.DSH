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
        private Process? _serviceProcess;
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        private bool _dshLoaded = false;
        private bool _proxyLoaded = false;

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

                await Task.WhenAll(initDshTask, initProxyTask);

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

            // 2. Ensure npm install -g antigravity-claude-proxy@latest
            UpdateStatus("Step 1/2: Ensuring npm install -g antigravity-claude-proxy@latest...");
            bool installSuccess = await RunNpmInstallAsync();

            if (!installSuccess)
            {
                UpdateStatus("Warning: npm install returned an error or warning, proceeding to start services...", isError: false);
            }

            // 3. Start background services
            UpdateStatus("Step 2/2: Starting background services (DSH Web + Antigravity Proxy)...");
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

        private void StartBackgroundServicesProcess()
        {
            try
            {
                KillExistingServicesProcess();

                string command = "antigravity-claude-proxy start & npx -y @deepseek-ai/dsh web --no-open";

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{command}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                _serviceProcess = Process.Start(psi);
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

                if (dshReady && proxyReady)
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateStatus("✅ Both Deepseek Harness (3080) and Antigravity Proxy (8080) are online & running!", isError: false, isSuccess: true);
                        // Refresh ONLY the currently active/visible tab
                        RefreshActiveTab();
                    });
                    return;
                }
                else if (dshReady)
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateStatus("⚡ Deepseek Harness (3080) is online. Waiting for Antigravity Proxy (8080)...");
                    });
                }
                else if (proxyReady)
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateStatus("🚀 Antigravity Proxy (8080) is online. Waiting for Deepseek Harness (3080)...");
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
                WebDsh.Reload();
            }
            else if (WebProxy.Visibility == Visibility.Visible)
            {
                WebProxy.Reload();
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

            WebDsh.Visibility = Visibility.Visible;
            WebProxy.Visibility = Visibility.Collapsed;

            if (!_dshLoaded)
            {
                WebDsh.Source = new Uri("http://127.0.0.1:3080/");
                _dshLoaded = true;
            }
        }

        private void BtnTabProxy_Click(object sender, RoutedEventArgs e)
        {
            BtnTabProxy.Style = (Style)FindResource("TabButtonActiveStyle");
            BtnTabDsh.Style = (Style)FindResource("TabButtonStyle");

            WebProxy.Visibility = Visibility.Visible;
            WebDsh.Visibility = Visibility.Collapsed;

            // Lazy load Antigravity Proxy tab only when opened
            if (!_proxyLoaded)
            {
                WebProxy.Source = new Uri("http://localhost:8080/");
                _proxyLoaded = true;
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
            UpdateStatus("Stopping services (DSH & Antigravity Proxy)...");

            // 1. Kill DSH / piped service process tree
            KillExistingServicesProcess();

            // 2. Execute antigravity-claude-proxy stop
            bool stopSuccess = await RunProxyStopAsync();

            if (stopSuccess)
            {
                UpdateStatus("🛑 Services stopped successfully (DSH terminated & antigravity-claude-proxy stop executed).", isError: true);
            }
            else
            {
                UpdateStatus("🛑 Services stopped (DSH terminated).", isError: true);
            }
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
                if (_serviceProcess != null && !_serviceProcess.HasExited)
                {
                    // Kill process tree on Windows
                    var psi = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/F /T /PID {_serviceProcess.Id}",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var killProc = Process.Start(psi);
                    killProc?.WaitForExit(2000);

                    _serviceProcess.Dispose();
                    _serviceProcess = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error killing services process: {ex.Message}");
            }
        }
    }
}

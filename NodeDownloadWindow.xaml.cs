using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace DshAntigravityLauncher
{
    public partial class NodeDownloadWindow : Window
    {
        public NodeDownloadWindow()
        {
            InitializeComponent();
            Closing += NodeDownloadWindow_Closing;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            OpenDownloadPageAndExit();
            e.Handled = true;
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            OpenDownloadPageAndExit();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void OpenDownloadPageAndExit()
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://nodejs.org/") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open URL: {ex.Message}");
            }
            Application.Current.Shutdown();
        }

        private void NodeDownloadWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}

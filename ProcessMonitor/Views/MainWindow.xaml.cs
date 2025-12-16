using System.Windows;
using ProcessMonitor.ViewModels;

namespace ProcessMonitor.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void OpenMonitoringWindow_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel mainVm)
                return;

            var win = new MonitoringWindow
            {
                Owner = this,
                DataContext = mainVm.MonitoringViewModel
            };
            win.Show();
        }
    }
}
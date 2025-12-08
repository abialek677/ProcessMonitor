using System.Windows;
using ProcessMonitor.ViewModels;

namespace ProcessMonitor
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
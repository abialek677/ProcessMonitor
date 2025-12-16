using System.Windows.Input;

namespace ProcessMonitor.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public MonitoringViewModel MonitoringViewModel { get; }
        public ICommand StartMonitoringCommand { get; }
        public ICommand StopMonitoringCommand { get; }
        public ProcessListViewModel ProcessListViewModel { get; }
        
        public MainViewModel()
        {
            var monitoringVm = new MonitoringViewModel();
            MonitoringViewModel = monitoringVm;
            ProcessListViewModel = new ProcessListViewModel(monitoringVm);

            StartMonitoringCommand = new RelayCommand(_ =>
            {
                var p = ProcessListViewModel.SelectedProcess;
                if (p != null)
                    MonitoringViewModel.StartMonitoring(p.ProcessId, p.ProcessName);
            });

            StopMonitoringCommand = new RelayCommand(_ =>
            {
                var p = ProcessListViewModel.SelectedProcess;
                if (p != null)
                    MonitoringViewModel.StopMonitoring(p.ProcessId);
            });
        }
    }
}
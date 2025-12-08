using System.Windows.Input;
using ProcessMonitor.ViewModels;

namespace ProcessMonitor.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ProcessListViewModel _processListViewModel;
        private MonitoringViewModel _monitoringViewModel;

        public ProcessListViewModel ProcessListViewModel
        {
            get => _processListViewModel;
            set => Set(ref _processListViewModel, value);
        }

        public MonitoringViewModel MonitoringViewModel
        {
            get => _monitoringViewModel;
            set => Set(ref _monitoringViewModel, value);
        }

        public ICommand StartMonitoringCommand { get; }
        public ICommand StopMonitoringCommand { get; }

        public MainViewModel()
        {
            ProcessListViewModel = new ProcessListViewModel();
            MonitoringViewModel = new MonitoringViewModel();

            StartMonitoringCommand = new RelayCommand(_ =>
            {
                if (ProcessListViewModel.SelectedProcess != null)
                {
                    MonitoringViewModel.StartMonitoring(
                        ProcessListViewModel.SelectedProcess.ProcessId,
                        ProcessListViewModel.SelectedProcess.ProcessName);
                }
            });

            StartMonitoringCommand = new RelayCommand(_ =>
            {
                if (ProcessListViewModel.SelectedProcess != null)
                {
                    MonitoringViewModel.StartMonitoring(
                        ProcessListViewModel.SelectedProcess.ProcessId,
                        ProcessListViewModel.SelectedProcess.ProcessName);
                }
            });
        }
    }
}
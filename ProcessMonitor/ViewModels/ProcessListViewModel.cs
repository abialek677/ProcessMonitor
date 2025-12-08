using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProcessMonitor.Models;
using ProcessMonitor.Services;

namespace ProcessMonitor.ViewModels
{
    public class ProcessListViewModel : ViewModelBase
    {
        private readonly ProcessMonitoringService _service = new ProcessMonitoringService();
        private ObservableCollection<ProcessInfo> _processes;
        private ProcessInfo _selectedProcess;
        private string _filterText = "";
        private string _sortBy = "Name";
        private Task _refreshTask;
        private bool _isAutoRefreshing = false;
        private int _refreshIntervalMs = 2000;
        private bool _shouldStopRefresh = false;

        public ObservableCollection<ProcessInfo> Processes
        {
            get => _processes;
            set => Set(ref _processes, value);
        }

        public ProcessInfo SelectedProcess
        {
            get => _selectedProcess;
            set => Set(ref _selectedProcess, value);
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                Set(ref _filterText, value);
                RefreshProcessList();
            }
        }

        public string SortBy
        {
            get => _sortBy;
            set
            {
                Set(ref _sortBy, value);
                RefreshProcessList();
            }
        }

        public int RefreshIntervalMs
        {
            get => _refreshIntervalMs;
            set => Set(ref _refreshIntervalMs, value);
        }

        public bool IsAutoRefreshing
        {
            get => _isAutoRefreshing;
            set
            {
                Set(ref _isAutoRefreshing, value);
                if (value)
                    StartAutoRefresh();
                else
                    StopAutoRefresh();
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand SetPriorityCommand { get; }
        public ICommand KillProcessCommand { get; }
        public ICommand ToggleMonitoringCommand { get; }

        public ProcessListViewModel()
        {
            Processes = new ObservableCollection<ProcessInfo>();

            RefreshCommand = new RelayCommand(_ =>
            {
                Debug.WriteLine(">>> REFRESH COMMAND EXECUTED");
                RefreshProcessList();
            });

            SetPriorityCommand = new RelayCommand(ExecuteSetPriority, CanExecutePriority);
            KillProcessCommand = new RelayCommand(ExecuteKillProcess, CanExecuteKillProcess);
            ToggleMonitoringCommand = new RelayCommand(ExecuteToggleMonitoring, _ => SelectedProcess != null);

            RefreshProcessList();
        }

        public void RefreshProcessList()
        {
            Task.Run(() =>
            {
                try
                {
                    var all = _service.GetAllProcesses();
                    Debug.WriteLine("PROCESY: " + all.Count);

                    var filtered = all
                        .Where(p => p.ProcessName != null &&
                                    p.ProcessName.Contains(FilterText ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    var sorted = SortBy switch
                    {
                        "Name" => filtered.OrderBy(p => p.ProcessName).ToList(),
                        "Memory" => filtered.OrderByDescending(p => p.WorkingSet).ToList(),
                        "PID" => filtered.OrderBy(p => p.ProcessId).ToList(),
                        "Threads" => filtered.OrderByDescending(p => p.ThreadCount).ToList(),
                        _ => filtered
                    };

                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Processes.Clear();
                        foreach (var p in sorted)
                            Processes.Add(p);
                    }));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("RefreshProcessList ERROR: " + ex);
                }
            });
        }

        public void StartAutoRefresh()
        {
            _shouldStopRefresh = false;
            _refreshTask = Task.Run(async () =>
            {
                while (!_shouldStopRefresh)
                {
                    RefreshProcessList();
                    await Task.Delay(RefreshIntervalMs);
                }
            });
        }

        public void StopAutoRefresh()
        {
            _shouldStopRefresh = true;
        }

        private void ExecuteSetPriority(object priority)
        {
            if (SelectedProcess == null) return;

            if (Enum.TryParse<ProcessPriorityClass>(priority.ToString(), out var p))
            {
                _service.SetProcessPriority(SelectedProcess.ProcessId, p);
                SelectedProcess.Priority = (int)p;
            }
        }

        private bool CanExecutePriority(object _) => SelectedProcess != null;

        private void ExecuteKillProcess(object _)
        {
            if (SelectedProcess == null) return;
            _service.KillProcess(SelectedProcess.ProcessId);
            RefreshProcessList();
        }

        private bool CanExecuteKillProcess(object _) => SelectedProcess != null;

        private void ExecuteToggleMonitoring(object _)
        {
            if (SelectedProcess == null) return;

            if (_service.IsMonitoring(SelectedProcess.ProcessId))
            {
                _service.StopMonitoringProcess(SelectedProcess.ProcessId);
            }
            else
            {
                // start monitorowania obsługujesz w MainViewModel
            }

            OnPropertyChanged(nameof(SelectedProcess));
        }

        public bool IsProcessMonitored(int processId) => _service.IsMonitoring(processId);
    }
}

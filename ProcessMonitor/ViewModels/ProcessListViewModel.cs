using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using ProcessMonitor.Models;
using ProcessMonitor.Services;

namespace ProcessMonitor.ViewModels
{
    public class ProcessListViewModel : ViewModelBase
    {
        private readonly ProcessMonitoringService _service = new();
        private ObservableCollection<ProcessInfo> _processes = new();
        private ProcessInfo _selectedProcess;
        private string _filterText = "";
        private string _sortBy = "Name";
        private Task _refreshTask;
        private bool _isAutoRefreshing;
        private int _refreshIntervalMs = 2000;
        private bool _shouldStopRefresh;
        private bool _isRefreshing;
        
        private string _minThreadsText;
        private string _maxThreadsText;
        private string _minMemoryMbText;
        private string _maxMemoryMbText;

        public string MinThreadsText
        {
            get => _minThreadsText;
            set
            {
                if (Set(ref _minThreadsText, value))
                    RefreshProcessList();
            }
        }

        public string MaxThreadsText
        {
            get => _maxThreadsText;
            set
            {
                if (Set(ref _maxThreadsText, value))
                    RefreshProcessList();
            }
        }

        public string MinMemoryMbText
        {
            get => _minMemoryMbText;
            set
            {
                if (Set(ref _minMemoryMbText, value))
                    RefreshProcessList();
            }
        }

        public string MaxMemoryMbText
        {
            get => _maxMemoryMbText;
            set
            {
                if (Set(ref _maxMemoryMbText, value))
                    RefreshProcessList();
            }
        }

        public ObservableCollection<ProcessInfo> Processes
        {
            get => _processes;
            set => Set(ref _processes, value);
        }

        public ProcessInfo SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                if (Set(ref _selectedProcess, value))
                {
                    if (value != null)
                        Task.Run(() => _service.PopulateDetails(value));
                }
            }
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (Set(ref _filterText, value))
                    RefreshProcessList();
            }
        }

        public string SortBy
        {
            get => _sortBy;
            set
            {
                if (Set(ref _sortBy, value))
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
                if (Set(ref _isAutoRefreshing, value))
                {
                    if (value)
                        StartAutoRefresh();
                    else
                        StopAutoRefresh();
                }
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand SetPriorityCommand { get; }
        public ICommand KillProcessCommand { get; }

        public ProcessListViewModel()
        {
            RefreshCommand = new RelayCommand(_ => RefreshProcessList());
            SetPriorityCommand = new RelayCommand(ExecuteSetPriority, _ => SelectedProcess != null);
            KillProcessCommand = new RelayCommand(ExecuteKillProcess, _ => SelectedProcess != null);

            RefreshProcessList();
        }

        public void RefreshProcessList()
        {
            int?  minThreads  = int.TryParse(MinThreadsText,  out var mt) ? mt : null;
            int?  maxThreads  = int.TryParse(MaxThreadsText,  out var xt) ? xt : null;
            long? minMemoryMb = long.TryParse(MinMemoryMbText, out var mm) ? mm : null;
            long? maxMemoryMb = long.TryParse(MaxMemoryMbText, out var xm) ? xm : null;
            
            if (_isRefreshing) return;
            _isRefreshing = true;

            _refreshTask = Task.Run(() =>
            {
                try
                {
                    var all = _service.GetAllProcesses();

                    var filterText = FilterText ?? string.Empty;

                    var filtered = all.Where(p =>
                        (string.IsNullOrEmpty(filterText) ||
                         (p.ProcessName?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                         p.ProcessId.ToString().Contains(filterText) ||
                         p.ThreadCount.ToString().Contains(filterText) ||
                         (p.WorkingSet / (1024 * 1024)).ToString().Contains(filterText)) &&

                        (!minThreads.HasValue  || p.ThreadCount >= minThreads.Value) &&
                        (!maxThreads.HasValue  || p.ThreadCount <= maxThreads.Value) &&
                        (!minMemoryMb.HasValue || (p.WorkingSet / (1024 * 1024)) >= minMemoryMb.Value) &&
                        (!maxMemoryMb.HasValue || (p.WorkingSet / (1024 * 1024)) <= maxMemoryMb.Value)
                    ).ToList();

                    var sorted = SortBy switch
                    {
                        "Name"    => filtered.OrderBy(p => p.ProcessName).ToList(),
                        "Memory"  => filtered.OrderByDescending(p => p.WorkingSet).ToList(),
                        "PID"     => filtered.OrderBy(p => p.ProcessId).ToList(),
                        "Threads" => filtered.OrderByDescending(p => p.ThreadCount).ToList(),
                        _         => filtered
                    };

                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // zapamiętaj aktualnie wybrany PID (to, co user ma TERAZ)
                        var currentSelectedPid = SelectedProcess?.ProcessId;

                        // zbuduj nową listę, ale bez ruszania SelectedProcess bez potrzeby
                        Processes.Clear();
                        foreach (var p in sorted)
                            Processes.Add(p);

                        if (currentSelectedPid.HasValue)
                        {
                            var match = Processes.FirstOrDefault(x => x.ProcessId == currentSelectedPid.Value);
                            if (match != null)
                                SelectedProcess = match; // tylko jeśli proces nadal istnieje
                            else
                                SelectedProcess = null;  // proces zniknął – zaznaczenie znika naturalnie
                        }
                        // jeśli currentSelectedPid == null – nic nie zaznaczamy
                    }));
                }
                finally
                {
                    _isRefreshing = false;
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

        public void StopAutoRefresh() => _shouldStopRefresh = true;

        private void ExecuteSetPriority(object priority)
        {
            if (SelectedProcess == null || priority == null) return;

            if (Enum.TryParse<ProcessPriorityClass>(priority.ToString(), out var p))
            {
                _service.SetProcessPriority(SelectedProcess.ProcessId, p);
                SelectedProcess.Priority = (int)p;
            }
        }

        private void ExecuteKillProcess(object _)
        {
            if (SelectedProcess == null) return;
            _service.KillProcess(SelectedProcess.ProcessId);
            RefreshProcessList();
        }
    }
}
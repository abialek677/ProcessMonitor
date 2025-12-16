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
        private readonly Dictionary<int, ProcessInfo> _processCache = new();
        private ObservableCollection<ProcessInfo> _processes = [];
        private readonly ProcessMonitoringService _service = new();
        private readonly MonitoringViewModel _monitoringVm;
        private ProcessInfo? _selectedProcess;
        
        // commands
        public ICommand RefreshCommand { get; }
        public ICommand SetPriorityCommand { get; }
        public ICommand KillProcessCommand { get; }
        
        private string _sortBy = "Name";
        private Task? _refreshTask;
        private bool _isAutoRefreshing;
        private int _refreshIntervalMs = 2000;
        private bool _shouldStopRefresh;
        private bool _isRefreshing;
        
        // filtering fields
        private string _minThreadsText = string.Empty;
        private string _maxThreadsText = string.Empty;
        private string _minMemoryMbText = string.Empty;
        private string _maxMemoryMbText = string.Empty;
        private string _filterText = "";
        private string _filterPidText;
        
        public string FilterPidText
        {
            get => _filterPidText;
            set
            {
                if (Set(ref _filterPidText, value))
                    RefreshProcessList();
            }
        }
        
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

        public ProcessInfo? SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                if (!Set(ref _selectedProcess, value))
                {
                    return;
                }
                
                if (value != null)
                    Task.Run(() => _service.PopulateDetails(value));
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
        
        public ProcessListViewModel(MonitoringViewModel monitoringVm)
        {
            _monitoringVm = monitoringVm;
            RefreshCommand = new RelayCommand(_ => RefreshProcessList());
            SetPriorityCommand = new RelayCommand(ExecuteSetPriority, _ => SelectedProcess != null);
            KillProcessCommand = new RelayCommand(ExecuteKillProcess, _ => SelectedProcess != null);

            RefreshProcessList();
        }

        private void RefreshProcessList()
        {
            int? minThreads = int.TryParse(MinThreadsText, out var mt) ? mt : null;
            int? maxThreads = int.TryParse(MaxThreadsText, out var xt) ? xt : null;
            long? minMemoryMb = long.TryParse(MinMemoryMbText, out var mm) ? mm : null;
            long? maxMemoryMb = long.TryParse(MaxMemoryMbText, out var xm) ? xm : null;
            
            if (_isRefreshing) return;
            _isRefreshing = true;

            _refreshTask = Task.Run(() =>
            {
                try
                {
                    var all = _service.GetAllProcesses();
                    
                    foreach (var p in all)
                    {
                        if (_processCache.TryGetValue(p.ProcessId, out var existing))
                        {
                            existing.ProcessName = p.ProcessName;
                            existing.ThreadCount = p.ThreadCount;
                            existing.WorkingSet = p.WorkingSet;
                            existing.Priority = p.Priority;
                            existing.ProcessPath = p.ProcessPath;
                            existing.StartTime = p.StartTime;
                            existing.IsMonitored = p.IsMonitored;
                            existing.MonitoringStartTime = p.MonitoringStartTime;
                            
                        }
                        else
                        {
                            _processCache[p.ProcessId] = p;
                        }
                        
                        
                        var monitored = _monitoringVm.MonitoredProcesses
                            .FirstOrDefault(m => m.ProcessId == p.ProcessId);

                        if (monitored != null)
                        {
                            p.IsMonitored = monitored.IsMonitoring;
                            p.MonitoringStartTime = monitored.MonitoringStartTime;
                        }
                        else
                        {
                            p.IsMonitored = false;
                            p.MonitoringStartTime = null;
                        }
                    }
                    
                    var pidsNow = new HashSet<int>(all.Select(x => x.ProcessId));
                    var toRemove = _processCache.Keys.Where(pid => !pidsNow.Contains(pid)).ToList();
                    foreach (var pid in toRemove)
                        _processCache.Remove(pid);

                    var filterText = FilterText ?? string.Empty;
                    var filterPidText = FilterPidText ?? string.Empty;

                    var filtered = all.Where(p =>
                        (string.IsNullOrEmpty(filterText) ||
                         (p.ProcessName?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                         p.ProcessId.ToString().Contains(filterText) ||
                         p.ThreadCount.ToString().Contains(filterText) ||
                         (p.WorkingSet / (1024 * 1024)).ToString().Contains(filterText)) &&
                        
                        (string.IsNullOrWhiteSpace(filterPidText)
                         || p.ProcessId.ToString().StartsWith(filterPidText.Trim())) &&

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
                        var currentSelectedPid = SelectedProcess?.ProcessId;

                        // remove non-existing items
                        for (var i = Processes.Count - 1; i >= 0; i--)
                        {
                            var item = Processes[i];
                            if (!sorted.Contains(item))
                                Processes.RemoveAt(i);
                        }

                        // add new items
                        foreach (var p in sorted)
                        {
                            if (!Processes.Contains(p))
                                Processes.Add(p);
                        }

                        if (!currentSelectedPid.HasValue) return;
                        
                        var match = Processes.FirstOrDefault(x => x.ProcessId == currentSelectedPid.Value);
                        SelectedProcess = match;
                    }));
                }
                finally
                {
                    _isRefreshing = false;
                }
            });
        }

        private void StartAutoRefresh()
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

        private void StopAutoRefresh() => _shouldStopRefresh = true;

        private void ExecuteSetPriority(object priority)
        {
            if (SelectedProcess == null || priority == null) return;

            if (!Enum.TryParse<ProcessPriorityClass>(priority.ToString(), out var p)) return;
            
            _service.SetProcessPriority(SelectedProcess.ProcessId, p);
        }

        private void ExecuteKillProcess(object _)
        {
            if (SelectedProcess == null) return;
            _service.KillProcess(SelectedProcess.ProcessId);
            RefreshProcessList();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ProcessMonitor.Models;
using ProcessMonitor.Services;

namespace ProcessMonitor.ViewModels
{
    public class MonitoringViewModel : ViewModelBase
    {
        private readonly ProcessMonitoringService _service = new();
        private readonly Dictionary<int, MonitoredProcess> _processSnapshots = new();
        private ObservableCollection<MonitoredProcess> _monitoredProcesses = new();
        private MonitoredProcess _selectedMonitoredProcess;
        private int _samplingIntervalMs = 1000; // sensowny default

        public ObservableCollection<MonitoredProcess> MonitoredProcesses
        {
            get => _monitoredProcesses;
            set => Set(ref _monitoredProcesses, value);
        }

        public MonitoredProcess SelectedMonitoredProcess
        {
            get => _selectedMonitoredProcess;
            set => Set(ref _selectedMonitoredProcess, value);
        }

        public int SamplingIntervalMs
        {
            get => _samplingIntervalMs;
            set => Set(ref _samplingIntervalMs, value);
        }

        public ICommand RemoveMonitoringCommand { get; }

        public MonitoringViewModel()
        {
            RemoveMonitoringCommand = new RelayCommand(_ => ExecuteRemoveMonitoring(), _ => SelectedMonitoredProcess != null);
        }

        public void StartMonitoring(int processId, string processName)
        {
            if (_processSnapshots.ContainsKey(processId))
                return;

            var monitored = new MonitoredProcess
            {
                ProcessId = processId,
                ProcessName = processName,
                MonitoringStartTime = DateTime.Now,
                IsMonitoring = true,
                SampleCount = 0,
                MaxMemoryUsage = 0,
                TotalMemoryAccumulated = 0
            };

            _processSnapshots[processId] = monitored;
            MonitoredProcesses.Add(monitored);

            _service.StartMonitoringProcess(processId, processName, SamplingIntervalMs, snapshot =>
            {
                if (snapshot == null) return;

                if (_processSnapshots.TryGetValue(processId, out var proc))
                {
                    proc.MaxMemoryUsage = Math.Max(proc.MaxMemoryUsage, snapshot.MemoryUsage);
                    proc.TotalMemoryAccumulated += snapshot.MemoryUsage;
                    proc.SampleCount++;
                    proc.AverageMemoryUsage = proc.TotalMemoryAccumulated / Math.Max(1, proc.SampleCount);
                }
            });
        }

        public void StopMonitoring(int processId)
        {
            _service.StopMonitoringProcess(processId);

            if (_processSnapshots.TryGetValue(processId, out var proc))
            {
                proc.IsMonitoring = false;
                proc.MonitoringEndTime = DateTime.Now;
            }
        }

        private void ExecuteRemoveMonitoring()
        {
            if (SelectedMonitoredProcess == null) return;

            StopMonitoring(SelectedMonitoredProcess.ProcessId);
            _processSnapshots.Remove(SelectedMonitoredProcess.ProcessId);
            MonitoredProcesses.Remove(SelectedMonitoredProcess);
        }

        public string GetMonitoringTime(int processId)
        {
            if (_processSnapshots.TryGetValue(processId, out var proc))
            {
                var end = proc.MonitoringEndTime ?? DateTime.Now;
                var duration = end - proc.MonitoringStartTime;
                return $"{duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            }
            return "00:00:00";
        }
    }
}

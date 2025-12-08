using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ProcessMonitor.Models;
using ProjectMonitor.Models;

namespace ProcessMonitor.Services
{
    public class ProcessMonitoringService
    {
        private readonly Dictionary<int, Task> _monitoringTasks = new();
        private readonly Dictionary<int, bool> _cancelTokens = new();

        // LEKKI listing do listy procesów
        public List<ProcessInfo> GetAllProcesses()
        {
            var result = new List<ProcessInfo>();

            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    var info = new ProcessInfo
                    {
                        ProcessId = proc.Id,
                        ProcessName = proc.ProcessName,
                        ThreadCount = proc.Threads.Count,
                        WorkingSet = proc.WorkingSet64,
                        StartTime = SafeGetStartTime(proc)
                    };

                    try { info.Priority = (int)proc.PriorityClass; }
                    catch { info.Priority = 0; }

                    try { info.ProcessPath = proc.MainModule?.FileName ?? "N/A"; }
                    catch { info.ProcessPath = "N/A"; }

                    result.Add(info);
                }
                catch
                {
                    // pojedyncze procesy z błędami pomijamy
                }
            }

            return result.OrderBy(p => p.ProcessName).ToList();
        }

        // Szczegóły dla master/detail – wywołuj TYLKO dla zaznaczonego procesu
        public void PopulateDetails(ProcessInfo info)
        {
            if (info == null) return;

            try
            {
                var proc = Process.GetProcessById(info.ProcessId);

                // NAJPIERW zbierz dane w lokalne listy (poza UI thread)
                var threads = new List<ThreadInfo>();
                try
                {
                    foreach (ProcessThread thread in proc.Threads)
                    {
                        threads.Add(new ThreadInfo
                        {
                            ThreadId = thread.Id,
                            ThreadState = thread.ThreadState.ToString(),
                            Priority = thread.CurrentPriority
                        });
                    }
                }
                catch { }

                var modules = new List<ModuleInfo>();
                try
                {
                    foreach (ProcessModule module in proc.Modules)
                    {
                        modules.Add(new ModuleInfo
                        {
                            ModuleName = module.ModuleName,
                            ModuleFileName = module.FileName,
                            ModuleMemorySize = module.ModuleMemorySize
                        });
                    }
                }
                catch { }

                // A potem zaktualizuj ObservableCollection na wątku UI
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    info.Threads.Clear();
                    foreach (var t in threads)
                        info.Threads.Add(t);

                    info.Modules.Clear();
                    foreach (var m in modules)
                        info.Modules.Add(m);
                }));
            }
            catch
            {
                // proces mógł się zakończyć / brak uprawnień
            }
        }


        private DateTime SafeGetStartTime(Process process)
        {
            try { return process.StartTime; }
            catch { return DateTime.MinValue; }
        }

        public bool SetProcessPriority(int processId, ProcessPriorityClass priority)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                process.PriorityClass = priority;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool KillProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                process.Kill();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public ProcessSnapshot GetProcessSnapshot(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                return new ProcessSnapshot
                {
                    ProcessId = processId,
                    ProcessName = process.ProcessName,
                    MemoryUsage = process.WorkingSet64,
                    SampleTime = DateTime.Now
                };
            }
            catch
            {
                return null;
            }
        }

        public Task StartMonitoringProcess(
            int processId,
            string processName,
            int samplingIntervalMs,
            Action<ProcessSnapshot> onSnapshot)
        {
            if (_monitoringTasks.ContainsKey(processId))
                return _monitoringTasks[processId];

            _cancelTokens[processId] = false;

            var task = Task.Run(async () =>
            {
                while (!_cancelTokens.GetValueOrDefault(processId, false))
                {
                    try
                    {
                        var snapshot = GetProcessSnapshot(processId);
                        if (snapshot == null)
                            break;

                        onSnapshot?.Invoke(snapshot);
                    }
                    catch
                    {
                        break;
                    }

                    await Task.Delay(samplingIntervalMs);
                }

                _monitoringTasks.Remove(processId);
                _cancelTokens.Remove(processId);
            });

            _monitoringTasks[processId] = task;
            return task;
        }

        public void StopMonitoringProcess(int processId)
        {
            if (_cancelTokens.ContainsKey(processId))
                _cancelTokens[processId] = true;
        }

        public bool IsMonitoring(int processId) => _monitoringTasks.ContainsKey(processId);
    }
}

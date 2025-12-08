using System.Diagnostics;
using ProcessMonitor.Models;
using ProjectMonitor.Models;

namespace ProcessMonitor.Services;

    public class ProcessMonitoringService
    {
        private readonly Dictionary<int, Task> _monitoringTasks = new();
        private readonly Dictionary<int, bool> _cancelTokens = new();
        
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
                    // processess with errors skipped
                }
            }

            return result.OrderBy(p => p.ProcessName).ToList();
        }
        
        public void PopulateDetails(ProcessInfo? info)
        {
            if (info == null) return;

            try
            {
                var proc = Process.GetProcessById(info.ProcessId);
                var threads = new List<ThreadInfo>(proc.Threads.Count);
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

                var modules = new List<ModuleInfo>(proc.Modules.Count);
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
            catch { }
        }


        private DateTime SafeGetStartTime(Process process)
        {
            try { return process.StartTime; }
            catch { return DateTime.MinValue; }
        }

        public void SetProcessPriority(int processId, ProcessPriorityClass priority)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                process.PriorityClass = priority;
            }
            catch { }
        }

        public void KillProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                process.Kill();
            }
            catch { }
        }

        private ProcessSnapshot? GetProcessSnapshot(int processId)
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

        public void StartMonitoringProcess(
            int processId,
            int samplingIntervalMs,
            Action<ProcessSnapshot> onSnapshot)
        {
            if (_monitoringTasks.ContainsKey(processId))
                return;

            _cancelTokens[processId] = false;

            var task = Task.Run(async () =>
            {
                while (!_cancelTokens.GetValueOrDefault(processId, false))
                {
                    ProcessSnapshot? snapshot;

                    try
                    {
                        snapshot = GetProcessSnapshot(processId);
                    }
                    catch
                    {
                        snapshot = null;
                    }
                    
                    onSnapshot?.Invoke(snapshot);

                    if (snapshot == null)
                        break;

                    await Task.Delay(samplingIntervalMs);
                }

                _monitoringTasks.Remove(processId);
                _cancelTokens.Remove(processId);
            });

            _monitoringTasks[processId] = task;
        }

        public void StopMonitoringProcess(int processId)
        {
            if (_cancelTokens.ContainsKey(processId))
                _cancelTokens[processId] = true;
        }
    }


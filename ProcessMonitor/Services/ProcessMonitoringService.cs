using System.Collections.Concurrent;
using System.Diagnostics;
using ProcessMonitor.Models;

namespace ProcessMonitor.Services;

    public class ProcessMonitoringService
    {
        private readonly ConcurrentDictionary<int, Task> _monitoringTasks = new();
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _cts = new();
        
        public List<ProcessInfo> GetAllProcesses()
        {
            var result = new List<ProcessInfo>();

            var processes = Process.GetProcesses();
            
            foreach (var proc in processes)
            {
                using (proc)
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

                        try { info.Priority = proc.BasePriority; }
                        catch { info.Priority = 0; }

                        try { info.ProcessPath = proc.MainModule?.FileName ?? "N/A"; }
                        catch { info.ProcessPath = "N/A"; }

                        result.Add(info);
                    }
                    catch
                    {
                        // suppress errors / skip erroneous processes
                    }
                }
            }

            result.Sort((a, b) => 
                string.Compare(a.ProcessName, b.ProcessName, StringComparison.OrdinalIgnoreCase));
            
            return result;
        }
        
        public void PopulateDetails(ProcessInfo? info)
        {
            if (info == null) return;

            try
            {
                using var proc = Process.GetProcessById(info.ProcessId);
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
                catch
                {
                    // suppress
                }

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
                catch
                {
                    // suppress
                }

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
                // suppress
            }
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
                using var proc = Process.GetProcessById(processId);
                proc.PriorityClass = priority;
            }
            catch
            {
                // suppress
            }
        }

        public void KillProcess(int processId)
        {
            try
            {
                using var proc = Process.GetProcessById(processId);
                proc.Kill();
                proc.WaitForExit(2000);
            }
            catch
            {
                // suppress
            }
        }

        private ProcessSnapshot? GetProcessSnapshot(int processId)
        {
            try
            {
                using var proc = Process.GetProcessById(processId);
                return new ProcessSnapshot
                {
                    ProcessId = processId,
                    ProcessName = proc.ProcessName,
                    MemoryUsage = proc.WorkingSet64,
                    SampleTime = DateTime.UtcNow
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
            Action<ProcessSnapshot?> onSnapshot)
        {
            if (!_monitoringTasks.TryAdd(processId, Task.CompletedTask))
                return;
    
            var cts = new CancellationTokenSource();
    
            if (!_cts.TryAdd(processId, cts))
            {
                _monitoringTasks.TryRemove(processId, out _);
                cts.Dispose();
                return;
            }

            var task = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(samplingIntervalMs));
        
                try
                {
                    while (await timer.WaitForNextTickAsync(cts.Token))
                    {
                        var snapshot = GetProcessSnapshot(processId);

                        try
                        {
                            onSnapshot?.Invoke(snapshot);
                        }
                        catch
                        {
                            // suppress
                        }

                        if (snapshot == null)
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    // expected on cancellation
                }
                finally
                {
                    _monitoringTasks.TryRemove(processId, out _);
                    _cts.TryRemove(processId, out var token);
                    token?.Dispose();
                }
            });

            _monitoringTasks[processId] = task;
        }

        public void StopMonitoringProcess(int processId)
        {
            if (_cts.TryGetValue(processId, out var cts))
            {
                cts.Cancel();
            }
        }
    }


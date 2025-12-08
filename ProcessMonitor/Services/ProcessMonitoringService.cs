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

        public List<ProcessInfo> GetAllProcesses()
        {
            var result = new List<ProcessInfo>();

            try
            {
                foreach (var proc in Process.GetProcesses())
                {
                    var info = ConvertToProcessInfo(proc);
                    if (info != null)
                        result.Add(info);
                }
            }
            catch
            {
                // Ignoruj globalne błędy, ale rezultat i tak będzie tym co się udało zebrać
            }

            return result.OrderBy(p => p.ProcessName).ToList();
        }

        private ProcessInfo ConvertToProcessInfo(Process process)
        {
            try
            {
                var info = new ProcessInfo
                {
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName,
                    ThreadCount = process.Threads.Count,
                    WorkingSet = process.WorkingSet64,
                    StartTime = SafeGetStartTime(process)
                };

                // Priorytet może rzucić wyjątek przy braku uprawnień
                try
                {
                    info.Priority = (int)process.PriorityClass;
                }
                catch
                {
                    info.Priority = 0;
                }

                // Ścieżka
                try
                {
                    info.ProcessPath = process.MainModule?.FileName ?? "N/A";
                }
                catch
                {
                    info.ProcessPath = "N/A";
                }

                // Wątki
                try
                {
                    foreach (ProcessThread thread in process.Threads)
                    {
                        info.Threads.Add(new ThreadInfo
                        {
                            ThreadId = thread.Id,
                            ThreadState = thread.ThreadState.ToString(),
                            Priority = thread.CurrentPriority
                        });
                    }
                }
                catch
                {
                    // brak uprawnień do wątków – pomijamy
                }

                // Moduły
                try
                {
                    foreach (ProcessModule module in process.Modules)
                    {
                        info.Modules.Add(new ModuleInfo
                        {
                            ModuleName = module.ModuleName,
                            ModuleFileName = module.FileName,
                            ModuleMemorySize = module.ModuleMemorySize
                        });
                    }
                }
                catch
                {
                    // brak uprawnień – pomijamy
                }

                return info;
            }
            catch
            {
                // Jakikolwiek wyjątek – zwróć null, ale nie wywalaj całej listy
                return null;
            }
        }

        private DateTime SafeGetStartTime(Process process)
        {
            try
            {
                return process.StartTime;
            }
            catch
            {
                return DateTime.MinValue;
            }
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

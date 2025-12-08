using System.Collections.ObjectModel;
using ProjectMonitor.Models;

namespace ProcessMonitor.Models
{
    public class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ProcessPath { get; set; } = string.Empty;
        public int ThreadCount { get; set; }
        public long WorkingSet { get; set; } // bytes
        
        public double WorkingSetMb => WorkingSet / 1024.0 / 1024.0;
        public int Priority { get; set; }
        public DateTime StartTime { get; set; }

        // Szczegóły ładowane na żądanie (dla master/detail)
        public ObservableCollection<ThreadInfo> Threads { get; } = new();
        public ObservableCollection<ModuleInfo> Modules { get; } = new();
    }
}
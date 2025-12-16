namespace ProcessMonitor.Models;

public class ProcessSnapshot
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public long MemoryUsage { get; set; } // bytes
    public DateTime SampleTime { get; set; }
}
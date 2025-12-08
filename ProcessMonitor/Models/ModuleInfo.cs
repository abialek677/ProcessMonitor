namespace ProcessMonitor.Models;

public class ModuleInfo
{
    public string ModuleName { get; set; } = string.Empty;
    public string ModuleFileName { get; set; } = string.Empty;
    public long ModuleMemorySize { get; set; }
}
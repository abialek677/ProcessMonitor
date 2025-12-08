using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProcessMonitor.Models;

public class MonitoredProcess : INotifyPropertyChanged
    {
        private int _processId;
        private string _processName;
        private DateTime _monitoringStartTime;
        private DateTime? _monitoringEndTime;
        private long _maxMemoryUsage;
        private long _averageMemoryUsage;
        private int _sampleCount;
        private bool _isMonitoring;
        private long _totalMemoryAccumulated;

        public int ProcessId
        {
            get => _processId;
            set => Set(ref _processId, value);
        }

        public string ProcessName
        {
            get => _processName;
            set => Set(ref _processName, value);
        }

        public DateTime MonitoringStartTime
        {
            get => _monitoringStartTime;
            set => Set(ref _monitoringStartTime, value);
        }

        public DateTime? MonitoringEndTime
        {
            get => _monitoringEndTime;
            set => Set(ref _monitoringEndTime, value);
        }

        public long MaxMemoryUsage
        {
            get => _maxMemoryUsage;
            set => Set(ref _maxMemoryUsage, value);
        }

        public long AverageMemoryUsage
        {
            get => _averageMemoryUsage;
            set => Set(ref _averageMemoryUsage, value);
        }

        public int SampleCount
        {
            get => _sampleCount;
            set => Set(ref _sampleCount, value);
        }

        public bool IsMonitoring
        {
            get => _isMonitoring;
            set => Set(ref _isMonitoring, value);
        }

        public long TotalMemoryAccumulated
        {
            get => _totalMemoryAccumulated;
            set => Set(ref _totalMemoryAccumulated, value);
        }
        
        private string _durationText;
        public string DurationText
        {
            get => _durationText;
            set => Set(ref _durationText, value);
        }



        public string FormattedMemory(long bytes) => $"{bytes / 1024 / 1024} MB";

        public override string ToString() => $"{ProcessName} (PID: {ProcessId})";

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
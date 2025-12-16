using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ProcessMonitor.Models;

namespace ProcessMonitor
{
    public partial class App : Application
    {
    }
    
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value == null ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? new SolidColorBrush(Color.FromRgb(16, 124, 16))
                    : new SolidColorBrush(Color.FromRgb(209, 52, 56));

            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
    
    public class MonitoringDurationConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not MonitoredProcess proc) return "00:00:00";
            
            var end = proc.MonitoringEndTime ?? DateTime.Now;
            var duration = end - proc.MonitoringStartTime;
            return $"{duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

}
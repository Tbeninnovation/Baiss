using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Baiss.UI.Converters
{
    public class ServerStatusTextConverter : IValueConverter
    {
        public static readonly ServerStatusTextConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isOnline)
            {
                return isOnline ? "Ready" : "Starting...";
            }
            return "Server Status Unknown";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

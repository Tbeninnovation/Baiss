using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Baiss.UI.Converters
{
    public class ServerStatusTooltipConverter : IValueConverter
    {
        public static readonly ServerStatusTooltipConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isOnline)
            {
                return isOnline ? "Server Online" : "Server Offline";
            }
            return "Unknown Status";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

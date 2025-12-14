using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Baiss.UI.ViewModels
{
    public class MessageWidthConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double width)
                return width * 0.7;
            return 400.0; // fallback
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 

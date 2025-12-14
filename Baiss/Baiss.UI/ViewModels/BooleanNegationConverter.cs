using System;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Baiss.UI.ViewModels
{
    public class BooleanNegationConverter : IValueConverter
    {
        public static readonly BooleanNegationConverter Instance = new BooleanNegationConverter();
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return value;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return value;
        }
    }
} 
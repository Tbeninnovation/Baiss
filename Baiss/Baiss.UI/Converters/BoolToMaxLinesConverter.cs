using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Baiss.UI.Converters;

public class BoolToMaxLinesConverter : IValueConverter
{
    public int CollapsedLines { get; set; } = 2;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool expanded && expanded)
        {
            return 0; // Unlimited
        }
        return CollapsedLines;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Baiss.UI.ViewModels;

public class ZeroToBoolConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isZero = value switch
        {
            int i => i == 0,
            long l => l == 0,
            double d => Math.Abs(d) < double.Epsilon,
            _ => value == null
        };

        return Invert ? !isZero : isZero;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

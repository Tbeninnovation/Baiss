using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Baiss.UI.Converters;

public sealed class EqualityMultiValueConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
        {
            return false;
        }

        var left = values[0];
        var right = values[1];
        var areEqual = Equals(left, right);

        if (parameter is string s && string.Equals(s, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            return !areEqual;
        }

        return areEqual;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

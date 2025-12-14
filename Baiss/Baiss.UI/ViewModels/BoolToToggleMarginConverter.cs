using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Baiss.UI.ViewModels;

public class BoolToToggleMarginConverter : IMultiValueConverter
{
    public object Convert(System.Collections.Generic.IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count > 0 && values[0] is bool isChecked && isChecked)
        {
            return new Thickness(16, 0, 0, 0); // Right position when checked
        }
        return new Thickness(2, 0, 0, 0); // Left position when unchecked
    }
}

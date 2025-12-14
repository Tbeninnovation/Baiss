using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace Baiss.UI.ViewModels;

public class BoolToToggleAlignmentConverter : IMultiValueConverter
{
    public object Convert(System.Collections.Generic.IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count > 0 && values[0] is bool isChecked && isChecked)
        {
            return HorizontalAlignment.Right; // Right position when checked
        }
        return HorizontalAlignment.Left; // Left position when unchecked
    }
}

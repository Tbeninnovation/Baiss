using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Baiss.UI.ViewModels;

public class BoolToToggleColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked)
        {
            return Color.FromRgb(59, 130, 246); // Blue when checked
        }
        return Color.FromRgb(55, 65, 81); // Gray when unchecked (#374151)
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

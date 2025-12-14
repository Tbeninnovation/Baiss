using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Baiss.UI.ViewModels;

public class NavigationBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            return new SolidColorBrush(Color.FromArgb(255, 59, 130, 246)); // Match primary action buttons
        }
        return new SolidColorBrush(Color.FromArgb(102, 19, 19, 19)); // Match history container background
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

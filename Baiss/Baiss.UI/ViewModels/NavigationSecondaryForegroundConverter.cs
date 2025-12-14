using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Baiss.UI.ViewModels;

public class NavigationSecondaryForegroundConverter : IValueConverter
{
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromArgb(255, 0x8C, 0x96, 0xAF));
    private static readonly SolidColorBrush SelectedBrush = new(Colors.White);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            return SelectedBrush;
        }

        return DefaultBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

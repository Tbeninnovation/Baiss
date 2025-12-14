using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Baiss.UI.ViewModels
{
    public class ExpandedToArrowConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
                return isExpanded ? "/Assets/chevron-up-white.svg" : "/Assets/chevron-down-white.svg";
            return "/Assets/chevron-down-white.svg";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

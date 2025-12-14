using Avalonia.Data.Converters;
using Avalonia.Layout;
using System;
using System.Globalization;

namespace Baiss.UI.ViewModels
{
    public class MessageAlignmentConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isMine)
                return isMine ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
            return HorizontalAlignment.Left;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 

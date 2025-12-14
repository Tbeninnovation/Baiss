using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Baiss.UI.ViewModels
{
    public class MessageBackgroundConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isMine = value is bool b && b;
            return isMine
                ? new SolidColorBrush(Color.FromArgb(255, 0, 20, 60)) // Very dark blue - User messages
                : new SolidColorBrush(Colors.Transparent); // Transparent - AI messages
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

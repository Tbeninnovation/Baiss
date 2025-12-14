using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Baiss.UI.ViewModels
{
    public class MessageMaxWidthConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            // values[0] should be the window width (double)
            // values[1] should be IsMine (bool)
            
            if (values.Count >= 2 && values[0] is double width && values[1] is bool isMine)
            {
                // User messages: 50% of window width
                // AI messages: 95% of window width
                return isMine ? width * 0.5 : width * 0.95;
            }
            
            return 400.0; // fallback
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

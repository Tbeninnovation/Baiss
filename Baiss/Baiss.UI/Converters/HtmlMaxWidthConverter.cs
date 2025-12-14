using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Baiss.UI.Converters
{
    public class HtmlMaxWidthConverter : IMultiValueConverter
    {
        public static readonly HtmlMaxWidthConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            // values[0] should be the window width (double)
            
            if (values.Count >= 2 && values[0] is double width && width > 0)
            {
                var isMine = values[1] is bool b && b;
                // Return 45% of window width for user messages (to account for padding), 90% for AI
                return isMine ? width * 0.45 : width * 0.9;
            }
            
            if (values.Count >= 1 && values[0] is double w && w > 0)
            {
                return w * 0.9;
            }
            
            return 1200.0; // fallback
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

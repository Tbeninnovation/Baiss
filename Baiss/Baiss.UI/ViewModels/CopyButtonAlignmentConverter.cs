using Avalonia.Data.Converters;
using Avalonia.Layout;
using System;
using System.Globalization;

namespace Baiss.UI.ViewModels
{
    /// <summary>
    /// Converter for copy button alignment - opposite of message alignment.
    /// User messages (IsMine=true) -> Right, AI messages (IsMine=false) -> Left
    /// </summary>
    public class CopyButtonAlignmentConverter : IValueConverter
    {
        public static readonly CopyButtonAlignmentConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isMine)
                return isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            return HorizontalAlignment.Left;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 

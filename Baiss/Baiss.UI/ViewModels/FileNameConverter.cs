using Avalonia.Data.Converters;
using Baiss.Application.DTOs;
using Baiss.UI.Models;
using System;
using System.Globalization;

namespace Baiss.UI.ViewModels
{
    public class FileNameConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is SourceItem sourceItem)
                return sourceItem.GetDisplayFileName();
            return "Unknown File";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

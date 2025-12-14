using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;

namespace Baiss.UI.Converters
{
    public class FileIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string fileName)
            {
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                
                return extension switch
                {
                    ".pdf" => "/Assets/file-text.svg",
                    ".txt" => "/Assets/file-text.svg",
                    ".md" => "/Assets/file-text.svg",
                    ".docx" => "/Assets/file-text.svg",
                    ".csv" => "/Assets/excel.svg",
                    ".xlsx" => "/Assets/excel.svg",
                    ".png" => "/Assets/image.svg",
                    ".jpg" => "/Assets/image.svg",
                    ".jpeg" => "/Assets/image.svg",
                    ".gif" => "/Assets/image.svg",
                    ".bmp" => "/Assets/image.svg",
                    ".svg" => "/Assets/image.svg",
                    _ => "/Assets/file-text.svg" // Default icon for unknown file types
                };
            }
            
            return "/Assets/file-text.svg"; // Default fallback
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
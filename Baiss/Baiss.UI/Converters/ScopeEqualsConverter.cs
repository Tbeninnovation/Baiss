using System;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Baiss.UI.Converters;

/// <summary>
/// Binds a string scope property (AIModelProviderScope) to a RadioButton IsChecked.
/// ConverterParameter must be the scope value (e.g. "local", "hosted", "databricks").
/// When converting back, only a true value updates the scope; false returns Binding.DoNothing.
/// </summary>
public class ScopeEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is null) return false;
        var scopeValue = value as string;
        var param = parameter.ToString();
        return string.Equals(scopeValue, param, StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is null) return null;
        if (value is bool b && b)
        {
            return parameter.ToString();
        }
        // Do not change underlying value on uncheck (let another RadioButton's true set it)
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}

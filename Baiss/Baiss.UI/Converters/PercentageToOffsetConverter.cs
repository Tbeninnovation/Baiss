using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Baiss.UI.Converters;

public class PercentageToOffsetConverter : IValueConverter
{
	public static readonly PercentageToOffsetConverter Instance = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return 0d;
		}

		if (value is IConvertible convertible)
		{
			try
			{
				var percentage = convertible.ToDouble(CultureInfo.InvariantCulture);
				var normalized = Math.Clamp(percentage / 100d, 0d, 1d);
				return normalized;
			}
			catch
			{
				return 0d;
			}
		}

		return 0d;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return 0d;
		}

		if (value is IConvertible convertible)
		{
			try
			{
				var offset = convertible.ToDouble(CultureInfo.InvariantCulture);
				var percentage = Math.Clamp(offset, 0d, 1d) * 100d;
				return percentage;
			}
			catch
			{
				return 0d;
			}
		}

		return 0d;
	}
}

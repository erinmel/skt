using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace skt.IDE.Services.Converters;

public sealed class BoolInvertConverter : IValueConverter
{
    public static BoolInvertConverter Instance { get; } = new();
    private BoolInvertConverter() {}

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Convert(value, targetType, parameter, culture);
    }
}


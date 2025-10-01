using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace skt.IDE.Services.Converters;

public class IconKeyToResourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resources = Avalonia.Application.Current?.Resources;
        if (value is string iconKey && !string.IsNullOrEmpty(iconKey) &&
            (resources?.TryGetResource(iconKey, null, out var resource) ?? false))
        {
            return resource as DrawingImage;
        }

        if (resources?.TryGetResource("Icon.Document", null, out var fallback) ?? false)
        {
            return fallback as DrawingImage;
        }

        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

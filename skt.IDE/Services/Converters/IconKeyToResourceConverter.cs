using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace skt.IDE.Converters;

public class IconKeyToResourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string iconKey && !string.IsNullOrEmpty(iconKey))
        {
            // Try to find the resource in the application resources
            if (Avalonia.Application.Current?.Resources.TryGetResource(iconKey, null, out var resource) == true)
            {
                return resource as DrawingImage;
            }
        }

        // Fallback to default document icon
        if (Avalonia.Application.Current?.Resources.TryGetResource("Icon.Document", null, out var fallback) == true)
        {
            return fallback as DrawingImage;
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace skt.IDE.Views;

public partial class SktIcon : UserControl
{
    public static readonly StyledProperty<string> IconKeyProperty =
        AvaloniaProperty.Register<SktIcon, string>(nameof(IconKey));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<SktIcon, double>(nameof(IconSize), 16.0);

    public static readonly StyledProperty<double> IconWidthProperty =
        AvaloniaProperty.Register<SktIcon, double>(nameof(IconWidth), double.NaN);

    public static readonly StyledProperty<double> IconHeightProperty =
        AvaloniaProperty.Register<SktIcon, double>(nameof(IconHeight), double.NaN);

    public static readonly StyledProperty<IBrush?> TintBrushProperty =
        AvaloniaProperty.Register<SktIcon, IBrush?>(nameof(TintBrush));

    public string IconKey
    {
        get => GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public double IconWidth
    {
        get => GetValue(IconWidthProperty);
        set => SetValue(IconWidthProperty, value);
    }

    public double IconHeight
    {
        get => GetValue(IconHeightProperty);
        set => SetValue(IconHeightProperty, value);
    }

    public IBrush? TintBrush
    {
        get => GetValue(TintBrushProperty);
        set => SetValue(TintBrushProperty, value);
    }

    public SktIcon()
    {
        InitializeComponent();
        UpdateIcon();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconKeyProperty ||
            change.Property == IconSizeProperty ||
            change.Property == IconWidthProperty ||
            change.Property == IconHeightProperty ||
            change.Property == TintBrushProperty)
        {
            UpdateIcon();
        }
    }

    private void UpdateIcon()
    {
        if (IconImage == null) return;

        if (!string.IsNullOrEmpty(IconKey))
        {
            var resource = Application.Current?.FindResource(IconKey);
            if (resource is DrawingImage drawingImage)
            {
                IconImage.Source = drawingImage;
            }
        }
        else
        {
            IconImage.Source = null;
        }

        IconImage.Width = !double.IsNaN(IconWidth) ? IconWidth : IconSize;

        IconImage.Height = !double.IsNaN(IconHeight) ? IconHeight : IconSize;

        if (TintBrush != null)
        {
            IconImage.OpacityMask = new ImageBrush
            {
                Source = (IImageBrushSource?)IconImage.Source,
                Stretch = Stretch.Uniform
            };
            Background = TintBrush;
        }
        else
        {
            IconImage.OpacityMask = null;
            Background = null;
        }
    }
}

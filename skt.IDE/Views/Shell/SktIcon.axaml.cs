using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using skt.IDE.Services;

namespace skt.IDE.Views.Shell;

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

    public static readonly StyledProperty<bool> UseAppFontSizeProperty =
        AvaloniaProperty.Register<SktIcon, bool>(nameof(UseAppFontSize), true);

    public static readonly StyledProperty<double> IconScaleProperty =
        AvaloniaProperty.Register<SktIcon, double>(nameof(IconScale), 1.0);

    public string IconKey
    {
        get => GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    public bool UseAppFontSize
    {
        get => GetValue(UseAppFontSizeProperty);
        set => SetValue(UseAppFontSizeProperty, value);
    }

    public double IconScale
    {
        get => GetValue(IconScaleProperty);
        set => SetValue(IconScaleProperty, value);
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

        // Subscribe to theme changes so we can reload the icon DrawingImage which may reference theme brushes.
        ThemeManager.ThemeApplied += OnThemeApplied;
    }

    protected override void OnDetachedFromLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        // Unsubscribe to avoid memory leaks
        ThemeManager.ThemeApplied -= OnThemeApplied;
    }

    private void OnThemeApplied(AppThemeVariant obj)
    {
        // Theme changed; re-run UpdateIcon on UI thread
        Dispatcher.UIThread.Post(() => UpdateIcon());
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
            // Try to resolve the resource; if it's a DrawingImage, assign it. If resource lookup fails
            // (because resources were reloaded), try to force a re-query by removing and re-adding merged dictionaries is not ideal here.
            // Instead, we look up the resource each time UpdateIcon is called so we pick up the new DrawingImage instance.
            var resource = Application.Current?.FindResource(IconKey);
            if (resource is DrawingImage drawingImage)
            {
                IconImage.Source = drawingImage;
            }
            else
            {
                // If not found, clear source to avoid showing stale image
                IconImage.Source = null;
            }
        }
        else
        {
            IconImage.Source = null;
        }

        // Determine final size. If UseAppFontSize is enabled and an AppFontSize resource exists, use it.
        double finalSize = IconSize;
        if (UseAppFontSize && Application.Current?.Resources != null && Application.Current.Resources.TryGetValue("AppFontSize", out var fontSizeObj) && fontSizeObj is double appFontSize)
        {
            finalSize = appFontSize * IconScale;
        }

        IconImage.Width = !double.IsNaN(IconWidth) ? IconWidth : finalSize;
        IconImage.Height = !double.IsNaN(IconHeight) ? IconHeight : finalSize;

        if (TintBrush != null)
        {
            IconImage.OpacityMask = new ImageBrush
            {
                Source = IconImage.Source as IImageBrushSource,
                Stretch = Stretch.Uniform
            };
            Background = TintBrush;
        }
        else
        {
            IconImage.OpacityMask = null;
            Background = Avalonia.Media.Brushes.Transparent;
        }
    }
}

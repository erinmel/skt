using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Svg.Skia;
using skt.IDE.ViewModels;
using skt.IDE.Views.Shell;
using skt.IDE.Services;
using CommunityToolkit.Mvvm.Messaging;

namespace skt.IDE;

public class App : Application
{
    public static IMessenger Messenger { get; } = WeakReferenceMessenger.Default;

    // Service container for dependency injection
    public static IServiceProvider? Services { get; private set; }
    private DocumentStateManager? _documentStateManager;

    public override void Initialize()
    {
        // Initialize SVG support
        GC.KeepAlive(typeof(SvgImageExtension).Assembly);
        GC.KeepAlive(typeof(Avalonia.Svg.Skia.Svg).Assembly);

        AvaloniaXamlLoader.Load(this);

        // Initialize services
        InitializeServices();

        // Load and apply saved user settings (theme, fonts, etc.)
        var settings = SettingsManager.Load();
        SettingsManager.ApplySettings(settings);
    }

    private void InitializeServices()
    {
        // Create service collection
        var services = new Dictionary<Type, object>();

        // Register ActiveEditorService as singleton
        var activeEditorService = new ActiveEditorService();
        services.Add(typeof(ActiveEditorService), activeEditorService);

        // Register DocumentStateManager as singleton
        _documentStateManager = new DocumentStateManager(Messenger);
        services.Add(typeof(DocumentStateManager), _documentStateManager);

        // Create simple service provider
        Services = new SimpleServiceProvider(services);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

// Simple service provider implementation
internal class SimpleServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, object> _services;

    public SimpleServiceProvider(Dictionary<Type, object> services)
    {
        _services = services;
    }

    public object? GetService(Type serviceType)
    {
        return _services.TryGetValue(serviceType, out var service) ? service : null;
    }
}

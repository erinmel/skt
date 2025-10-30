using System;
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
    private CompilerBridge? _compilerBridge; // keep reference

    public override void Initialize()
    {
        // Initialize SVG support
        GC.KeepAlive(typeof(SvgImageExtension).Assembly);
        GC.KeepAlive(typeof(Avalonia.Svg.Skia.Svg).Assembly);

        AvaloniaXamlLoader.Load(this);
        _compilerBridge ??= new CompilerBridge(Messenger);
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

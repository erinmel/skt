using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace skt.IDE.Views.ToolWindows;

public partial class TokensView : UserControl
{
    public TokensView()
    {
        // Load the XAML at runtime. This avoids relying on generated InitializeComponent
        // which may not be present for newly added views until full build completes.
        AvaloniaXamlLoader.Load(this);
    }
}

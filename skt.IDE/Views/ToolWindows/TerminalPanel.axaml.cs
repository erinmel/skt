using Avalonia.Controls;

namespace skt.IDE.Views.ToolWindows;

public partial class TerminalPanel : UserControl
{
    public TerminalPanel()
    {
        InitializeComponent();
    }

    public void SetSelectedTab(int index)
    {
        TerminalTabView.SelectedIndex = index;
    }
}


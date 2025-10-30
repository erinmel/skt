using skt.IDE.ViewModels;

namespace skt.IDE.Views.Editor;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;

public partial class TabbedEditor : UserControl
{
    public TabbedEditor()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public TabbedEditorViewModel? ViewModel => DataContext as TabbedEditorViewModel;

    // Method to load a file from external calls
    public async Task OpenFileAsync(string filePath)
    {
        if (ViewModel != null)
        {
            await ViewModel.OpenFileAsync(filePath);
        }
    }

    public void NewTab()
    {
        ViewModel?.NewTabCommand.Execute(null);
    }

    public void SaveCurrentFile()
    {
        ViewModel?.SaveCommand.Execute(null);
    }
}
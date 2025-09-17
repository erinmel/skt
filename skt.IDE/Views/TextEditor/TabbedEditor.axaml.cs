using skt.IDE.ViewModels;

namespace skt.IDE.Views.TextEditor;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;

public partial class TabbedEditor : UserControl
{
    public TabbedEditor()
    {
        InitializeComponent();

        // Set default DataContext if none is provided
        DataContext ??= new TabbedEditorViewModel();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // Expose the ViewModel as a property for easier access
    public TabbedEditorViewModel? ViewModel => DataContext as TabbedEditorViewModel;

    // Method to load a file from external calls
    public async Task OpenFileAsync(string filePath)
    {
        if (ViewModel != null)
        {
            await ViewModel.OpenFileAsync(filePath);
        }
    }

    // Method to create a new tab from external calls
    public void NewTab()
    {
        ViewModel?.NewTabCommand.Execute(null);
    }

    // Method to save current file from external calls
    public void SaveCurrentFile()
    {
        ViewModel?.SaveCommand.Execute(null);
    }
}
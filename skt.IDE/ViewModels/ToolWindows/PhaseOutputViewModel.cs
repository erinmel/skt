using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace skt.IDE.ViewModels.ToolWindows;

public partial class PhaseOutputViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<string> _outputLines = new();

    [ObservableProperty]
    private string _currentPhase = "Ready";

    public PhaseOutputViewModel()
    {
        OutputLines.Add("Phase Output Window Ready");
    }

    public void AddOutput(string line)
    {
        OutputLines.Add(line);
    }

    public void ClearOutput()
    {
        OutputLines.Clear();
    }

    public void SetPhase(string phase)
    {
        CurrentPhase = phase;
        AddOutput($"--- {phase} Phase Started ---");
    }
}

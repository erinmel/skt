using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using skt.IDE.Services.Buss;
using skt.Shared;

namespace skt.IDE.ViewModels.ToolWindows;

public class SymbolRow
{
    public string Name { get; }
    public string DataType { get; }
    public string Scope { get; }
    public string IsUsed { get; }
    public string Lines { get; }

    public SymbolRow(string name, string dataType, string scope, bool isUsed, List<int> lines)
    {
        Name = name;
        DataType = dataType;
        Scope = scope;
        IsUsed = isUsed ? "Si" : "No";
        Lines = string.Join(", ", lines);
    }
}

public partial class SymbolTableViewModel : ObservableObject
{
    private readonly ObservableCollection<SymbolRow> _rows = new();

    [ObservableProperty]
    private FlatTreeDataGridSource<SymbolRow> _source = null!;

    [ObservableProperty]
    private int _symbolCount;

    public SymbolTableViewModel()
    {
        _source = CreateSource(_rows);

        App.EventBus.Subscribe<SemanticAnalysisCompletedEvent>(OnSemanticCompleted);
        App.EventBus.Subscribe<FileClosedEvent>(OnFileClosed);
    }

    private FlatTreeDataGridSource<SymbolRow> CreateSource(IList<SymbolRow> items) => new(items)
    {
        Columns =
        {
            new TextColumn<SymbolRow, string>("Nombre", x => x.Name),
            new TextColumn<SymbolRow, string>("Tipo", x => x.DataType),
            new TextColumn<SymbolRow, string>("Ámbito", x => x.Scope),
            new TextColumn<SymbolRow, string>("Usada", x => x.IsUsed),
            new TextColumn<SymbolRow, string>("Linea", x => x.Lines)
        }
    };

    private void OnSemanticCompleted(SemanticAnalysisCompletedEvent e)
    {
        var entries = e.SymbolTable.Entries;

        Dispatcher.UIThread.Post(() =>
        {
            _rows.Clear();

            foreach (var entry in entries)
            {
                var allLines = new List<int> { entry.DeclarationLine };
                allLines.AddRange(entry.References.Select(r => r.Line));

                var isUsed = entry.References.Count > 0;

                _rows.Add(new SymbolRow(entry.Name, entry.DataType, entry.Scope, isUsed, allLines));
            }

            SymbolCount = entries.Count;
        });
    }

    private void OnFileClosed(FileClosedEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _rows.Clear();
            SymbolCount = 0;
        });
    }
}

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using skt.IDE.Services.Buss;
using skt.Shared;
using CommunityToolkit.Mvvm.Messaging;
using skt.IDE.Models;

namespace skt.IDE.ViewModels.ToolWindows;

public partial class PCodeViewModel : ObservableObject, IDisposable
{
    private readonly ObservableCollection<PCodeRow> _rows = new();
    private readonly Services.ActiveEditorService? _activeEditorService;

    [ObservableProperty]
    private FlatTreeDataGridSource<PCodeRow> _source;

    [ObservableProperty]
    private string _currentFile = "";

    [ObservableProperty]
    private int _instructionCount;

    [ObservableProperty]
    private int _dataSize;

    [ObservableProperty]
    private int _stringCount;

    private PCodeProgram? _currentProgram;

    public PCodeViewModel()
    {
        _source = CreateSource(_rows);
        _activeEditorService = App.Services?.GetService(typeof(Services.ActiveEditorService)) as Services.ActiveEditorService;

        App.Messenger.Register<ActiveEditorChangedEvent>(this, (_, m) => OnActiveEditorChanged(m));
        App.Messenger.Register<PCodeGenerationCompletedEvent>(this, (_, m) => OnPCodeCompleted(m));
        App.Messenger.Register<FileClosedEvent>(this, (_, m) => OnFileClosed(m));
    }

    private FlatTreeDataGridSource<PCodeRow> CreateSource(IList<PCodeRow> items) => new(items)
    {
        Columns =
        {
            new TextColumn<PCodeRow, string>("Address", x => x.Address, width: new GridLength(70)),
            new TextColumn<PCodeRow, string>("Operation", x => x.Operation, width: new GridLength(100)),
            new TextColumn<PCodeRow, string>("Operand", x => x.Operand, width: new GridLength(80)),
            new TextColumn<PCodeRow, string>("Comment", x => x.Comment)
        }
    };

    private void OnActiveEditorChanged(ActiveEditorChangedEvent e)
    {
        if (e.ActiveEditor == null)
        {
            CurrentFile = string.Empty;
            Clear();
            return;
        }

        CurrentFile = e.ActiveEditor.FilePath ?? string.Empty;
        
        // Load P-code if available
        if (e.ActiveEditor.PCodeProgram != null)
        {
            LoadPCode(e.ActiveEditor.PCodeProgram);
        }
        else
        {
            Clear();
        }
    }

    private void OnPCodeCompleted(PCodeGenerationCompletedEvent e)
    {
        // Only update if this is for the currently active file
        if (!string.Equals(CurrentFile, e.FilePath, StringComparison.OrdinalIgnoreCase))
            return;

        LoadPCode(e.Program);
    }

    private void OnFileClosed(FileClosedEvent e)
    {
        if (!string.Equals(CurrentFile, e.FilePath, StringComparison.OrdinalIgnoreCase)) return;
        CurrentFile = string.Empty;
        Clear();
    }

    private void LoadPCode(PCodeProgram program)
    {
        _currentProgram = program;

        Dispatcher.UIThread.Post(() =>
        {
            _rows.Clear();

            for (int i = 0; i < program.Instructions.Count; i++)
            {
                var instruction = program.Instructions[i];
                _rows.Add(new PCodeRow(
                    i,
                    instruction.Op.ToString(),
                    instruction.Operand,
                    instruction.Comment
                ));
            }

            InstructionCount = program.Instructions.Count;
            DataSize = program.DataSize;
            StringCount = program.StringTable.Count;
        });
    }

    private void Clear()
    {
        _currentProgram = null;
        Dispatcher.UIThread.Post(() =>
        {
            _rows.Clear();
            InstructionCount = 0;
            DataSize = 0;
            StringCount = 0;
        });
    }

    public PCodeProgram? GetCurrentProgram() => _currentProgram;

    public void Dispose()
    {
        App.Messenger.UnregisterAll(this);
    }
}


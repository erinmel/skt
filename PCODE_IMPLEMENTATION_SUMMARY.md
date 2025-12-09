# P-Code View and Execution Implementation Summary

## Overview
This implementation adds a P-Code view controller similar to the Tokens view, toolbar controls for P-code generation and execution, and integrates P-code generation into the live compilation pipeline.

## Features Implemented

### 1. P-Code View Components
- **PCodeRow Model** (`Models/PCodeRow.cs`): Model for displaying P-code instructions in the grid
  - Properties: Address, Operation, Level, Operand, Comment

- **PCodeViewModel** (`ViewModels/ToolWindows/PCodeViewModel.cs`): ViewModel for P-code display
  - Displays P-code instructions in a TreeDataGrid
  - Shows instruction count, data size, and string count
  - Subscribes to `PCodeGenerationCompletedEvent` and `ActiveEditorChangedEvent`
  - Stores current PCodeProgram for execution

- **PCodeView** (`Views/ToolWindows/CompilerOutput/PCodeView.axaml[.cs]`): View for P-code display
  - TreeDataGrid with columns: Addr, Operation, Level, Operand, Comment
  - Integrated into MainWindow tool windows

### 2. Event Bus Extensions
Added to `Services/Buss/CompilerEvents.cs`:
- **PCodeGenerationRequestEvent**: Request P-code generation from annotated AST
- **PCodeGenerationCompletedEvent**: Fired when P-code generation succeeds
- **PCodeGenerationFailedEvent**: Fired when P-code generation fails
- **PCodeExecutionRequestEvent**: Request P-code execution
- **PCodeExecutionOutputEvent**: Fired for each output line during execution
- **PCodeExecutionCompletedEvent**: Fired when execution completes

Added to `Services/Buss/FileEvents.cs`:
- **ClearTerminalRequestEvent**: Request to clear terminal output

### 3. Compiler Bridge Updates
`Services/CompilerBridge.cs`:
- Added P-code generation after successful semantic analysis in live compilation
- Added `OnPCodeGenerationRequest` handler
- Added `OnPCodeExecutionRequest` handler with output/error event subscription
- Integrated `PCodeGenerator` and `PCodeInterpreter`

### 4. Document State Manager Updates
`Services/DocumentStateManager.cs`:
- Added P-code generation to the live compilation pipeline
- P-code is generated automatically after successful semantic analysis
- Fires `PCodeGenerationCompletedEvent` on UI thread

### 5. Text Editor ViewModel Updates
`ViewModels/TextEditorViewModel.cs`:
- Added `PCodeProgram` property to store generated P-code
- Added `HasPCodeGeneration` property
- Added `UpdatePCodeGeneration()` method
- P-code is cleared when analysis results are cleared

### 6. Active Editor Service Updates
`Services/ActiveEditorService.cs`:
- Subscribes to `PCodeGenerationCompletedEvent`
- Updates editor's PCodeProgram when generation completes

### 7. Main Window Updates
`Views/Shell/MainWindow.axaml[.cs]`:
- Added `PCode` to `ToolWindowType` enum
- Added PCode to tool window titles and button mappings
- Added PCodeView to dynamic content grid
- PCode tool window can be selected and switched to

### 8. Tool Window Strip Updates
`Views/ToolWindows/Chrome/ToolWindowStrip.axaml`:
- Added `PCodeToggle` button between Tokens and Syntax Tree
- Uses `Icon.PCode` icon
- Tooltip: "P-Code"

### 9. Toolbar Updates
`Views/ToolWindows/Chrome/ToolBar.axaml[.cs]`:
- Added "Generate P-Code" menu item in Compiler dropdown
  - Enabled after successful semantic analysis
  - Opens P-Code tool window when clicked
- Added "Execute P-Code" toolbar button
  - Enabled after successful P-code generation
  - Uses `Icon.ToolbarExecute` icon
- Event handlers:
  - `GeneratePCodeMenuItem_Click`: Generates P-code and shows P-code view
  - `ExecutePCodeButton_Click`: Executes P-code and displays output in terminal
- Subscriptions:
  - `SemanticAnalysisCompletedEvent`: Enables P-code generation
  - `PCodeGenerationCompletedEvent`: Enables execution button

### 10. Terminal Panel Updates
`Views/ToolWindows/Terminal/TerminalPanel.axaml[.cs]`:
- Added named controls: `TerminalTextBox` and `TerminalScrollViewer`
- Subscribes to `PCodeExecutionOutputEvent`
- Subscribes to `ClearTerminalRequestEvent`
- Event handlers:
  - `OnExecutionOutput`: Appends output to terminal and scrolls to end
  - `OnClearTerminal`: Clears terminal text

### 11. P-Code Interpreter Updates
`skt.Compiler/PCodeInterpreter.cs`:
- Added `OnOutput` event for output text
- Added `OnError` event for error messages
- Added parameterless constructor for event-based usage
- Added `Execute(PCodeProgram, string[]?)` overload
- Updated WRT, WRS, WRL operations to invoke `OnOutput` event
- Updated error handling to invoke `OnError` event
- Refactored to support executing different PCodeProgram instances

### 12. Main Window ViewModel Updates
`ViewModels/MainWindowViewModel.cs`:
- Added `PCode` property of type `PCodeViewModel`
- Exposed for binding in MainWindow

## Compilation Pipeline

### Live Compilation Flow:
1. User types in editor
2. `DocumentStateManager.AnalyzeDocumentAsync()` runs:
   - Lexical Analysis → Fires `LexicalAnalysisCompletedEvent`
   - Syntax Analysis → Fires `SyntaxAnalysisCompletedEvent`
   - Semantic Analysis → Fires `SemanticAnalysisCompletedEvent`
   - **P-Code Generation** (if no semantic errors) → Fires `PCodeGenerationCompletedEvent`

### Manual P-Code Generation:
1. User clicks "Compiler" → "Generate P-Code"
2. `GeneratePCodeMenuItem_Click` fires `PCodeGenerationRequestEvent`
3. `CompilerBridge.OnPCodeGenerationRequest` generates P-code
4. `PCodeGenerationCompletedEvent` fired
5. P-code view displays instructions

### P-Code Execution:
1. User clicks Execute button (or Execute P-Code button in toolbar)
2. Terminal is cleared via `ClearTerminalRequestEvent`
3. Terminal is shown via `ShowTerminalTabRequestEvent(0)`
4. `PCodeExecutionRequestEvent` is fired
5. `CompilerBridge.OnPCodeExecutionRequest` executes P-code
6. `PCodeInterpreter` fires `OnOutput` events for each WRT/WRS/WRL operation
7. `TerminalPanel` subscribes to `PCodeExecutionOutputEvent` and displays output
8. `PCodeExecutionCompletedEvent` fired when done

## Usage

1. **View P-Code**: Click the P-Code button in the tool window strip (left side)
2. **Generate P-Code**: 
   - Automatic: Type code in editor (live compilation)
   - Manual: Click "Compiler" → "Generate P-Code" (requires successful semantic analysis)
3. **Execute P-Code**: Click the Execute button in the toolbar (▶ icon) once P-code is generated
4. **View Output**: Terminal panel automatically opens and displays execution output

## Code Flow Diagram

```
Editor Content Change
    ↓
DocumentStateManager
    ↓
Lexical → Syntax → Semantic → P-Code Generation
    ↓
PCodeGenerationCompletedEvent
    ↓
├─→ ActiveEditorService → TextEditorViewModel.PCodeProgram
├─→ PCodeViewModel → Update Grid
└─→ Toolbar → Enable Execute Button

Execute Button Click
    ↓
PCodeExecutionRequestEvent
    ↓
CompilerBridge
    ↓
PCodeInterpreter
    ↓
OnOutput Events
    ↓
PCodeExecutionOutputEvent
    ↓
TerminalPanel → Display Output
```

## Testing Notes

- P-Code generation only occurs if there are no semantic errors
- Execute button is only enabled when valid P-code exists
- Terminal automatically clears before execution
- Terminal automatically opens and switches to tab 0 on execution
- All output is captured and displayed in real-time

## Future Enhancements

- Add input dialog for READ operations (currently uses queue)
- Add step-by-step debugging for P-code execution
- Add breakpoints in P-code view
- Show execution stack state during execution
- Add P-code export functionality


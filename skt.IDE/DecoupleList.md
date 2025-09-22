# MainWindow Refactoring Checklist

## Quick Implementation Options (Pick Any)

- [x] **Move editor cursor/changed-text handling out of MainWindow** (LOW RISK)
- [ ] **Move tool-window selection & UI state into ViewModel** (MEDIUM RISK)
- [ ] **Move terminal panel show/hide logic into ViewModel** (LOW-MEDIUM RISK)
- [ ] **Move analysis/refresh logic into service/PhaseOutputViewModel** (MEDIUM RISK)
- [ ] **Remove remaining file/save UI state from MainWindow** (LOW RISK)
- [ ] **Keep window-state → ViewModel sync in MainWindow** (LEAVE AS-IS)

---

## Detailed Implementation Suggestions

### 1. Editor Cursor & TextChanged Handling ⭐ RECOMMENDED FIRST STEP
**Risk Level:** LOW
**Priority:** HIGH

**Problem:**
- `EditorTextBox_TextChanged` in MainWindow.axaml.cs contains editor-specific logic
- Cursor index → line/column calculation ties window to editor internals
- Triggers analysis from wrong location

**Solution:**
- Move cursor calculation and TextChanged handling into TabbedEditor control
- Editor should publish `CursorPositionChangedEvent` (line, column)
- Optional: publish `EditorTextChangedEvent` with text
- MainWindowViewModel subscribes to position changes via EventBus

**Files to Modify:**
- FROM: Views/MainWindow.axaml.cs
- TO: Views/TextEditor/TabbedEditor.axaml.cs OR TabbedEditorViewModel.cs
- ADD: Services/EventBus → CursorPositionChangedEvent
- UPDATE: MainWindowViewModel.cs → subscribe to cursor events

**Edge Cases:**
- Editor with no text: caret at 0 → should show line 1, column 1
- Large documents: throttle events (publish on caret change, not every keystroke)

### 2. Tool Window Selection & Toggling
**Risk Level:** MEDIUM
**Priority:** MEDIUM

**Problem:**
- MainWindow directly manages tool window indices/visibility
- DOM classes manipulated in code-behind
- UI logic not testable or decoupled

**Solution:**
- Move `_selectedToolWindow`, `UpdateToolWindowVisibility`, `UpdateToolWindowSelection` to MainWindowViewModel
- Expose ICommand properties for tool window switching
- Bind commands to XAML buttons
- Remove click handlers from code-behind

**Files to Modify:**
- Views/MainWindow.axaml.cs → remove ToolWindowToggle_Click handlers
- Views/MainWindow.axaml → wire buttons to VM commands
- ViewModels/MainWindowViewModel.cs → add SelectedToolWindowIndex, commands

**Edge Cases:**
- CSS Classes.Add may need small code-behind helpers
- Prefer XAML styles/triggers bound to VM properties

### 3. Terminal Panel Visibility & Tab Switching
**Risk Level:** LOW-MEDIUM
**Priority:** LOW-MEDIUM

**Problem:**
- `UpdateTerminalPanelVisibility` manipulates Grid row heights in code-behind
- Layout logic should be declarative via data binding

**Solution:**
- Add `bool IsTerminalPanelVisible` to MainWindowViewModel
- Add `int SelectedTerminalPanelIndex` to MainWindowViewModel
- Bind GridRow height/Visibility in XAML (use converter if needed)
- Move `SwitchTerminalTab`, `ToggleTerminalPanel` to ViewModel

**Files to Modify:**
- Views/MainWindow.axaml.cs → remove panel manipulation
- Views/MainWindow.axaml → add bindings
- ViewModels/MainWindowViewModel.cs → add panel state properties

**Edge Cases:**
- Animations/measured heights → use fixed pixel height for visible panel
- Create bool → GridLength converter

### 4. Analysis & Refresh Logic
**Risk Level:** MEDIUM
**Priority:** MEDIUM

**Problem:**
- `RefreshAnalysisData` and `UpdateAnalysisOutput` are domain logic
- Analysis orchestration doesn't belong in MainWindow

**Solution:**
- Create `IAnalysisService` OR move to PhaseOutputViewModel/TokensViewModel
- Service subscribes to editor events (file opened, saved, text changes)
- Publish analysis results via events OR set ViewModel properties directly
- Implement debounced text change handling

**Files to Modify:**
- Views/MainWindow.axaml.cs → remove RefreshAnalysisData
- ADD: Services/AnalysisService.cs OR UPDATE: PhaseOutputViewModel.cs

**Edge Cases:**
- Debounce/async handling to prevent UI blocking
- Cancel previous analysis when new request arrives

### 5. Remove Remaining Toolbar State (MOSTLY DONE)
**Risk Level:** LOW
**Status:** IN PROGRESS

**What's Done:**
- Removed CanSave, CanSaveAs, CanCreateNewFile bindings
- Toolbar subscribes to EventBus directly

**Still To Check:**
- MainWindowViewModel.cs → ensure only window-level state remains
- ToolBar.axaml → remove any remaining MainWindowViewModel bindings

### 6. File/Save Commands (VERIFY COMPLETE)
**Risk Level:** LOW
**Status:** SHOULD BE DONE

**Current State:**
- TabbedEditorViewModel handles Save/Open requests
- Toolbar publishes SaveRequest events
- File dialogs should be in TabbedEditor or dedicated service

**Files to Verify:**
- TabbedEditorViewModel.cs → confirms subscription to Save/Open
- Toolbar.axaml.cs → confirms event publishing

---

## Immediate Implementation Candidates

### Option A: Editor Cursor Decoupling (RECOMMENDED)
**Scope:** Small, contained change
**Steps:**
1. Move UpdateCursorPosition + TextChanged handling to TabbedEditor
2. Create and publish CursorPositionChangedEvent
3. Remove EditorTextBox_TextChanged from MainWindow
4. Update MainWindowViewModel to subscribe and update CurrentLine/Column

### Option B: Terminal Panel → ViewModel
**Scope:** UI state management
**Steps:**
1. Move ToggleTerminalPanel to MainWindowViewModel
2. Add IsTerminalPanelVisible property
3. Update XAML bindings
4. Thin out MainWindow code-behind

### Option C: Analysis Service Extraction
**Scope:** Domain logic separation  
**Steps:**
1. Move RefreshAnalysisData to PhaseOutputViewModel
2. Subscribe to file open/update events
3. Remove analysis logic from MainWindow

---

## Quality Gates & Safety Checks

**After Each Change:**
- [ ] Run `dotnet build` → check for compile errors
- [ ] Run unit tests in `skt.Compiler.Tests`
- [ ] Smoke test: open app UI, verify toolbar behavior unchanged

**Edge Cases to Monitor:**
- Event ordering → ensure subscriptions occur before relevant events
- Unsubscribe on Unloaded (toolbar already handles this)
- High-frequency events → implement debouncing for editor text changes

---

## Next Steps

**RECOMMENDATION:** Start with **Option A (Editor Cursor Decoupling)**
- Lowest risk
- Quick implementation
- Removes clear MainWindow responsibility
- Good foundation for other changes

**After Option A:** Move to terminal panel ViewModel, then analysis pipeline

**Ready to implement?** Choose one option and I'll:
1. Apply the necessary code edits
2. Run project build & report results
3. Execute tests and share findings
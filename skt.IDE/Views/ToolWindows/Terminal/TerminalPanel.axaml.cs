using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using skt.IDE.Services.Buss;
using System;

namespace skt.IDE.Views.ToolWindows.Terminal;

public partial class TerminalPanel : UserControl
{
    private bool _waitingForInput = false;
    private int _inputStartPosition = 0;
    private string _lastValidText = string.Empty;
    
    public TerminalPanel()
    {
        InitializeComponent();
        
        // Subscribe to terminal events
        App.Messenger.Register<PCodeExecutionOutputEvent>(this, (_, m) => OnExecutionOutput(m));
        App.Messenger.Register<ClearTerminalRequestEvent>(this, (_, m) => OnClearTerminal(m));
        App.Messenger.Register<PCodeInputRequestEvent>(this, (_, m) => OnInputRequest(m));
        App.Messenger.Register<PCodeExecutionCompletedEvent>(this, (_, m) => OnExecutionCompleted(m));
        
        // Handle input in the main terminal TextBox
        var terminalTextBox = this.FindControl<TextBox>("TerminalTextBox");
        if (terminalTextBox != null)
        {
            terminalTextBox.KeyDown += TerminalTextBox_KeyDown;
            terminalTextBox.AddHandler(InputElement.KeyDownEvent, TerminalTextBox_PreviewKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            terminalTextBox.TextChanged += TerminalTextBox_TextChanged;
            terminalTextBox.GotFocus += TerminalTextBox_GotFocus;
            terminalTextBox.LostFocus += TerminalTextBox_LostFocus;
            terminalTextBox.PointerPressed += TerminalTextBox_PointerPressed;
        }
        
        // Constrain TextBox height to prevent overflow beyond ScrollViewer
        var terminalScrollViewer = this.FindControl<ScrollViewer>("TerminalScrollViewer");
        if (terminalScrollViewer != null && terminalTextBox != null)
        {
            // Update MaxHeight when ScrollViewer size changes
            terminalScrollViewer.SizeChanged += (s, e) =>
            {
                if (e.NewSize.Height > 0 && terminalTextBox != null)
                {
                    // Set MaxHeight to available height minus padding
                    var availableHeight = e.NewSize.Height - 6; // 6 is top padding
                    if (availableHeight > 0)
                    {
                        terminalTextBox.MaxHeight = availableHeight;
                    }
                }
            };
        }
        
        // Clean up on unload
        Unloaded += (_, _) => App.Messenger.UnregisterAll(this);
    }

    public void SetSelectedTab(int index)
    {
        TerminalTabView.SelectedIndex = index;
    }

    private void OnExecutionOutput(PCodeExecutionOutputEvent e)
    {
        System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Received output: {e.Output}");
        Dispatcher.UIThread.Post(() =>
        {
            var textBox = this.FindControl<TextBox>("TerminalTextBox");
            if (textBox != null)
            {
                System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Appending to textbox: {e.Output}");
                textBox.Text += e.Output;
                _inputStartPosition = textBox.Text?.Length ?? 0;
                
                // Scroll to end by setting caret to end
                textBox.CaretIndex = textBox.Text?.Length ?? 0;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[TerminalPanel] ERROR: TerminalTextBox not found!");
            }
        });
    }

    private void OnClearTerminal(ClearTerminalRequestEvent e)
    {
        System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Clearing terminal");
        Dispatcher.UIThread.Post(() =>
        {
            var textBox = this.FindControl<TextBox>("TerminalTextBox");
            if (textBox != null)
            {
                textBox.Text = string.Empty;
                textBox.IsReadOnly = true; // Start as readonly until input is requested
            }
            
            _waitingForInput = false;
            _inputStartPosition = 0;
        });
    }

    private void OnExecutionCompleted(PCodeExecutionCompletedEvent e)
    {
        System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Execution completed, cleaning up input state");
        Dispatcher.UIThread.Post(() =>
        {
            var textBox = this.FindControl<TextBox>("TerminalTextBox");
            if (textBox != null)
            {
                textBox.IsReadOnly = true; // Make readonly when execution completes
            }
            
            // Clean up waiting state
            _waitingForInput = false;
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] _waitingForInput set to false");
        });
    }
    
    private void OnInputRequest(PCodeInputRequestEvent e)
    {
        System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Input requested - setting up for input");
        Dispatcher.UIThread.Post(() =>
        {
            _waitingForInput = true;
            
            var textBox = this.FindControl<TextBox>("TerminalTextBox");
            if (textBox != null)
            {
                // Mark where user input starts
                _inputStartPosition = textBox.Text?.Length ?? 0;
                // Store ALL accumulated text (all previous outputs + inputs) as the last valid state
                _lastValidText = textBox.Text ?? string.Empty;
                
                System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Input start position: {_inputStartPosition}");
                System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Current text: '{textBox.Text}'");
                
                // Make textbox editable and focusable
                textBox.IsReadOnly = false;
                textBox.Focusable = true;
                textBox.IsEnabled = true;
                textBox.CaretIndex = _inputStartPosition;
                
                // Single focus attempt with proper priority
                Dispatcher.UIThread.Post(() =>
                {
                    if (_waitingForInput && textBox != null) // Verify still waiting
                    {
                        textBox.Focus();
                        textBox.CaretIndex = _inputStartPosition;
                        System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Focused and caret set to {_inputStartPosition}");
                    }
                }, Avalonia.Threading.DispatcherPriority.Input);
                
                System.Diagnostics.Debug.WriteLine($"[TerminalPanel] TextBox is now editable and focus requested");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[TerminalPanel] ERROR: TerminalTextBox not found for input!");
            }
        });
    }
    
    private void TerminalTextBox_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[TerminalPanel] PreviewKeyDown: Key={e.Key}, _waitingForInput={_waitingForInput}");
        
        if (!_waitingForInput)
        {
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Not waiting for input, ignoring key");
            return;
        }
        
        var textBox = sender as TextBox;
        if (textBox == null)
        {
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] ERROR: TextBox is null in PreviewKeyDown");
            return;
        }
        
        // Verify caret is in valid position before processing any key
        if (textBox.CaretIndex < _inputStartPosition)
        {
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] PreviewKeyDown: Caret was at {textBox.CaretIndex}, correcting to {_inputStartPosition}");
            textBox.CaretIndex = _inputStartPosition;
        }
        
        if (e.Key == Key.Enter)
        {
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] ===== ENTER KEY PRESSED =====");
            
            // Get the input that the user typed (everything after _inputStartPosition)
            var fullText = textBox.Text ?? string.Empty;
            var input = fullText.Length > _inputStartPosition 
                ? fullText.Substring(_inputStartPosition) 
                : string.Empty;
            
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] User pressed Enter");
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Full text: '{fullText}'");
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Full text length: {fullText.Length}");
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Input start pos: {_inputStartPosition}");
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Extracted input: '{input}'");
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Input length: {input.Length}");
            
            // Add newline to terminal
            textBox.Text += Environment.NewLine;
            _inputStartPosition = textBox.Text.Length;
            _lastValidText = textBox.Text;
            
            // Make readonly again while executing
            textBox.IsReadOnly = true;
            
            // Send input response
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Sending PCodeInputResponseEvent with: '{input}'");
            App.Messenger.Send(new PCodeInputResponseEvent(input));
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Event sent successfully");
            
            _waitingForInput = false;
            e.Handled = true; // Critical: Prevent Enter from creating additional newline
            
            // Auto-scroll to end after input is submitted - multiple attempts for reliability
            var scrollViewer = this.FindControl<ScrollViewer>("TerminalScrollViewer");
            if (scrollViewer != null)
            {
                Dispatcher.UIThread.Post(() => scrollViewer.ScrollToEnd(), DispatcherPriority.Background);
                Dispatcher.UIThread.Post(() => 
                {
                    System.Threading.Thread.Sleep(10);
                    scrollViewer.ScrollToEnd();
                }, DispatcherPriority.Background);
            }
            
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] ===== INPUT HANDLING COMPLETE =====");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Key {e.Key} pressed (not Enter)");
        }
    }
    
    private void TerminalTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!_waitingForInput) return;
        
        var textBox = sender as TextBox;
        if (textBox == null) return;
        
        // First, verify caret position is valid
        if (textBox.CaretIndex < _inputStartPosition)
        {
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Caret was at {textBox.CaretIndex}, correcting to {_inputStartPosition}");
            textBox.CaretIndex = _inputStartPosition;
        }
        
        // Check if there's a selection that includes protected text
        if (!string.IsNullOrEmpty(textBox.SelectedText))
        {
            var selectionStart = textBox.SelectionStart;
            if (selectionStart < _inputStartPosition)
            {
                // Selection includes protected text, prevent any modification
                if (e.Key == Key.Back || e.Key == Key.Delete || e.Key == Key.X && e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    e.Handled = true;
                    textBox.SelectionStart = _inputStartPosition;
                    textBox.SelectionEnd = _inputStartPosition;
                    return;
                }
            }
        }
        
        // Prevent Backspace at or before input start
        if (e.Key == Key.Back)
        {
            if (textBox.CaretIndex <= _inputStartPosition)
            {
                e.Handled = true;
            }
        }
        
        // Prevent Delete if caret is before input start or at the protected boundary
        if (e.Key == Key.Delete)
        {
            if (textBox.CaretIndex < _inputStartPosition)
            {
                e.Handled = true;
            }
        }
        
        // Prevent Left arrow at input start position
        if (e.Key == Key.Left)
        {
            if (textBox.CaretIndex <= _inputStartPosition)
            {
                e.Handled = true;
            }
        }
        
        // Prevent Home key from going before input start
        if (e.Key == Key.Home)
        {
            e.Handled = true;
            textBox.CaretIndex = _inputStartPosition;
        }
        
        // Prevent Ctrl+A (Select All) as it would select protected text
        if (e.Key == Key.A && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            // Select only user input
            textBox.SelectionStart = _inputStartPosition;
            textBox.SelectionEnd = textBox.Text?.Length ?? _inputStartPosition;
        }
    }
    
    private void TerminalTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_waitingForInput) return;
        
        var textBox = sender as TextBox;
        if (textBox == null) return;
        
        // Prevent user from deleting text before input start position
        var currentText = textBox.Text ?? string.Empty;
        var currentLength = currentText.Length;
        
        if (currentLength < _inputStartPosition)
        {
            // User tried to delete program output, restore it
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] User tried to delete output. Restoring from length {currentLength} to {_inputStartPosition}");
            
            // Restore the text up to the input start position
            textBox.Text = _lastValidText;
            textBox.CaretIndex = _inputStartPosition;
            
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Text restored, caret at {textBox.CaretIndex}");
        }
        else if (currentLength >= _inputStartPosition)
        {
            // Valid text change, update last valid text
            _lastValidText = currentText;
        }
    }
    
    private void TerminalTextBox_GotFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox == null) return;
        
        if (_waitingForInput)
        {
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] TextBox regained focus while waiting for input");
            
            // Ensure textbox is editable
            textBox.IsReadOnly = false;
            
            // Restore caret to end of text (where user should type)
            var currentLength = textBox.Text?.Length ?? 0;
            if (currentLength >= _inputStartPosition)
            {
                textBox.CaretIndex = currentLength;
            }
            else
            {
                textBox.CaretIndex = _inputStartPosition;
            }
            
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Caret restored to position: {textBox.CaretIndex}, IsReadOnly: {textBox.IsReadOnly}");
        }
    }
    
    private void TerminalTextBox_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox == null) return;
        
        if (_waitingForInput)
        {
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] WARNING: TextBox lost focus while waiting for input!");
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] User needs to click back on terminal to continue");
            // Note: We preserve state so when user clicks back, PointerPressed will restore everything
        }
    }
    
    private void TerminalTextBox_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox == null) return;
        
        if (_waitingForInput)
        {
            System.Diagnostics.Debug.WriteLine($"[TerminalPanel] User clicked on terminal while waiting for input");
            
            // Restore input state
            textBox.IsReadOnly = false;
            textBox.Focusable = true;
            
            // Immediately enforce caret position after click is processed
            Dispatcher.UIThread.Post(() =>
            {
                if (textBox.CaretIndex < _inputStartPosition)
                {
                    System.Diagnostics.Debug.WriteLine($"[TerminalPanel] User clicked before input position ({textBox.CaretIndex}), correcting to {_inputStartPosition}");
                    textBox.CaretIndex = _inputStartPosition;
                }
                
                // Clear any selection that includes protected text
                if (!string.IsNullOrEmpty(textBox.SelectedText))
                {
                    var selectionStart = textBox.SelectionStart;
                    if (selectionStart < _inputStartPosition)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TerminalPanel] Selection includes protected text, clearing selection");
                        textBox.SelectionStart = _inputStartPosition;
                        textBox.SelectionEnd = _inputStartPosition;
                    }
                }
            }, DispatcherPriority.Render);
            
            // Also verify after a longer delay to catch any edge cases
            Dispatcher.UIThread.Post(() =>
            {
                if (_waitingForInput && textBox != null)
                {
                    if (textBox.CaretIndex < _inputStartPosition)
                    {
                        textBox.CaretIndex = _inputStartPosition;
                    }
                    textBox.Focus();
                }
            }, DispatcherPriority.Background);
        }
    }
}

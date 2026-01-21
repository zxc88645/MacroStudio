using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using MacroStudio.Application.Services;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Presentation.SyntaxHighlighting;
using MacroStudio.Presentation.Views;
using System.Collections.ObjectModel;
using MoonSharp.Interpreter;
using LuaScript = MoonSharp.Interpreter.Script;
using Script = MacroStudio.Domain.Entities.Script;
using System.Windows.Threading;
using Point = MacroStudio.Domain.ValueObjects.Point;

namespace MacroStudio.Presentation.ViewModels;

/// <summary>
/// ViewModel for displaying and editing the command list of a selected script.
/// </summary>
public partial class CommandGridViewModel : ObservableObject
{
    private readonly IScriptManager _scriptManager;
    private readonly ILoggingService _loggingService;
    private readonly IInputSimulator _inputSimulator;
    private bool _isUpdatingDocument;
    private string _originalScriptText = string.Empty;
    private readonly DispatcherTimer _autoSaveTimer;
    private int _pendingVersion;

    public ObservableCollection<Command> Commands { get; } = new();

    // Updated by the view (AvalonEdit) so we can insert snippets at the caret.
    [ObservableProperty]
    private int caretOffset;

    [ObservableProperty]
    private int selectionStart;

    [ObservableProperty]
    private int selectionLength;

    [ObservableProperty]
    private string scriptText = string.Empty;

    [ObservableProperty]
    private Script? currentScript;

    [ObservableProperty]
    private bool hasUnsavedChanges;

    [ObservableProperty]
    private string editorStatusText = string.Empty;

    public event EventHandler? DiagnosticsChanged;

    [ObservableProperty]
    private bool hasDiagnostic;

    [ObservableProperty]
    private int diagnosticLine;

    [ObservableProperty]
    private int diagnosticColumn;

    [ObservableProperty]
    private string? diagnosticMessage;

    /// <summary>
    /// Gets the AvalonEdit document for the script text.
    /// </summary>
    public TextDocument Document { get; }

    /// <summary>
    /// Gets the syntax highlighting definition for Macro Studio scripts.
    /// </summary>
    public IHighlightingDefinition SyntaxHighlighting { get; }

    public CommandGridViewModel(IScriptManager scriptManager, ILoggingService loggingService, IInputSimulator inputSimulator)
    {
        _scriptManager = scriptManager ?? throw new ArgumentNullException(nameof(scriptManager));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _inputSimulator = inputSimulator ?? throw new ArgumentNullException(nameof(inputSimulator));

        Document = new TextDocument();
        Document.TextChanged += OnDocumentTextChanged;
        
        SyntaxHighlighting = MacroScriptSyntaxMode.GetHighlightingDefinition();

        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(650)
        };
        _autoSaveTimer.Tick += OnAutoSaveTimerTick;
    }

    public void LoadScript(Script? script)
    {
        CurrentScript = script;
        Commands.Clear();
        if (script == null)
        {
            _isUpdatingDocument = true;
            Document.Text = string.Empty;
            _originalScriptText = string.Empty;
            HasUnsavedChanges = false;
            _isUpdatingDocument = false;
            SetDiagnostic(null, 0, 0);
            EditorStatusText = string.Empty;
            return;
        }
        
        foreach (var cmd in script.Commands)
            Commands.Add(cmd);

        // Lua-only editor: SourceText is the primary representation.
        // If empty (e.g. legacy/recorded script), fall back to a sequence of host API calls
        // which is valid Lua as well.
        var text = string.IsNullOrWhiteSpace(script.SourceText)
            ? ScriptTextConverter.ToText(script)
            : script.SourceText;
        _isUpdatingDocument = true;
        Document.Text = text;
        _originalScriptText = text;
        HasUnsavedChanges = false;
        _isUpdatingDocument = false;
        SetDiagnostic(null, 0, 0);
        EditorStatusText = string.Empty;
    }

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingDocument) return;
        
        ScriptText = Document.Text;
        
        // Check if text has changed from original
        HasUnsavedChanges = CurrentScript != null && Document.Text != _originalScriptText;
        if (HasUnsavedChanges)
            EditorStatusText = "Unsaved";

        // Debounced validate + autosave on UI thread (more reliable than Task.Run + dispatcher hops).
        _pendingVersion++;
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    [RelayCommand(CanExecute = nameof(HasScript))]
    private async Task AddSleepCommandAsync()
    {
        // Quick insert: msleep(100) at caret (or replace selection).
        // Persistence still happens via "Apply Script" like other text edits.
        InsertSnippetAtCaret("msleep(100)");
    }

    [RelayCommand(CanExecute = nameof(HasScript))]
    private async Task AddKeyboardTextAsync()
    {
        if (CurrentScript == null) return;

        var dlg = new InputDialog(
            "Add Keyboard Text",
            "輸入要自動輸出的文字（會插入 KeyboardCommand）。",
            "Text:",
            "");
        dlg.Owner = System.Windows.Application.Current?.MainWindow;

        if (dlg.ShowDialog() != true)
            return;

        var text = dlg.ValueText?.Trim() ?? string.Empty;
        
        // Validate that text is not empty
        if (string.IsNullOrEmpty(text))
        {
            await _loggingService.LogWarningAsync("Cannot add empty keyboard text command", new Dictionary<string, object>
            {
                { "ScriptId", CurrentScript.Id },
                { "ScriptName", CurrentScript.Name }
            });
            return;
        }

        InsertSnippetAtCaret($"type_text('{EscapeSingleQuotes(text)}')");
    }

    [RelayCommand(CanExecute = nameof(HasScript))]
    private async Task AddMouseMoveAsync()
    {
        if (CurrentScript == null) return;

        var picker = new PickPointWindow
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        // 使用 PickPointWindow，讓使用者移動滑鼠並按 F2 來決定座標
        if (picker.ShowDialog() != true)
            return;

        var position = await _inputSimulator.GetCursorPositionAsync();
        InsertSnippetAtCaret($"move({position.X}, {position.Y})");
    }

    [RelayCommand(CanExecute = nameof(HasScript))]
    private Task AddLeftClickAsync() => AddClickSequenceAsync(MouseButton.Left);

    [RelayCommand(CanExecute = nameof(HasScript))]
    private Task AddRightClickAsync() => AddClickSequenceAsync(MouseButton.Right);

    [RelayCommand(CanExecute = nameof(HasScript))]
    private Task AddMiddleClickAsync() => AddClickSequenceAsync(MouseButton.Middle);

    [RelayCommand(CanExecute = nameof(HasScript))]
    private async Task RemoveLastCommandAsync()
    {
        if (CurrentScript == null) return;
        RemoveLastNonEmptyLine();
    }

    private bool HasScript() => CurrentScript != null;

    private async Task<bool> TryApplyScriptTextInternalAsync()
    {
        if (CurrentScript == null) return false;

        try
        {
            CurrentScript.SourceText = Document.Text ?? string.Empty;
            await _scriptManager.UpdateScriptAsync(CurrentScript);
            return true;
        }
        catch (Exception ex)
        {
            await _loggingService.LogErrorAsync("Failed to parse script text before modifying commands", ex, new Dictionary<string, object>
            {
                { "ScriptId", CurrentScript.Id },
                { "ScriptName", CurrentScript.Name }
            });
            return false;
        }
    }

    private static bool TryParsePoint(string? text, out Point point)
    {
        point = Point.Zero;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        if (!int.TryParse(parts[0], out var x)) return false;
        if (!int.TryParse(parts[1], out var y)) return false;
        if (x < 0 || y < 0) return false;
        point = new Point(x, y);
        return true;
    }

    private async Task PersistAndReloadAsync(string logMessage)
    {
        if (CurrentScript == null) return;

        await _scriptManager.UpdateScriptAsync(CurrentScript);
        await _loggingService.LogInfoAsync(logMessage, new Dictionary<string, object>
        {
            { "ScriptId", CurrentScript.Id },
            { "CommandCount", CurrentScript.CommandCount }
        });

        LoadScript(CurrentScript);
        AddSleepCommandCommand.NotifyCanExecuteChanged();
        RemoveLastCommandCommand.NotifyCanExecuteChanged();
        AddKeyboardTextCommand.NotifyCanExecuteChanged();
        AddMouseMoveCommand.NotifyCanExecuteChanged();
        AddLeftClickCommand.NotifyCanExecuteChanged();
        AddRightClickCommand.NotifyCanExecuteChanged();
        AddMiddleClickCommand.NotifyCanExecuteChanged();
    }

    partial void OnCurrentScriptChanged(Script? oldValue, Script? newValue)
    {
        AddSleepCommandCommand.NotifyCanExecuteChanged();
        RemoveLastCommandCommand.NotifyCanExecuteChanged();
        AddKeyboardTextCommand.NotifyCanExecuteChanged();
        AddMouseMoveCommand.NotifyCanExecuteChanged();
        AddLeftClickCommand.NotifyCanExecuteChanged();
        AddRightClickCommand.NotifyCanExecuteChanged();
        AddMiddleClickCommand.NotifyCanExecuteChanged();
    }

    private async Task AddClickSequenceAsync(MouseButton button)
    {
        if (CurrentScript == null) return;
        var b = button.ToString().ToLowerInvariant();
        InsertSnippetAtCaret(
            $"mouse_down('{b}'){Environment.NewLine}" +
            $"msleep(50){Environment.NewLine}" +
            $"mouse_release('{b}')");
    }

    private void InsertSnippetAtCaret(string snippet)
    {
        InsertTextAtCaret(snippet, ensureStandaloneLine: true);
    }

    /// <summary>
    /// Inserts arbitrary text into the editor at the current caret position (or replaces selection).
    /// Intended for integrating other UI features (e.g., insert recorded script into editor).
    /// </summary>
    public void InsertTextAtCaret(string? text, bool ensureStandaloneLine)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var insertion = text;

        if (ensureStandaloneLine)
        {
            // Ensure insertion ends with a newline so subsequent typing starts on a new line.
            if (!insertion.EndsWith("\n", StringComparison.Ordinal) &&
                !insertion.EndsWith("\r", StringComparison.Ordinal))
            {
                insertion += Environment.NewLine;
            }
        }

        var doc = Document;
        var docLen = doc.TextLength;
        var caret = Math.Clamp(CaretOffset, 0, docLen);

        // If inserting mid-line, start on a new line first.
        if (ensureStandaloneLine && caret > 0)
        {
            var prev = doc.GetCharAt(caret - 1);
            if (prev != '\n' && prev != '\r')
            {
                insertion = Environment.NewLine + insertion;
            }
        }

        if (SelectionLength > 0)
        {
            var start = Math.Clamp(SelectionStart, 0, docLen);
            var len = Math.Clamp(SelectionLength, 0, docLen - start);
            doc.Replace(start, len, insertion);
            CaretOffset = start + insertion.Length;
        }
        else
        {
            doc.Insert(caret, insertion);
            CaretOffset = caret + insertion.Length;
        }
    }

    private static string EscapeSingleQuotes(string text) => text.Replace("'", "\\'");

    private async void OnAutoSaveTimerTick(object? sender, EventArgs e)
    {
        _autoSaveTimer.Stop();

        var version = _pendingVersion;

        // 1) Validate first (so we don't auto-save invalid scripts)
        await ValidateLuaSyntaxAsync(CancellationToken.None);
        if (version != _pendingVersion)
            return;

        // 2) Auto-save
        await AutoSaveAsync(version, CancellationToken.None);
    }

    private async Task AutoSaveAsync(int version, CancellationToken ct)
    {
        if (CurrentScript == null)
            return;

        if (!HasUnsavedChanges)
            return;

        if (HasDiagnostic)
        {
            EditorStatusText = "Syntax error";
            return;
        }

        try
        {
            EditorStatusText = "Saving…";
            CurrentScript.SourceText = Document.Text ?? string.Empty;
            await _scriptManager.UpdateScriptAsync(CurrentScript);

            if (version != _pendingVersion)
                return;

            _originalScriptText = CurrentScript.SourceText;
            HasUnsavedChanges = false;
            EditorStatusText = "Saved";
        }
        catch (Exception ex)
        {
            EditorStatusText = "Save failed";
            await _loggingService.LogErrorAsync("Auto-save failed", ex, new Dictionary<string, object>
            {
                { "ScriptId", CurrentScript.Id },
                { "ScriptName", CurrentScript.Name }
            });
        }
    }

    private async Task ValidateLuaSyntaxAsync(CancellationToken ct)
    {
        if (CurrentScript == null)
        {
            SetDiagnostic(null, 0, 0);
            return;
        }

        var code = Document.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            SetDiagnostic(null, 0, 0);
            return;
        }

        try
        {
            var lua = new LuaScript(CoreModules.Basic | CoreModules.String | CoreModules.Table | CoreModules.Math);
            lua.LoadString(code);
            SetDiagnostic(null, 0, 0);
        }
        catch (SyntaxErrorException ex)
        {
            var (line, col) = TryParseLineCol(ex.DecoratedMessage);
            SetDiagnostic(ex.DecoratedMessage, line, col);
        }
        catch (Exception ex)
        {
            SetDiagnostic(ex.Message, 0, 0);
        }

        await Task.CompletedTask;
    }

    private void SetDiagnostic(string? message, int line, int column)
    {
        // If we have a message but no location, mark line 1 so UI still shows a highlight.
        if (!string.IsNullOrWhiteSpace(message) && line <= 0)
        {
            line = 1;
            column = 1;
        }

        DiagnosticMessage = message;
        DiagnosticLine = line;
        DiagnosticColumn = column;
        HasDiagnostic = !string.IsNullOrWhiteSpace(message);
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static (int line, int col) TryParseLineCol(string? msg)
    {
        if (string.IsNullOrEmpty(msg))
            return (0, 0);

        var lineIdx = msg.IndexOf("line ", StringComparison.OrdinalIgnoreCase);
        if (lineIdx >= 0)
        {
            var after = msg[(lineIdx + 5)..];
            var parts = after.Split(new[] { ' ', '\r', '\n', '\t', ',', ')' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && int.TryParse(parts[0], out var line))
            {
                var colIdx = msg.IndexOf("col ", StringComparison.OrdinalIgnoreCase);
                if (colIdx >= 0)
                {
                    var afterCol = msg[(colIdx + 4)..];
                    var colParts = afterCol.Split(new[] { ' ', '\r', '\n', '\t', ',', ')' }, StringSplitOptions.RemoveEmptyEntries);
                    if (colParts.Length > 0 && int.TryParse(colParts[0], out var col))
                        return (line, col);
                }
                return (line, 1);
            }
        }

        return (0, 0);
    }

    private void RemoveLastNonEmptyLine()
    {
        var text = Document.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return;

        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                lines.RemoveAt(i);
                break;
            }
        }

        _isUpdatingDocument = true;
        Document.Text = string.Join(Environment.NewLine, lines);
        _isUpdatingDocument = false;
    }
}


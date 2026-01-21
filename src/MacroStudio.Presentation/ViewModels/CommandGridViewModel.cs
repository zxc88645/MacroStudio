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
            ApplyScriptTextCommand.NotifyCanExecuteChanged();
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
        ApplyScriptTextCommand.NotifyCanExecuteChanged();
    }

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingDocument) return;
        
        ScriptText = Document.Text;
        
        // Check if text has changed from original
        HasUnsavedChanges = CurrentScript != null && Document.Text != _originalScriptText;
        ApplyScriptTextCommand.NotifyCanExecuteChanged();
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

    [RelayCommand(CanExecute = nameof(CanApplyScript))]
    private async Task ApplyScriptTextAsync()
    {
        if (CurrentScript == null) return;

        try
        {
            CurrentScript.SourceText = Document.Text ?? string.Empty;
            await PersistAndReloadAsync("Updated script source");
        }
        catch (Exception ex)
        {
            await _loggingService.LogErrorAsync("Failed to parse script text", ex, new Dictionary<string, object>
            {
                { "ScriptId", CurrentScript.Id },
                { "ScriptName", CurrentScript.Name }
            });
        }
    }

    private bool HasScript() => CurrentScript != null;
    
    private bool CanApplyScript() => CurrentScript != null && HasUnsavedChanges;

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
        ApplyScriptTextCommand.NotifyCanExecuteChanged();
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
        ApplyScriptTextCommand.NotifyCanExecuteChanged();
    }

    private async Task AddClickSequenceAsync(MouseButton button)
    {
        if (CurrentScript == null) return;
        InsertSnippetAtCaret($"mouse_click('{button.ToString().ToLowerInvariant()}')");
    }

    private void InsertSnippetAtCaret(string snippet)
    {
        // Ensure this is a standalone line.
        var insertion = snippet + Environment.NewLine;

        var doc = Document;
        var docLen = doc.TextLength;
        var caret = Math.Clamp(CaretOffset, 0, docLen);

        // If inserting mid-line, start on a new line first.
        if (caret > 0)
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


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

    public ObservableCollection<Command> Commands { get; } = new();

    [ObservableProperty]
    private string scriptText = string.Empty;

    [ObservableProperty]
    private Script? currentScript;

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
            _isUpdatingDocument = false;
            return;
        }
        
        foreach (var cmd in script.Commands)
            Commands.Add(cmd);

        var text = ScriptTextConverter.ToText(script);
        _isUpdatingDocument = true;
        Document.Text = text;
        _isUpdatingDocument = false;
    }

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingDocument) return;
        
        ScriptText = Document.Text;
    }

    [RelayCommand(CanExecute = nameof(HasScript))]
    private async Task AddSleepCommandAsync()
    {
        if (CurrentScript == null) return;
        if (!await TryApplyScriptTextInternalAsync()) return;

        var dlg = new InputDialog(
            "Add Sleep",
            "輸入延遲秒數，例如：0.05 代表 50ms。",
            "Seconds:",
            "0.25");
        dlg.Owner = System.Windows.Application.Current?.MainWindow;

        if (dlg.ShowDialog() != true)
            return;

        if (!double.TryParse(dlg.ValueText, out var seconds) || seconds < 0)
            return;

        var delay = TimeSpan.FromSeconds(seconds);
        CurrentScript.AddCommand(new SleepCommand(delay));
        await PersistAndReloadAsync("Added sleep command");
    }

    [RelayCommand(CanExecute = nameof(HasScript))]
    private async Task AddKeyboardTextAsync()
    {
        if (CurrentScript == null) return;
        if (!await TryApplyScriptTextInternalAsync()) return;

        var dlg = new InputDialog(
            "Add Keyboard Text",
            "輸入要自動輸出的文字（會插入 KeyboardCommand）。",
            "Text:",
            "");
        dlg.Owner = System.Windows.Application.Current?.MainWindow;

        if (dlg.ShowDialog() != true)
            return;

        var text = dlg.ValueText ?? string.Empty;
        CurrentScript.AddCommand(new KeyboardCommand(text));
        await PersistAndReloadAsync("Added keyboard text command");
    }

    [RelayCommand(CanExecute = nameof(HasScript))]
    private async Task AddMouseMoveAsync()
    {
        if (CurrentScript == null) return;
        if (!await TryApplyScriptTextInternalAsync()) return;

        var picker = new PickPointWindow
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        // 使用 PickPointWindow，讓使用者移動滑鼠並按 F2 來決定座標
        if (picker.ShowDialog() != true)
            return;

        var position = await _inputSimulator.GetCursorPositionAsync();
        CurrentScript.AddCommand(new MouseMoveCommand(position));
        await PersistAndReloadAsync("Added mouse move command");
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
        if (!await TryApplyScriptTextInternalAsync()) return;
        if (CurrentScript.CommandCount == 0) return;
        CurrentScript.RemoveCommandAt(CurrentScript.CommandCount - 1);
        await PersistAndReloadAsync("Removed last command");
    }

    [RelayCommand(CanExecute = nameof(HasScript))]
    private async Task ApplyScriptTextAsync()
    {
        if (CurrentScript == null) return;

        try
        {
            var text = Document.Text;
            var commands = ScriptTextConverter.Parse(text);

            CurrentScript.ClearCommands();
            foreach (var cmd in commands)
            {
                CurrentScript.AddCommand(cmd);
            }

            await PersistAndReloadAsync("Updated script from text");
        }
        catch (FormatException ex)
        {
            await _loggingService.LogErrorAsync("Failed to parse script text", ex, new Dictionary<string, object>
            {
                { "ScriptId", CurrentScript.Id },
                { "ScriptName", CurrentScript.Name }
            });
        }
    }

    private bool HasScript() => CurrentScript != null;

    private async Task<bool> TryApplyScriptTextInternalAsync()
    {
        if (CurrentScript == null) return false;

        try
        {
            var text = Document.Text;
            var commands = ScriptTextConverter.Parse(text);
            CurrentScript.ClearCommands();
            foreach (var cmd in commands)
            {
                CurrentScript.AddCommand(cmd);
            }

            await _scriptManager.UpdateScriptAsync(CurrentScript);
            return true;
        }
        catch (FormatException ex)
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
    }

    private async Task AddClickSequenceAsync(MouseButton button)
    {
        if (CurrentScript == null) return;
        if (!await TryApplyScriptTextInternalAsync()) return;

        // Click will be performed at current cursor position when executed.
        var down = new MouseClickCommand(button, ClickType.Down);
        var sleep = new SleepCommand(TimeSpan.FromMilliseconds(50));
        var up = new MouseClickCommand(button, ClickType.Up);
        CurrentScript.AddCommand(down);
        CurrentScript.AddCommand(sleep);
        CurrentScript.AddCommand(up);
        await PersistAndReloadAsync($"Added {button} click sequence");
    }
}


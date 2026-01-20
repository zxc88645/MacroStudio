using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Presentation.ViewModels;
using MacroStudio.Presentation.Views;
using Microsoft.Win32;
using System.Collections.ObjectModel;

namespace MacroStudio.Presentation.ViewModels;

/// <summary>
/// ViewModel for listing, searching, and basic CRUD operations for scripts.
/// </summary>
public partial class ScriptListViewModel : ObservableObject
{
    private readonly IScriptManager _scriptManager;
    private readonly ILoggingService _loggingService;

    public ObservableCollection<Script> Scripts { get; } = new();

    [ObservableProperty]
    private Script? selectedScript;

    [ObservableProperty]
    private string searchText = string.Empty;

    public event EventHandler<Script?>? SelectedScriptChanged;

    public ScriptListViewModel(IScriptManager scriptManager, ILoggingService loggingService)
    {
        _scriptManager = scriptManager ?? throw new ArgumentNullException(nameof(scriptManager));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        Scripts.Clear();
        var scripts = await _scriptManager.GetAllScriptsAsync();
        foreach (var s in scripts.OrderBy(s => s.Name))
            Scripts.Add(s);
    }

    [RelayCommand]
    private async Task CreateScriptAsync()
    {
        // Generate a unique 'New Script', 'New Script (2)', ... name to avoid crashes.
        var baseName = "New Script";
        var name = baseName;
        var counter = 1;
        while (!await _scriptManager.IsValidScriptNameAsync(name))
        {
            counter++;
            name = $"{baseName} ({counter})";
        }

        var script = await _scriptManager.CreateScriptAsync(name);
        Scripts.Add(script);
        SelectedScript = script;
        await _loggingService.LogInfoAsync("Script created", new Dictionary<string, object> { { "ScriptId", script.Id } });
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedScript == null) return;
        var id = SelectedScript.Id;
        await _scriptManager.DeleteScriptAsync(id);
        Scripts.Remove(SelectedScript);
        SelectedScript = Scripts.FirstOrDefault();
        await _loggingService.LogWarningAsync("Script deleted", new Dictionary<string, object> { { "ScriptId", id } });
    }

    private bool CanDeleteSelected() => SelectedScript != null;

    [RelayCommand]
    private async Task ImportAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import Script",
            Filter = "MacroStudio Script (*.json)|*.json|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dlg.ShowDialog() != true)
            return;

        var imported = await _scriptManager.ImportScriptAsync(dlg.FileName);
        await RefreshAsync();
        SelectedScript = Scripts.FirstOrDefault(s => s.Id == imported.Id) ?? Scripts.FirstOrDefault();
        await _loggingService.LogInfoAsync("Script imported", new Dictionary<string, object>
        {
            { "ScriptId", imported.Id },
            { "FilePath", dlg.FileName }
        });
    }

    [RelayCommand(CanExecute = nameof(CanExportSelected))]
    private async Task ExportSelectedAsync()
    {
        if (SelectedScript == null) return;

        var dlg = new SaveFileDialog
        {
            Title = "Export Script",
            Filter = "MacroStudio Script (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"{SelectedScript.Name}.json"
        };

        if (dlg.ShowDialog() != true)
            return;

        await _scriptManager.ExportScriptAsync(SelectedScript.Id, dlg.FileName);
        await _loggingService.LogInfoAsync("Script exported", new Dictionary<string, object>
        {
            { "ScriptId", SelectedScript.Id },
            { "FilePath", dlg.FileName }
        });
    }

    private bool CanExportSelected() => SelectedScript != null;

    [RelayCommand]
    private async Task ApplySearchAsync()
    {
        var term = SearchText?.Trim() ?? string.Empty;
        Scripts.Clear();
        var results = string.IsNullOrWhiteSpace(term)
            ? await _scriptManager.GetAllScriptsAsync()
            : await _scriptManager.SearchScriptsAsync(term);

        foreach (var s in results.OrderBy(s => s.Name))
            Scripts.Add(s);
    }

    [RelayCommand(CanExecute = nameof(CanSetHotkey))]
    private async Task SetHotkeyAsync()
    {
        if (SelectedScript == null) return;

        // Get MainViewModel to disable hotkey triggering during capture
        var mainWindow = System.Windows.Application.Current?.MainWindow;
        var mainVm = mainWindow?.DataContext as MainViewModel;
        mainVm?.SetHotkeyCaptureActive(true);

        try
        {
            var dialog = new HotkeyCaptureWindow(SelectedScript.TriggerHotkey)
            {
                Owner = mainWindow
            };

            if (dialog.ShowDialog() == true && dialog.ResultHotkey != null)
            {
                var scriptId = SelectedScript.Id;
                SelectedScript.TriggerHotkey = dialog.ResultHotkey;
                await _scriptManager.UpdateScriptAsync(SelectedScript);
                
                // Script is a domain entity (no INotifyPropertyChanged), so changing TriggerHotkey won't
                // automatically refresh the ListBox item template. The most reliable fix is to run the same
                // reload path as the "Refresh" button (clear + reload from storage), then restore selection.
                await RefreshAsync();
                SelectedScript = Scripts.FirstOrDefault(s => s.Id == scriptId) ?? Scripts.FirstOrDefault();
                
                // Selection might be null if there are no scripts loaded; avoid null dereference.
                var selected = SelectedScript;

                await _loggingService.LogInfoAsync("Script hotkey updated", new Dictionary<string, object>
                {
                    { "ScriptId", selected?.Id.ToString() ?? string.Empty },
                    { "ScriptName", selected?.Name ?? string.Empty },
                    { "Hotkey", dialog.ResultHotkey.GetDisplayString() }
                });
            }
        }
        finally
        {
            // Re-enable hotkey triggering
            mainVm?.SetHotkeyCaptureActive(false);
        }
    }

    private bool CanSetHotkey() => SelectedScript != null;

    partial void OnSelectedScriptChanged(Script? oldValue, Script? newValue)
    {
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        ExportSelectedCommand.NotifyCanExecuteChanged();
        SetHotkeyCommand.NotifyCanExecuteChanged();
        SelectedScriptChanged?.Invoke(this, newValue);
    }
}


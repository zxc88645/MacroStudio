using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MacroStudio.Application.Services;

/// <summary>
/// Application service for managing automation scripts.
/// Handles CRUD operations, validation, and persistence.
/// </summary>
public class ScriptManager : IScriptManager
{
    private readonly IFileStorageService _storageService;
    private readonly IScriptHotkeyHookService _scriptHotkeyHookService;
    private readonly ILogger<ScriptManager> _logger;
    private readonly Dictionary<Guid, Script> _scriptCache;
    private readonly Dictionary<Guid, HotkeyDefinition> _registeredHotkeys = new();
    private readonly object _cacheLock = new();

    /// <summary>
    /// Initializes a new instance of the ScriptManager class.
    /// </summary>
    /// <param name="storageService">File storage service for persistence.</param>
    /// <param name="scriptHotkeyHookService">Hook-based hotkey service for script trigger hotkeys.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public ScriptManager(IFileStorageService storageService, IScriptHotkeyHookService scriptHotkeyHookService, ILogger<ScriptManager> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _scriptHotkeyHookService = scriptHotkeyHookService ?? throw new ArgumentNullException(nameof(scriptHotkeyHookService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scriptCache = new Dictionary<Guid, Script>();

        _logger.LogDebug("ScriptManager initialized");
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Script>> GetAllScriptsAsync()
    {
        try
        {
            _logger.LogDebug("Loading all scripts from storage");

            var scripts = await _storageService.LoadScriptsAsync();
            var scriptList = scripts.ToList();

            // Update cache
            lock (_cacheLock)
            {
                _scriptCache.Clear();
                foreach (var script in scriptList)
                {
                    _scriptCache[script.Id] = script;
                }
            }

            _logger.LogInformation("Loaded {Count} scripts", scriptList.Count);
            return scriptList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading all scripts");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Script?> GetScriptAsync(Guid id)
    {
        try
        {
            _logger.LogDebug("Getting script {ScriptId}", id);

            // Check cache first
            lock (_cacheLock)
            {
                if (_scriptCache.TryGetValue(id, out var cachedScript))
                {
                    _logger.LogDebug("Script {ScriptId} found in cache", id);
                    return cachedScript;
                }
            }

            // Load from storage if not in cache
            var allScripts = await GetAllScriptsAsync();
            var script = allScripts.FirstOrDefault(s => s.Id == id);

            if (script != null)
            {
                _logger.LogDebug("Script {ScriptId} found in storage", id);
            }
            else
            {
                _logger.LogWarning("Script {ScriptId} not found", id);
            }

            return script;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting script {ScriptId}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Script> CreateScriptAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Script name cannot be null or empty.", nameof(name));

        try
        {
            _logger.LogDebug("Creating new script with name: {ScriptName}", name);

            // Validate name uniqueness
            var isValid = await IsValidScriptNameAsync(name);
            if (!isValid)
            {
                throw new InvalidOperationException($"A script with the name '{name}' already exists.");
            }

            // Create new script
            var script = new Script(name);

            // Save to storage immediately
            await _storageService.SaveScriptAsync(script);

            // Update cache
            lock (_cacheLock)
            {
                _scriptCache[script.Id] = script;
            }

            _logger.LogInformation("Created new script {ScriptId} with name: {ScriptName}", script.Id, name);
            return script;
        }
        catch (Exception ex) when (!(ex is ArgumentException || ex is InvalidOperationException))
        {
            _logger.LogError(ex, "Error creating script with name: {ScriptName}", name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateScriptAsync(Script script)
    {
        if (script == null)
            throw new ArgumentNullException(nameof(script));

        try
        {
            _logger.LogDebug("Updating script {ScriptId}", script.Id);

            // Verify script exists
            var existingScript = await GetScriptAsync(script.Id);
            if (existingScript == null)
            {
                throw new InvalidOperationException($"Script with ID {script.Id} does not exist.");
            }

            // Validate script
            var validationResult = ValidateScript(script);
            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors);
                throw new InvalidOperationException($"Script validation failed: {errors}");
            }

            // Update cached hotkey mapping if it changed
            HotkeyDefinition? oldHotkeyToUnregister = null;
            bool hotkeyChanged = false;
            lock (_cacheLock)
            {
                if (_registeredHotkeys.TryGetValue(script.Id, out var oldHotkey))
                {
                    // Compare by Modifiers, Key, and TriggerMode
                    hotkeyChanged = script.TriggerHotkey == null ||
                                    oldHotkey.Modifiers != script.TriggerHotkey.Modifiers ||
                                    oldHotkey.Key != script.TriggerHotkey.Key ||
                                    oldHotkey.TriggerMode != script.TriggerHotkey.TriggerMode;

                    if (hotkeyChanged)
                    {
                        oldHotkeyToUnregister = oldHotkey;
                    }
                }
            }

            // Remove old hotkey from mapping outside the lock
            if (hotkeyChanged && oldHotkeyToUnregister != null)
            {
                lock (_cacheLock) { _registeredHotkeys.Remove(script.Id); }
                _logger.LogInformation("Removed old hotkey mapping for script {ScriptId}: {Hotkey}", script.Id, oldHotkeyToUnregister);
            }

            // Save to storage immediately
            await _storageService.SaveScriptAsync(script);

            // Update cache
            lock (_cacheLock)
            {
                _scriptCache[script.Id] = script;
            }

            // Update mapping with new hotkey if specified
            if (script.TriggerHotkey != null)
            {
                lock (_cacheLock) { _registeredHotkeys[script.Id] = script.TriggerHotkey; }
                _logger.LogInformation("Updated trigger hotkey mapping for script {ScriptId}: {Hotkey}", script.Id, script.TriggerHotkey);
            }

            // Apply all mappings to hook service (atomic replace).
            lock (_cacheLock)
            {
                _scriptHotkeyHookService.SetScriptHotkeys(new Dictionary<Guid, HotkeyDefinition>(_registeredHotkeys));
            }

            _logger.LogInformation("Updated script {ScriptId}", script.Id);
        }
        catch (Exception ex) when (!(ex is ArgumentNullException || ex is InvalidOperationException))
        {
            _logger.LogError(ex, "Error updating script {ScriptId}", script.Id);
            throw;
        }
    }

    /// <summary>
    /// Registers hotkeys for all scripts that have trigger hotkeys defined.
    /// </summary>
    public async Task RegisterAllScriptHotkeysAsync()
    {
        try
        {
            var scripts = await GetAllScriptsAsync();
            var hotkeys = new Dictionary<Guid, HotkeyDefinition>();
            foreach (var script in scripts)
            {
                if (script.TriggerHotkey != null)
                {
                    hotkeys[script.Id] = script.TriggerHotkey;
                }
            }
            lock (_cacheLock)
            {
                _registeredHotkeys.Clear();
                foreach (var kv in hotkeys) _registeredHotkeys[kv.Key] = kv.Value;
            }
            _scriptHotkeyHookService.SetScriptHotkeys(hotkeys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering script hotkeys");
        }
    }

    private void ApplyHotkeyMappingsToHook()
    {
        lock (_cacheLock)
        {
            _scriptHotkeyHookService.SetScriptHotkeys(new Dictionary<Guid, HotkeyDefinition>(_registeredHotkeys));
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteScriptAsync(Guid id)
    {
        try
        {
            _logger.LogDebug("Deleting script {ScriptId}", id);

            var deleted = await _storageService.DeleteScriptAsync(id);

            if (deleted)
            {
                // Remove from cache and tracking
                lock (_cacheLock)
                {
                    _scriptCache.Remove(id);
                    _registeredHotkeys.Remove(id);
                }
                ApplyHotkeyMappingsToHook();

                _logger.LogInformation("Deleted script {ScriptId}", id);
            }
            else
            {
                _logger.LogWarning("Script {ScriptId} not found for deletion", id);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting script {ScriptId}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Script> DuplicateScriptAsync(Guid id, string? newName = null)
    {
        try
        {
            _logger.LogDebug("Duplicating script {ScriptId}", id);

            var sourceScript = await GetScriptAsync(id);
            if (sourceScript == null)
            {
                throw new InvalidOperationException($"Script with ID {id} does not exist.");
            }

            // Generate new name if not provided
            if (string.IsNullOrWhiteSpace(newName))
            {
                var baseName = sourceScript.Name;
                var counter = 1;
                newName = $"{baseName} (Copy)";

                // Ensure unique name
                while (!await IsValidScriptNameAsync(newName))
                {
                    counter++;
                    newName = $"{baseName} (Copy {counter})";
                }
            }
            else
            {
                // Validate provided name
                if (!await IsValidScriptNameAsync(newName))
                {
                    throw new InvalidOperationException($"A script with the name '{newName}' already exists.");
                }
            }

            // Create duplicate using Script.Duplicate method
            var duplicatedScript = sourceScript.Duplicate(newName);

            // Save to storage immediately
            await _storageService.SaveScriptAsync(duplicatedScript);

            // Update cache
            lock (_cacheLock)
            {
                _scriptCache[duplicatedScript.Id] = duplicatedScript;
            }

            _logger.LogInformation("Duplicated script {SourceId} to {NewId} with name: {NewName}",
                id, duplicatedScript.Id, newName);

            return duplicatedScript;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            _logger.LogError(ex, "Error duplicating script {ScriptId}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RenameScriptAsync(Guid id, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Script name cannot be null or empty.", nameof(newName));

        try
        {
            _logger.LogDebug("Renaming script {ScriptId} to '{NewName}'", id, newName);

            var script = await GetScriptAsync(id);
            if (script == null)
            {
                throw new InvalidOperationException($"Script with ID {id} does not exist.");
            }

            // Ensure the new name is valid and unique, excluding the script being renamed.
            var isValidName = await IsValidScriptNameAsync(newName, excludeId: id);
            if (!isValidName)
            {
                throw new InvalidOperationException($"A script with the name '{newName}' already exists.");
            }

            script.Name = newName.Trim();

            // Reuse the existing update pipeline so validation, persistence, cache,
            // and hotkey mappings all stay consistent.
            await UpdateScriptAsync(script);

            _logger.LogInformation("Renamed script {ScriptId} to '{NewName}'", id, script.Name);
        }
        catch (Exception ex) when (!(ex is ArgumentException || ex is InvalidOperationException))
        {
            _logger.LogError(ex, "Error renaming script {ScriptId} to '{NewName}'", id, newName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsValidScriptNameAsync(string name, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        try
        {
            var allScripts = await GetAllScriptsAsync();
            var trimmedName = name.Trim();

            return !allScripts.Any(s =>
                s.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase) &&
                (!excludeId.HasValue || s.Id != excludeId.Value));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating script name: {ScriptName}", name);
            throw;
        }
    }

    /// <inheritdoc />
    public ScriptValidationResult ValidateScript(Script script)
    {
        if (script == null)
            throw new ArgumentNullException(nameof(script));

        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate name
        if (string.IsNullOrWhiteSpace(script.Name))
        {
            errors.Add("Script name cannot be null or empty.");
        }
        else if (script.Name.Length > 200)
        {
            errors.Add("Script name cannot exceed 200 characters.");
        }

        // Validate SourceText (primary representation for execution)
        if (string.IsNullOrWhiteSpace(script.SourceText))
        {
            warnings.Add("Script has no source text. It may not be executable.");
        }

        // Check source text length (rough indicator of script complexity)
        if (script.SourceTextLength > 100_000)
        {
            warnings.Add($"Script has a very long source text ({script.SourceTextLength} characters). Consider breaking it into smaller scripts.");
        }

        return errors.Count > 0
            ? ScriptValidationResult.Failure(errors, warnings)
            : ScriptValidationResult.Success(warnings);
    }

    /// <inheritdoc />
    public async Task<int> GetScriptCountAsync()
    {
        try
        {
            var allScripts = await GetAllScriptsAsync();
            return allScripts.Count();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting script count");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Script>> SearchScriptsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Array.Empty<Script>();

        try
        {
            _logger.LogDebug("Searching scripts with term: {SearchTerm}", searchTerm);

            var allScripts = await GetAllScriptsAsync();
            var trimmedTerm = searchTerm.Trim();

            var matchingScripts = allScripts
                .Where(s => s.Name.Contains(trimmedTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();

            _logger.LogDebug("Found {Count} scripts matching search term: {SearchTerm}", matchingScripts.Count, searchTerm);

            return matchingScripts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching scripts with term: {SearchTerm}", searchTerm);
            throw;
        }
    }

    public async Task<Script> ImportScriptAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        _logger.LogInformation("Importing script from {FilePath}", filePath);
        var imported = await _storageService.ImportScriptAsync(filePath);

        // Ensure name uniqueness (append if needed)
        var name = imported.Name;
        var counter = 1;
        while (!await IsValidScriptNameAsync(name, excludeId: imported.Id))
        {
            counter++;
            name = $"{imported.Name} (Imported {counter})";
        }

        if (name != imported.Name)
            imported.Name = name;

        await _storageService.SaveScriptAsync(imported);

        lock (_cacheLock)
        {
            _scriptCache[imported.Id] = imported;
        }

        return imported;
    }

    public async Task ExportScriptAsync(Guid id, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        var script = await GetScriptAsync(id);
        if (script == null)
            throw new InvalidOperationException($"Script with ID {id} does not exist.");

        _logger.LogInformation("Exporting script {ScriptId} to {FilePath}", id, filePath);
        await _storageService.ExportScriptAsync(script, filePath);
    }
}

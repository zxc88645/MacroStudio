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
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ILogger<ScriptManager> _logger;
    private readonly Dictionary<Guid, Script> _scriptCache;
    private readonly Dictionary<Guid, HotkeyDefinition> _registeredHotkeys = new();
    private readonly object _cacheLock = new();

    /// <summary>
    /// Initializes a new instance of the ScriptManager class.
    /// </summary>
    /// <param name="storageService">File storage service for persistence.</param>
    /// <param name="hotkeyService">Global hotkey service for script trigger hotkeys.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public ScriptManager(IFileStorageService storageService, IGlobalHotkeyService hotkeyService, ILogger<ScriptManager> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
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

            // Unregister old hotkey if it changed
            // First, check if hotkey changed and get the old hotkey (inside lock)
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

            // Unregister old hotkey outside the lock to avoid deadlock
            if (hotkeyChanged && oldHotkeyToUnregister != null)
            {
                try
                {
                    await _hotkeyService.UnregisterHotkeyAsync(oldHotkeyToUnregister);
                    lock (_cacheLock)
                    {
                        _registeredHotkeys.Remove(script.Id);
                    }
                    _logger.LogInformation("Unregistered old hotkey for script {ScriptId}: {Hotkey}", script.Id, oldHotkeyToUnregister);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to unregister old hotkey for script {ScriptId}", script.Id);
                }
            }

            // Save to storage immediately
            await _storageService.SaveScriptAsync(script);

            // Update cache
            lock (_cacheLock)
            {
                _scriptCache[script.Id] = script;
            }

            // Register new hotkey if specified
            if (script.TriggerHotkey != null)
            {
                try
                {
                    await _hotkeyService.RegisterHotkeyAsync(script.TriggerHotkey);
                    lock (_cacheLock)
                    {
                        _registeredHotkeys[script.Id] = script.TriggerHotkey;
                    }
                    _logger.LogInformation("Registered trigger hotkey for script {ScriptId}: {Hotkey}", script.Id, script.TriggerHotkey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to register trigger hotkey for script {ScriptId}", script.Id);
                }
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
            foreach (var script in scripts)
            {
                if (script.TriggerHotkey != null)
                {
                    try
                    {
                        await _hotkeyService.RegisterHotkeyAsync(script.TriggerHotkey);
                        lock (_cacheLock)
                        {
                            _registeredHotkeys[script.Id] = script.TriggerHotkey;
                        }
                        _logger.LogDebug("Registered trigger hotkey for script {ScriptId}: {Hotkey}", script.Id, script.TriggerHotkey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to register trigger hotkey for script {ScriptId}", script.Id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering script hotkeys");
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteScriptAsync(Guid id)
    {
        try
        {
            _logger.LogDebug("Deleting script {ScriptId}", id);

            // Get script before deletion to check for hotkey
            // Try cache first, then load from storage if not in cache
            Script? scriptToDelete = null;
            lock (_cacheLock)
            {
                _scriptCache.TryGetValue(id, out scriptToDelete);
            }

            // If not in cache, try to load from storage to get hotkey info
            if (scriptToDelete == null)
            {
                try
                {
                    scriptToDelete = await GetScriptAsync(id);
                }
                catch
                {
                    // Script might not exist, continue with deletion attempt
                }
            }

            // Determine which hotkey to unregister
            // Priority: 1) Check _registeredHotkeys (most reliable), 2) Check script's TriggerHotkey
            HotkeyDefinition? hotkeyToUnregister = null;
            
            // First, check if we have it tracked in _registeredHotkeys
            lock (_cacheLock)
            {
                _logger.LogWarning("Deleting script {ScriptId}. Checking for hotkey to unregister...", id);
                _logger.LogWarning("  _registeredHotkeys count: {Count}", _registeredHotkeys.Count);
                foreach (var kvp in _registeredHotkeys)
                {
                    _logger.LogWarning("  Tracked: ScriptId={ScriptId}, Hotkey={Hotkey}", kvp.Key, kvp.Value);
                }
                
                if (_registeredHotkeys.TryGetValue(id, out hotkeyToUnregister))
                {
                    // Found in tracking dictionary, use it
                    _logger.LogWarning("Found hotkey in tracking dictionary for script {ScriptId}: {Hotkey}", id, hotkeyToUnregister);
                }
                else if (scriptToDelete?.TriggerHotkey != null)
                {
                    // Not in tracking but script has hotkey (might be registered by MainViewModel)
                    // Use the script's hotkey definition
                    hotkeyToUnregister = scriptToDelete.TriggerHotkey;
                    _logger.LogWarning("Hotkey not in tracking, using script's TriggerHotkey for script {ScriptId}: {Hotkey}", id, hotkeyToUnregister);
                }
                else
                {
                    _logger.LogWarning("No hotkey found for script {ScriptId} (not in tracking and script has no TriggerHotkey)", id);
                }
            }

            // Unregister the hotkey if we found one
            if (hotkeyToUnregister != null)
            {
                _logger.LogWarning("Attempting to unregister hotkey for deleted script {ScriptId}: {Hotkey} (Modifiers={Modifiers}, Key={Key}, TriggerMode={TriggerMode})", 
                    id, hotkeyToUnregister, hotkeyToUnregister.Modifiers, hotkeyToUnregister.Key, hotkeyToUnregister.TriggerMode);
                
                // Verify hotkey is registered before attempting unregister
                var wasRegisteredBefore = await _hotkeyService.IsHotkeyRegisteredAsync(hotkeyToUnregister);
                _logger.LogWarning("Hotkey registration status before unregister for script {ScriptId}: {IsRegistered}", id, wasRegisteredBefore);
                
                try
                {
                    await _hotkeyService.UnregisterHotkeyAsync(hotkeyToUnregister);
                    
                    // Verify hotkey was actually unregistered
                    var isRegisteredAfter = await _hotkeyService.IsHotkeyRegisteredAsync(hotkeyToUnregister);
                    _logger.LogWarning("Hotkey registration status after unregister for script {ScriptId}: {IsRegistered}", id, isRegisteredAfter);
                    
                    if (isRegisteredAfter)
                    {
                        _logger.LogError("CRITICAL: Hotkey {Hotkey} is still registered after unregister attempt for script {ScriptId}!", hotkeyToUnregister, id);
                        
                        // Try to get all registered hotkeys to see what's still there
                        var allRegistered = await _hotkeyService.GetRegisteredHotkeysAsync();
                        _logger.LogWarning("Currently registered hotkeys after failed unregister: {Count}", allRegistered.Count());
                        foreach (var h in allRegistered)
                        {
                            _logger.LogWarning("  Still registered: {Hotkey} (Modifiers={Modifiers}, Key={Key}, TriggerMode={TriggerMode})", 
                                h, h.Modifiers, h.Key, h.TriggerMode);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Confirmed: Hotkey {Hotkey} was successfully unregistered for script {ScriptId}", hotkeyToUnregister, id);
                    }
                    
                    lock (_cacheLock)
                    {
                        _registeredHotkeys.Remove(id);
                    }
                    _logger.LogWarning("Successfully unregistered hotkey for deleted script {ScriptId}: {Hotkey}", id, hotkeyToUnregister);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "First attempt failed to unregister hotkey for script {ScriptId}: {Error}. Hotkey: {Hotkey}", 
                        id, ex.Message, hotkeyToUnregister);
                    
                    // If unregistration failed, try to find the hotkey by Modifiers/Key/TriggerMode
                    // This handles cases where the HotkeyDefinition instance is different
                    try
                    {
                        _logger.LogDebug("Attempting fallback: getting all registered hotkeys to find match");
                        var allRegistered = await _hotkeyService.GetRegisteredHotkeysAsync();
                        _logger.LogDebug("Found {Count} registered hotkeys", allRegistered.Count());
                        
                        var matchingHotkey = allRegistered.FirstOrDefault(h => 
                            h.Modifiers == hotkeyToUnregister.Modifiers && 
                            h.Key == hotkeyToUnregister.Key &&
                            h.TriggerMode == hotkeyToUnregister.TriggerMode);
                        
                        if (matchingHotkey != null)
                        {
                            _logger.LogInformation("Found matching hotkey by Modifiers/Key/TriggerMode: {MatchingHotkey}, attempting unregister again", matchingHotkey);
                            await _hotkeyService.UnregisterHotkeyAsync(matchingHotkey);
                            _logger.LogInformation("Successfully unregistered hotkey for deleted script {ScriptId} using fallback method", id);
                        }
                        else
                        {
                            _logger.LogWarning("No matching hotkey found by Modifiers/Key/TriggerMode for script {ScriptId}. Searched {Count} registered hotkeys", 
                                id, allRegistered.Count());
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogWarning(fallbackEx, "Fallback unregistration also failed for script {ScriptId}", id);
                    }
                    
                    // Still remove from tracking even if unregistration failed
                    lock (_cacheLock)
                    {
                        _registeredHotkeys.Remove(id);
                    }
                }
            }
            else if (scriptToDelete?.TriggerHotkey != null)
            {
                // Even if not in tracking, try to unregister using the script's hotkey
                // This handles edge cases where hotkey was registered but not tracked
                _logger.LogDebug("Hotkey not in tracking but script has hotkey, attempting unregister anyway");
                try
                {
                    await _hotkeyService.UnregisterHotkeyAsync(scriptToDelete.TriggerHotkey);
                    _logger.LogInformation("Successfully unregistered untracked hotkey for deleted script {ScriptId}", id);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not unregister hotkey for script {ScriptId} (may not have been registered)", id);
                }
            }

            var deleted = await _storageService.DeleteScriptAsync(id);

            if (deleted)
            {
                // Remove from cache and tracking
                lock (_cacheLock)
                {
                    _scriptCache.Remove(id);
                    _registeredHotkeys.Remove(id); // Ensure removal even if not found above
                }

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

        // Validate commands
        if (script.CommandCount == 0)
        {
            warnings.Add("Script contains no commands.");
        }

        // Validate each command
        for (int i = 0; i < script.CommandCount; i++)
        {
            var command = script.GetCommand(i);
            if (!command.IsValid())
            {
                errors.Add($"Command at index {i} is invalid: {command.Description}");
            }
        }

        // Check for very large scripts
        if (script.CommandCount > 10000)
        {
            warnings.Add($"Script contains a very large number of commands ({script.CommandCount}). Execution may be slow.");
        }

        // Check estimated duration
        if (script.EstimatedDuration > TimeSpan.FromHours(1))
        {
            warnings.Add($"Script has a very long estimated duration ({script.EstimatedDuration}). Consider breaking it into smaller scripts.");
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

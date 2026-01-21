using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MacroStudio.Infrastructure.Storage;

/// <summary>
/// JSON-based file storage service implementation.
/// Handles serialization, deserialization, and versioned schema support.
/// </summary>
public class JsonFileStorageService : IFileStorageService
{
    private readonly ILogger<JsonFileStorageService> _logger;
    private readonly string _storageDirectory;
    private readonly string _scriptsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the JsonFileStorageService class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="storageDirectory">
    /// Optional base directory for storage (primarily for tests). If null, defaults to LocalApplicationData\\MacroStudio.
    /// </param>
    public JsonFileStorageService(ILogger<JsonFileStorageService> logger, string? storageDirectory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Use LocalApplicationData for storage unless an override is provided (tests).
        if (string.IsNullOrWhiteSpace(storageDirectory))
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _storageDirectory = Path.Combine(appDataPath, "MacroStudio");
        }
        else
        {
            _storageDirectory = storageDirectory;
        }
        _scriptsFilePath = Path.Combine(_storageDirectory, "scripts.json");

        // Ensure storage directory exists
        Directory.CreateDirectory(_storageDirectory);

        // Configure JSON serialization options
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new CommandJsonConverter(),
                new TimeSpanJsonConverter(),
                new DateTimeJsonConverter()
            }
        };

        _logger.LogDebug("JsonFileStorageService initialized. Storage path: {StoragePath}", _scriptsFilePath);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Script>> LoadScriptsAsync()
    {
        try
        {
            if (!File.Exists(_scriptsFilePath))
            {
                _logger.LogInformation("Scripts file does not exist. Returning empty collection.");
                return Array.Empty<Script>();
            }

            _logger.LogDebug("Loading scripts from {FilePath}", _scriptsFilePath);

            var jsonContent = await File.ReadAllTextAsync(_scriptsFilePath);
            
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogWarning("Scripts file is empty. Returning empty collection.");
                return Array.Empty<Script>();
            }

            var storageModel = JsonSerializer.Deserialize<ScriptStorageModel>(jsonContent, _jsonOptions);
            
            if (storageModel == null)
            {
                _logger.LogWarning("Failed to deserialize scripts file. Returning empty collection.");
                return Array.Empty<Script>();
            }

            // Handle schema migration if needed
            var migratedModel = MigrateSchema(storageModel);

            var scripts = migratedModel.Scripts.Select(s => ConvertToScript(s)).ToList();
            
            _logger.LogInformation("Loaded {Count} scripts from storage", scripts.Count);
            
            return scripts;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error while loading scripts");
            throw new StorageException($"Failed to parse scripts file: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading scripts from storage");
            throw new StorageException($"Failed to load scripts: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task SaveScriptAsync(Script script)
    {
        if (script == null)
            throw new ArgumentNullException(nameof(script));

        try
        {
            _logger.LogDebug("Saving script {ScriptId} ({ScriptName})", script.Id, script.Name);

            // Load all existing scripts
            var allScripts = (await LoadScriptsAsync()).ToList();
            
            // Remove the script if it already exists, then add the updated version
            var existingIndex = allScripts.FindIndex(s => s.Id == script.Id);
            if (existingIndex >= 0)
            {
                allScripts[existingIndex] = script;
                _logger.LogDebug("Updated existing script {ScriptId}", script.Id);
            }
            else
            {
                allScripts.Add(script);
                _logger.LogDebug("Added new script {ScriptId}", script.Id);
            }

            // Convert to storage model
            var storageModel = new ScriptStorageModel
            {
                Version = "1.0",
                Scripts = allScripts.Select(ConvertToScriptDto).ToList()
            };

            // Serialize and save
            var jsonContent = JsonSerializer.Serialize(storageModel, _jsonOptions);
            await File.WriteAllTextAsync(_scriptsFilePath, jsonContent);

            _logger.LogDebug("Successfully saved script {ScriptId}", script.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving script {ScriptId}", script.Id);
            throw new StorageException($"Failed to save script: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteScriptAsync(Guid id)
    {
        try
        {
            _logger.LogDebug("Deleting script {ScriptId}", id);

            var allScripts = (await LoadScriptsAsync()).ToList();
            var scriptToDelete = allScripts.FirstOrDefault(s => s.Id == id);

            if (scriptToDelete == null)
            {
                _logger.LogWarning("Script {ScriptId} not found for deletion", id);
                return false;
            }

            allScripts.Remove(scriptToDelete);

            // Save updated collection
            var storageModel = new ScriptStorageModel
            {
                Version = "1.0",
                Scripts = allScripts.Select(ConvertToScriptDto).ToList()
            };

            var jsonContent = JsonSerializer.Serialize(storageModel, _jsonOptions);
            await File.WriteAllTextAsync(_scriptsFilePath, jsonContent);

            _logger.LogInformation("Successfully deleted script {ScriptId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting script {ScriptId}", id);
            throw new StorageException($"Failed to delete script: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Script> ImportScriptAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Script file not found: {filePath}", filePath);

        try
        {
            _logger.LogDebug("Importing script from {FilePath}", filePath);

            var jsonContent = await File.ReadAllTextAsync(filePath);
            var storageModel = JsonSerializer.Deserialize<ScriptStorageModel>(jsonContent, _jsonOptions);

            if (storageModel == null || storageModel.Scripts.Count == 0)
            {
                throw new StorageException("Imported file does not contain any valid scripts.");
            }

            // Handle schema migration
            var migratedModel = MigrateSchema(storageModel);

            // Import the first script (for single script import)
            var scriptDto = migratedModel.Scripts.First();
            var script = ConvertToScript(scriptDto);

            _logger.LogInformation("Successfully imported script {ScriptId} ({ScriptName})", script.Id, script.Name);
            
            return script;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error while importing script from {FilePath}", filePath);
            throw new StorageException($"Failed to parse script file: {ex.Message}", ex);
        }
        catch (Exception ex) when (!(ex is StorageException))
        {
            _logger.LogError(ex, "Error importing script from {FilePath}", filePath);
            throw new StorageException($"Failed to import script: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task ExportScriptAsync(Script script, string filePath)
    {
        if (script == null)
            throw new ArgumentNullException(nameof(script));

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        try
        {
            _logger.LogDebug("Exporting script {ScriptId} to {FilePath}", script.Id, filePath);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var storageModel = new ScriptStorageModel
            {
                Version = "1.0",
                Scripts = new List<ScriptDto> { ConvertToScriptDto(script) }
            };

            var jsonContent = JsonSerializer.Serialize(storageModel, _jsonOptions);
            await File.WriteAllTextAsync(filePath, jsonContent);

            _logger.LogInformation("Successfully exported script {ScriptId} to {FilePath}", script.Id, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting script {ScriptId} to {FilePath}", script.Id, filePath);
            throw new StorageException($"Failed to export script: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Migrates the storage model to the current schema version if needed.
    /// </summary>
    private ScriptStorageModel MigrateSchema(ScriptStorageModel model)
    {
        if (model.Version == "1.0")
        {
            return model; // Already at current version
        }

        _logger.LogInformation("Migrating schema from version {OldVersion} to 1.0", model.Version);
        
        // For now, we only support version 1.0
        // Future versions would have migration logic here
        return model;
    }

    /// <summary>
    /// Converts a Script entity to a ScriptDto for serialization.
    /// </summary>
    private ScriptDto ConvertToScriptDto(Script script)
    {
        return new ScriptDto
        {
            Id = script.Id,
            Name = script.Name,
            CreatedAt = script.CreatedAt,
            ModifiedAt = script.ModifiedAt,
            Commands = script.Commands.Select(ConvertToCommandDto).ToList(),
            SourceText = script.SourceText,
            TriggerHotkey = script.TriggerHotkey != null ? new HotkeyDto
            {
                Id = script.TriggerHotkey.Id,
                Name = script.TriggerHotkey.Name,
                Modifiers = script.TriggerHotkey.Modifiers.ToString(),
                Key = script.TriggerHotkey.Key.ToString(),
                TriggerMode = script.TriggerHotkey.TriggerMode.ToString()
            } : null
        };
    }

    /// <summary>
    /// Converts a Command entity to a CommandDto for serialization.
    /// </summary>
    private CommandDto ConvertToCommandDto(Command command)
    {
        var dto = new CommandDto
        {
            Id = command.Id,
            Delay = command.Delay,
            CreatedAt = command.CreatedAt,
            Type = command switch
            {
                MouseMoveCommand => "MouseMove",
                MouseClickCommand => "MouseClick",
                KeyPressCommand => "KeyPress",
                KeyboardCommand => "Keyboard",
                SleepCommand => "Sleep",
                _ => throw new NotSupportedException($"Unsupported command type: {command.GetType().Name}")
            }
        };

        // Set type-specific parameters
        switch (command)
        {
            case MouseMoveCommand moveCmd:
                dto.Parameters = new Dictionary<string, object?>
                {
                    ["x"] = moveCmd.Position.X,
                    ["y"] = moveCmd.Position.Y
                };
                break;

            case MouseClickCommand clickCmd:
                dto.Parameters = new Dictionary<string, object?>
                {
                    ["button"] = clickCmd.Button.ToString(),
                    ["clickType"] = clickCmd.Type.ToString()
                };
                break;

            case KeyboardCommand keyCmd:
                var keyParams = new Dictionary<string, object?>();
                if (!string.IsNullOrEmpty(keyCmd.Text))
                {
                    keyParams["text"] = keyCmd.Text;
                }
                if (keyCmd.Keys.Count > 0)
                {
                    keyParams["keys"] = keyCmd.Keys.Select(k => k.ToString()).ToList();
                }
                dto.Parameters = keyParams;
                break;

            case KeyPressCommand kp:
                dto.Parameters = new Dictionary<string, object?>
                {
                    ["key"] = kp.Key.ToString(),
                    ["isDown"] = kp.IsDown
                };
                break;

            case SleepCommand sleepCmd:
                dto.Parameters = new Dictionary<string, object?>
                {
                    ["duration"] = sleepCmd.Duration
                };
                break;
        }

        return dto;
    }

    /// <summary>
    /// Converts a ScriptDto to a Script entity.
    /// </summary>
    private Script ConvertToScript(ScriptDto dto)
    {
        var commands = dto.Commands.Select(ConvertToCommand).ToList();
        
        HotkeyDefinition? triggerHotkey = null;
        if (dto.TriggerHotkey != null)
        {
            try
            {
                var modifiers = Enum.Parse<HotkeyModifiers>(dto.TriggerHotkey.Modifiers);
                var key = Enum.Parse<VirtualKey>(dto.TriggerHotkey.Key);
                // Parse TriggerMode, default to Once if not present (for backward compatibility)
                var triggerMode = HotkeyTriggerMode.Once;
                if (!string.IsNullOrEmpty(dto.TriggerHotkey.TriggerMode))
                {
                    if (Enum.TryParse<HotkeyTriggerMode>(dto.TriggerHotkey.TriggerMode, out var parsedMode))
                    {
                        triggerMode = parsedMode;
                    }
                }
                triggerHotkey = new HotkeyDefinition(
                    dto.TriggerHotkey.Id,
                    dto.TriggerHotkey.Name,
                    modifiers,
                    key,
                    triggerMode
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse trigger hotkey for script {ScriptId}", dto.Id);
            }
        }
        
        return new Script(
            dto.Id,
            dto.Name,
            commands,
            dto.CreatedAt,
            dto.ModifiedAt,
            triggerHotkey,
            dto.SourceText
        );
    }

    /// <summary>
    /// Converts a CommandDto to a Command entity.
    /// </summary>
    private Command ConvertToCommand(CommandDto dto)
    {
        return dto.Type switch
        {
            "MouseMove" => new MouseMoveCommand(
                dto.Id,
                dto.Delay,
                dto.CreatedAt,
                new Point(
                    GetIntParameter(dto.Parameters, "x"),
                    GetIntParameter(dto.Parameters, "y")
                )
            ),

            "MouseClick" => new MouseClickCommand(
                dto.Id,
                dto.Delay,
                dto.CreatedAt,
                Enum.Parse<MouseButton>(GetStringParameter(dto.Parameters, "button")),
                Enum.Parse<ClickType>(GetStringParameter(dto.Parameters, "clickType"))
            ),

            "KeyPress" => new KeyPressCommand(
                dto.Id,
                dto.Delay,
                dto.CreatedAt,
                Enum.Parse<VirtualKey>(GetStringParameter(dto.Parameters, "key")),
                GetBoolParameter(dto.Parameters, "isDown")
            ),

            "Keyboard" => new KeyboardCommand(
                dto.Id,
                dto.Delay,
                dto.CreatedAt,
                dto.Parameters != null && dto.Parameters.ContainsKey("text") ? GetStringParameter(dto.Parameters, "text") : null,
                dto.Parameters != null && dto.Parameters.ContainsKey("keys")
                    ? GetStringListParameter(dto.Parameters, "keys")
                        .Select(k => Enum.Parse<VirtualKey>(k))
                        .ToList()
                    : new List<VirtualKey>()
            ),

            "Sleep" => new SleepCommand(
                dto.Id,
                dto.Delay,
                dto.CreatedAt,
                GetTimeSpanParameter(dto.Parameters, "duration")
            ),

            _ => throw new NotSupportedException($"Unsupported command type: {dto.Type}")
        };
    }

    private bool GetBoolParameter(Dictionary<string, object?>? parameters, string key)
    {
        if (parameters == null || !parameters.TryGetValue(key, out var value))
            throw new StorageException($"Missing required parameter '{key}' in command");

        return value switch
        {
            bool b => b,
            JsonElement element when element.ValueKind == JsonValueKind.True => true,
            JsonElement element when element.ValueKind == JsonValueKind.False => false,
            JsonElement element when element.ValueKind == JsonValueKind.String => bool.Parse(element.GetString() ?? "false"),
            string s => bool.Parse(s),
            _ => throw new StorageException($"Invalid parameter type for '{key}': expected boolean")
        };
    }

    private int GetIntParameter(Dictionary<string, object?>? parameters, string key)
    {
        if (parameters == null || !parameters.TryGetValue(key, out var value))
            throw new StorageException($"Missing required parameter '{key}' in command");

        return value switch
        {
            int i => i,
            long l => (int)l,
            JsonElement element when element.ValueKind == JsonValueKind.Number => element.GetInt32(),
            _ => throw new StorageException($"Invalid parameter type for '{key}': expected integer")
        };
    }

    private string GetStringParameter(Dictionary<string, object?>? parameters, string key)
    {
        if (parameters == null || !parameters.TryGetValue(key, out var value))
            throw new StorageException($"Missing required parameter '{key}' in command");

        return value switch
        {
            string s => s,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString() ?? string.Empty,
            _ => throw new StorageException($"Invalid parameter type for '{key}': expected string")
        };
    }

    private List<string> GetStringListParameter(Dictionary<string, object?>? parameters, string key)
    {
        if (parameters == null || !parameters.TryGetValue(key, out var value))
            throw new StorageException($"Missing required parameter '{key}' in command");

        return value switch
        {
            List<string> list => list,
            JsonElement element when element.ValueKind == JsonValueKind.Array => 
                element.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList(),
            _ => throw new StorageException($"Invalid parameter type for '{key}': expected string array")
        };
    }

    private TimeSpan GetTimeSpanParameter(Dictionary<string, object?>? parameters, string key)
    {
        if (parameters == null || !parameters.TryGetValue(key, out var value))
            throw new StorageException($"Missing required parameter '{key}' in command");

        return value switch
        {
            TimeSpan ts => ts,
            string s when TimeSpan.TryParse(s, out var ts) => ts,
            long l => TimeSpan.FromMilliseconds(l),
            int i => TimeSpan.FromMilliseconds(i),
            double d => TimeSpan.FromMilliseconds(d),
            JsonElement element when element.ValueKind == JsonValueKind.String => 
                TimeSpan.Parse(element.GetString() ?? "00:00:00"),
            JsonElement element when element.ValueKind == JsonValueKind.Number => 
                TimeSpan.FromMilliseconds(element.GetDouble()),
            _ => throw new StorageException($"Invalid parameter type for '{key}': expected TimeSpan")
        };
    }

    #region Data Transfer Objects

    /// <summary>
    /// Storage model for scripts collection with versioning.
    /// </summary>
    private class ScriptStorageModel
    {
        public string Version { get; set; } = "1.0";
        public List<ScriptDto> Scripts { get; set; } = new();
    }

    /// <summary>
    /// Data transfer object for Script serialization.
    /// </summary>
    private class ScriptDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public List<CommandDto> Commands { get; set; } = new();
        public string? SourceText { get; set; }
        public HotkeyDto? TriggerHotkey { get; set; }
    }

    /// <summary>
    /// Data transfer object for HotkeyDefinition serialization.
    /// </summary>
    private class HotkeyDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Modifiers { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string TriggerMode { get; set; } = HotkeyTriggerMode.Once.ToString();
    }

    /// <summary>
    /// Data transfer object for Command serialization.
    /// </summary>
    private class CommandDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public TimeSpan Delay { get; set; }
        public DateTime CreatedAt { get; set; }
        public Dictionary<string, object?>? Parameters { get; set; }
    }

    #endregion

    #region JSON Converters

    /// <summary>
    /// JSON converter for Command polymorphic serialization.
    /// </summary>
    private class CommandJsonConverter : JsonConverter<Command>
    {
        public override Command? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // This is handled by CommandDto deserialization
            throw new NotImplementedException("Command deserialization is handled by CommandDto");
        }

        public override void Write(Utf8JsonWriter writer, Command value, JsonSerializerOptions options)
        {
            // This is handled by CommandDto serialization
            throw new NotImplementedException("Command serialization is handled by CommandDto");
        }
    }

    /// <summary>
    /// JSON converter for TimeSpan serialization.
    /// </summary>
    private class TimeSpanJsonConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (TimeSpan.TryParse(value, out var result))
                    return result;
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                return TimeSpan.FromMilliseconds(reader.GetDouble());
            }

            throw new JsonException($"Unable to convert {reader.GetString()} to TimeSpan");
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(@"hh\:mm\:ss\.fff"));
        }
    }

    /// <summary>
    /// JSON converter for DateTime serialization (ISO 8601 format).
    /// </summary>
    private class DateTimeJsonConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    // Preserve Kind (e.g., Z/UTC) for round-trip correctness.
                    return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                }
            }

            throw new JsonException($"Unable to convert {reader.GetString()} to DateTime");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToUniversalTime().ToString("O")); // ISO 8601
        }
    }

    #endregion
}

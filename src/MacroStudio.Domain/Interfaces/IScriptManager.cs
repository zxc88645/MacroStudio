using MacroStudio.Domain.Entities;

namespace MacroStudio.Domain.Interfaces;

/// <summary>
/// Domain service interface for managing automation scripts.
/// Provides operations for script CRUD, validation, and lifecycle management.
/// </summary>
public interface IScriptManager
{
    /// <summary>
    /// Retrieves all scripts from the collection.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains all scripts.</returns>
    Task<IEnumerable<Script>> GetAllScriptsAsync();

    /// <summary>
    /// Retrieves a specific script by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the script to retrieve.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the script if found, null otherwise.</returns>
    Task<Script?> GetScriptAsync(Guid id);

    /// <summary>
    /// Creates a new script with the specified name.
    /// </summary>
    /// <param name="name">The name for the new script.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the newly created script.</returns>
    /// <exception cref="ArgumentException">Thrown when the name is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a script with the same name already exists.</exception>
    Task<Script> CreateScriptAsync(string name);

    /// <summary>
    /// Updates an existing script in the collection.
    /// </summary>
    /// <param name="script">The script to update.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the script is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the script does not exist in the collection.</exception>
    Task UpdateScriptAsync(Script script);

    /// <summary>
    /// Deletes a script from the collection.
    /// </summary>
    /// <param name="id">The unique identifier of the script to delete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether the script was successfully deleted.</returns>
    Task<bool> DeleteScriptAsync(Guid id);

    /// <summary>
    /// Creates a duplicate of an existing script with a new name.
    /// </summary>
    /// <param name="id">The unique identifier of the script to duplicate.</param>
    /// <param name="newName">The name for the duplicated script. If null, a default name will be generated.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the duplicated script.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the source script does not exist.</exception>
    Task<Script> DuplicateScriptAsync(Guid id, string? newName = null);

    /// <summary>
    /// Validates that a script name is unique and meets naming requirements.
    /// </summary>
    /// <param name="name">The name to validate.</param>
    /// <param name="excludeId">Optional script ID to exclude from uniqueness check (for rename operations).</param>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether the name is valid.</returns>
    Task<bool> IsValidScriptNameAsync(string name, Guid? excludeId = null);

    /// <summary>
    /// Validates that a script is ready for execution.
    /// </summary>
    /// <param name="script">The script to validate.</param>
    /// <returns>A validation result indicating whether the script is valid and any issues found.</returns>
    ScriptValidationResult ValidateScript(Script script);

    /// <summary>
    /// Gets the total number of scripts in the collection.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the script count.</returns>
    Task<int> GetScriptCountAsync();

    /// <summary>
    /// Searches for scripts by name using case-insensitive partial matching.
    /// </summary>
    /// <param name="searchTerm">The search term to match against script names.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains matching scripts.</returns>
    Task<IEnumerable<Script>> SearchScriptsAsync(string searchTerm);

    /// <summary>
    /// Imports a script from a JSON file and persists it into storage.
    /// </summary>
    /// <param name="filePath">Path to the script JSON file.</param>
    /// <returns>The imported script.</returns>
    Task<Script> ImportScriptAsync(string filePath);

    /// <summary>
    /// Exports a script to a JSON file.
    /// </summary>
    /// <param name="id">Script id to export.</param>
    /// <param name="filePath">Path to write the exported file.</param>
    Task ExportScriptAsync(Guid id, string filePath);
}

/// <summary>
/// Represents the result of script validation.
/// </summary>
public class ScriptValidationResult
{
    /// <summary>
    /// Gets whether the script is valid for execution.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the list of validation errors found.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Gets the list of validation warnings found.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Initializes a new validation result.
    /// </summary>
    /// <param name="isValid">Whether the script is valid.</param>
    /// <param name="errors">List of validation errors.</param>
    /// <param name="warnings">List of validation warnings.</param>
    public ScriptValidationResult(bool isValid, IEnumerable<string> errors, IEnumerable<string> warnings)
    {
        IsValid = isValid;
        Errors = errors.ToList().AsReadOnly();
        Warnings = warnings.ToList().AsReadOnly();
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="warnings">Optional list of warnings.</param>
    /// <returns>A validation result indicating success.</returns>
    public static ScriptValidationResult Success(IEnumerable<string>? warnings = null)
    {
        return new ScriptValidationResult(true, Array.Empty<string>(), warnings ?? Array.Empty<string>());
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">List of validation errors.</param>
    /// <param name="warnings">Optional list of warnings.</param>
    /// <returns>A validation result indicating failure.</returns>
    public static ScriptValidationResult Failure(IEnumerable<string> errors, IEnumerable<string>? warnings = null)
    {
        return new ScriptValidationResult(false, errors, warnings ?? Array.Empty<string>());
    }
}
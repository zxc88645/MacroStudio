using MacroStudio.Domain.Entities;

namespace MacroStudio.Domain.Interfaces;

/// <summary>
/// Domain service interface for file storage operations.
/// Provides operations for loading, saving, importing, and exporting scripts.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Loads all scripts from persistent storage.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains all loaded scripts.</returns>
    /// <exception cref="StorageException">Thrown when file operations fail or JSON parsing fails.</exception>
    Task<IEnumerable<Script>> LoadScriptsAsync();

    /// <summary>
    /// Saves a script to persistent storage.
    /// If the script already exists, it will be updated; otherwise, it will be added.
    /// </summary>
    /// <param name="script">The script to save.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when script is null.</exception>
    /// <exception cref="StorageException">Thrown when file operations fail or serialization fails.</exception>
    Task SaveScriptAsync(Script script);

    /// <summary>
    /// Deletes a script from persistent storage.
    /// </summary>
    /// <param name="id">The unique identifier of the script to delete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether the script was successfully deleted.</returns>
    /// <exception cref="StorageException">Thrown when file operations fail.</exception>
    Task<bool> DeleteScriptAsync(Guid id);

    /// <summary>
    /// Imports a script from a JSON file.
    /// </summary>
    /// <param name="filePath">The path to the JSON file to import.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the imported script.</returns>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="StorageException">Thrown when file operations fail or JSON parsing fails.</exception>
    Task<Script> ImportScriptAsync(string filePath);

    /// <summary>
    /// Exports a script to a JSON file.
    /// </summary>
    /// <param name="script">The script to export.</param>
    /// <param name="filePath">The path where the JSON file should be created.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when script is null.</exception>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="StorageException">Thrown when file operations fail or serialization fails.</exception>
    Task ExportScriptAsync(Script script, string filePath);
}

/// <summary>
/// Exception thrown when storage operations fail.
/// </summary>
public class StorageException : Exception
{
    /// <summary>
    /// Initializes a new instance of the StorageException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public StorageException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the StorageException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public StorageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Domain.Entities;

/// <summary>
/// Represents an automation script containing a sequence of commands.
/// </summary>
public class Script
{
    private readonly List<Command> _commands;
    private string _name;
    private string _sourceText = string.Empty;

    /// <summary>
    /// Unique identifier for this script.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The display name of the script.
    /// </summary>
    public string Name 
    { 
        get => _name;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Script name cannot be null or empty.", nameof(value));
            
            _name = value.Trim();
            ModifiedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Optional hotkey that triggers this script execution.
    /// </summary>
    public HotkeyDefinition? TriggerHotkey { get; set; }

    /// <summary>
    /// Read-only collection of commands in this script.
    /// </summary>
    public IReadOnlyList<Command> Commands => _commands.AsReadOnly();

    /// <summary>
    /// Lua source code for this script. This is the primary representation for execution.
    /// </summary>
    public string SourceText
    {
        get => _sourceText;
        set
        {
            _sourceText = value ?? string.Empty;
            ModifiedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Timestamp when this script was created.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Timestamp when this script was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; private set; }

    /// <summary>
    /// Gets the total number of commands in this script.
    /// </summary>
    public int CommandCount => _commands.Count;

    /// <summary>
    /// Gets the estimated total execution time for this script (sum of all delays and sleep durations).
    /// </summary>
    public TimeSpan EstimatedDuration
    {
        get
        {
            var totalTime = TimeSpan.Zero;
            foreach (var command in _commands)
            {
                totalTime = totalTime.Add(command.Delay);
                if (command is SleepCommand sleepCommand)
                {
                    totalTime = totalTime.Add(sleepCommand.Duration);
                }
            }
            return totalTime;
        }
    }

    /// <summary>
    /// Initializes a new script with the specified name.
    /// </summary>
    /// <param name="name">The display name for the script.</param>
    public Script(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Script name cannot be null or empty.", nameof(name));

        Id = Guid.NewGuid();
        _name = name.Trim();
        _commands = new List<Command>();
        CreatedAt = DateTime.UtcNow;
        ModifiedAt = CreatedAt;
        _sourceText = string.Empty;
    }

    /// <summary>
    /// Initializes a new script with specific parameters (used for deserialization).
    /// </summary>
    /// <param name="id">The unique identifier for this script.</param>
    /// <param name="name">The display name for the script.</param>
    /// <param name="commands">The initial commands for this script.</param>
    /// <param name="createdAt">The timestamp when this script was created.</param>
    /// <param name="modifiedAt">The timestamp when this script was last modified.</param>
    /// <param name="triggerHotkey">Optional hotkey that triggers this script execution.</param>
    public Script(Guid id, string name, IEnumerable<Command> commands, DateTime createdAt, DateTime modifiedAt, HotkeyDefinition? triggerHotkey = null, string? sourceText = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Script name cannot be null or empty.", nameof(name));

        Id = id;
        _name = name.Trim();
        _commands = new List<Command>(commands);
        CreatedAt = createdAt;
        ModifiedAt = modifiedAt;
        TriggerHotkey = triggerHotkey;
        _sourceText = sourceText ?? string.Empty;
    }

    /// <summary>
    /// Adds a command to the end of the script.
    /// </summary>
    /// <param name="command">The command to add.</param>
    public void AddCommand(Command command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        _commands.Add(command);
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Inserts a command at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert the command.</param>
    /// <param name="command">The command to insert.</param>
    public void InsertCommand(int index, Command command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        if (index < 0 || index > _commands.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _commands.Insert(index, command);
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Removes the command at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the command to remove.</param>
    /// <returns>True if the command was successfully removed, false otherwise.</returns>
    public bool RemoveCommandAt(int index)
    {
        if (index < 0 || index >= _commands.Count)
            return false;

        _commands.RemoveAt(index);
        ModifiedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Removes the specified command from the script.
    /// </summary>
    /// <param name="command">The command to remove.</param>
    /// <returns>True if the command was successfully removed, false otherwise.</returns>
    public bool RemoveCommand(Command command)
    {
        if (command == null)
            return false;

        var removed = _commands.Remove(command);
        if (removed)
        {
            ModifiedAt = DateTime.UtcNow;
        }
        return removed;
    }

    /// <summary>
    /// Moves a command from one position to another.
    /// </summary>
    /// <param name="fromIndex">The current index of the command.</param>
    /// <param name="toIndex">The target index for the command.</param>
    /// <returns>True if the command was successfully moved, false otherwise.</returns>
    public bool MoveCommand(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _commands.Count ||
            toIndex < 0 || toIndex >= _commands.Count ||
            fromIndex == toIndex)
            return false;

        var command = _commands[fromIndex];
        _commands.RemoveAt(fromIndex);
        _commands.Insert(toIndex, command);
        ModifiedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Replaces the command at the specified index with a new command.
    /// </summary>
    /// <param name="index">The index of the command to replace.</param>
    /// <param name="newCommand">The new command to place at the specified index.</param>
    /// <returns>True if the command was successfully replaced, false otherwise.</returns>
    public bool ReplaceCommand(int index, Command newCommand)
    {
        if (newCommand == null)
            throw new ArgumentNullException(nameof(newCommand));
        if (index < 0 || index >= _commands.Count)
            return false;

        _commands[index] = newCommand;
        ModifiedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Clears all commands from the script.
    /// </summary>
    public void ClearCommands()
    {
        _commands.Clear();
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the command at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the command to retrieve.</param>
    /// <returns>The command at the specified index.</returns>
    public Command GetCommand(int index)
    {
        if (index < 0 || index >= _commands.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        return _commands[index];
    }

    /// <summary>
    /// Finds the index of the specified command.
    /// </summary>
    /// <param name="command">The command to locate.</param>
    /// <returns>The zero-based index of the command, or -1 if not found.</returns>
    public int IndexOf(Command command)
    {
        return _commands.IndexOf(command);
    }

    /// <summary>
    /// Validates that all commands in the script are valid for execution.
    /// </summary>
    /// <returns>True if all commands are valid, false otherwise.</returns>
    public bool IsValid()
    {
        return _commands.All(command => command.IsValid());
    }

    /// <summary>
    /// Creates a deep copy of this script with a new ID and name.
    /// </summary>
    /// <param name="newName">The name for the duplicated script.</param>
    /// <returns>A new Script instance with copied commands.</returns>
    public Script Duplicate(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("New script name cannot be null or empty.", nameof(newName));

        var duplicatedScript = new Script(newName);
        foreach (var command in _commands)
        {
            duplicatedScript.AddCommand(command.Clone());
        }
        duplicatedScript.SourceText = SourceText;
        return duplicatedScript;
    }

    public override bool Equals(object? obj)
    {
        return obj is Script other && Id.Equals(other.Id);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override string ToString()
    {
        return $"{Name} ({CommandCount} commands)";
    }
}
using FsCheck;
using FsCheck.Xunit;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.ValueObjects;
using System.Linq;

namespace MacroStudio.Tests.Domain.Entities;

/// <summary>
/// Property-based tests for Script Lifecycle Integrity.
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 1.6**
/// </summary>
public class ScriptLifecycleIntegrityPropertyTests
{
    /// <summary>
    /// Property 1: Script Lifecycle Integrity
    /// For any script management operation (create, delete, rename, copy), 
    /// the script collection should maintain consistency with unique identifiers, 
    /// proper metadata updates, and immediate persistence to storage.
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 1.6**
    /// </summary>
    [Property]
    public bool ScriptLifecycleIntegrity(NonEmptyString scriptName)
    {
        var name = scriptName.Get;
        
        // Skip names that would be invalid after trimming (whitespace-only)
        if (string.IsNullOrWhiteSpace(name))
            return true;
        
        try
        {
            // Test script creation (Requirement 1.1)
            var script = new Script(name);
            var originalId = script.Id;
            var originalCreatedAt = script.CreatedAt;
            var originalModifiedAt = script.ModifiedAt;

            // Verify unique identifier generation
            var script2 = new Script(name);
            if (script.Id == script2.Id)
                return false; // Unique ID property failed

            // Add some test commands
            var commands = new Command[]
            {
                new MouseMoveCommand(new Point(100, 200)),
                new MouseClickCommand(new Point(150, 250), MouseButton.Left, ClickType.Click),
                new KeyboardCommand("Hello World"),
                new SleepCommand(TimeSpan.FromMilliseconds(500))
            };

            foreach (var cmd in commands)
            {
                script.AddCommand(cmd);
            }

            // Test script duplication (Requirement 1.4)
            var duplicatedScript = script.Duplicate($"{script.Name}_Copy");
            if (duplicatedScript.Id == script.Id)
                return false; // Different ID failed
            if (duplicatedScript.Name != $"{script.Name}_Copy")
                return false; // Correct name failed
            if (duplicatedScript.CommandCount != script.CommandCount)
                return false; // Same command count failed
            if (!duplicatedScript.Commands.All(cmd => script.Commands.Any(origCmd => 
                cmd.GetType() == origCmd.GetType() && cmd.Id != origCmd.Id)))
                return false; // Commands are clones failed

            // Test script renaming (Requirement 1.3)
            var newName = $"{script.Name}_Renamed";
            var preRenameModifiedAt = script.ModifiedAt;
            Thread.Sleep(10); // Ensure time difference
            script.Name = newName;
            if (script.Name != newName)
                return false; // Name updated failed
            if (script.Id != originalId)
                return false; // ID unchanged failed
            if (script.CreatedAt != originalCreatedAt)
                return false; // CreatedAt unchanged failed
            if (script.ModifiedAt <= preRenameModifiedAt)
                return false; // ModifiedAt updated failed

            // Test command manipulation maintaining sequence integrity (Requirements 1.5, 2.1-2.6)
            if (script.CommandCount > 0)
            {
                var originalCommandCount = script.CommandCount;
                
                // Test command removal
                var removeResult = script.RemoveCommandAt(0);
                if (!removeResult)
                    return false; // Remove result failed
                if (script.CommandCount != originalCommandCount - 1)
                    return false; // Command count after removal failed

                // Test command insertion
                var newCommand = new MouseMoveCommand(new Point(500, 500));
                script.InsertCommand(0, newCommand);
                if (script.CommandCount != originalCommandCount)
                    return false; // Command count after insertion failed
                if (script.Commands[0] != newCommand)
                    return false; // Command at position 0 failed
            }

            // Test script validation
            if (script.IsValid() != script.Commands.All(cmd => cmd.IsValid()))
                return false; // Validation property failed

            // Test metadata consistency
            if (script.Id == Guid.Empty)
                return false; // Valid ID failed
            if (string.IsNullOrWhiteSpace(script.Name))
                return false; // Valid name failed
            if (script.CreatedAt > DateTime.UtcNow)
                return false; // Valid creation time failed
            if (script.ModifiedAt > DateTime.UtcNow)
                return false; // Valid modification time failed
            if (script.ModifiedAt < script.CreatedAt)
                return false; // Modification time >= creation time failed

            // Test estimated duration calculation
            var expectedDuration = script.Commands.Sum(cmd => 
                cmd.Delay.TotalMilliseconds + 
                (cmd is SleepCommand sleepCmd ? sleepCmd.Duration.TotalMilliseconds : 0));
            if (Math.Abs(script.EstimatedDuration.TotalMilliseconds - expectedDuration) >= 1)
                return false; // Duration property failed

            return true;
        }
        catch (Exception)
        {
            // If any exception occurs, the property fails
            return false;
        }
    }

    /// <summary>
    /// Property: Script Command Sequence Preservation
    /// For any command manipulation operation (add, delete, reorder, modify), 
    /// the command sequence should maintain integrity with correct indices, 
    /// valid parameters, and preserved execution order.
    /// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6**
    /// </summary>
    [Property]
    public bool CommandSequencePreservation(NonEmptyString scriptName, PositiveInt commandCount)
    {
        var name = scriptName.Get;
        var count = Math.Min(commandCount.Get, 10); // Limit to reasonable size
        
        // Skip names that would be invalid after trimming (whitespace-only)
        if (string.IsNullOrWhiteSpace(name))
            return true;
        
        if (count == 0) return true; // Skip empty case

        var script = new Script(name);
        
        // Add test commands
        var commands = new List<Command>();
        for (int i = 0; i < count; i++)
        {
            commands.Add(new MouseMoveCommand(new Point(i * 10, i * 10)));
        }

        foreach (var cmd in commands)
        {
            script.AddCommand(cmd);
        }

        var originalCommandCount = script.CommandCount;
        var originalCommands = script.Commands.ToList();

        // Test command reordering (if we have multiple commands)
        if (originalCommandCount > 1)
        {
            var fromIndex = 0;
            var toIndex = originalCommandCount - 1;
            var commandToMove = originalCommands[fromIndex];
            
            var moveResult = script.MoveCommand(fromIndex, toIndex);
            var moveProperties = 
                moveResult &&
                script.CommandCount == originalCommandCount && // Count unchanged
                script.Commands[toIndex] == commandToMove && // Command moved to correct position
                script.Commands.All(cmd => originalCommands.Contains(cmd)); // All original commands still present
            
            if (!moveProperties)
                return false;
        }

        // Test command replacement
        if (originalCommandCount > 0)
        {
            var replaceIndex = 0;
            var newCommand = new MouseMoveCommand(new Point(999, 999));
            var replaceResult = script.ReplaceCommand(replaceIndex, newCommand);
            var replaceProperties = 
                replaceResult &&
                script.CommandCount == originalCommandCount && // Count unchanged
                script.Commands[replaceIndex] == newCommand; // Command replaced correctly
            
            if (!replaceProperties)
                return false;
        }

        // Test command indexing and retrieval
        for (int i = 0; i < script.CommandCount; i++)
        {
            var cmd = script.GetCommand(i);
            var index = script.IndexOf(cmd);
            if (index != i)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Property: Script State Consistency
    /// For any script state change, all derived properties should remain consistent
    /// and metadata should be properly updated.
    /// **Validates: Requirements 1.5, 1.6**
    /// </summary>
    [Property]
    public bool ScriptStateConsistency(NonEmptyString scriptName, PositiveInt commandCount)
    {
        var name = scriptName.Get;
        var count = Math.Min(commandCount.Get, 5); // Limit to reasonable size
        
        // Skip names that would be invalid after trimming (whitespace-only)
        if (string.IsNullOrWhiteSpace(name))
            return true;
        
        var script = new Script(name);

        // Add commands one by one and verify consistency
        for (int i = 0; i < count; i++)
        {
            var preAddModifiedAt = script.ModifiedAt;
            var preAddCount = script.CommandCount;
            
            Thread.Sleep(10); // Ensure time difference
            var cmd = new MouseMoveCommand(new Point(i * 10, i * 10));
            script.AddCommand(cmd);
            
            // Verify state consistency after each addition
            var stateConsistent = 
                script.CommandCount == preAddCount + 1 && // Count incremented
                script.ModifiedAt > preAddModifiedAt && // ModifiedAt updated
                script.Commands.Last() == cmd && // Command added at end
                script.IsValid() == script.Commands.All(c => c.IsValid()); // Validation consistent
            
            if (!stateConsistent)
                return false;
        }

        // Test clear operation
        if (script.CommandCount > 0)
        {
            var preClearModifiedAt = script.ModifiedAt;
            Thread.Sleep(10);
            script.ClearCommands();
            
            var clearProperties = 
                script.CommandCount == 0 &&
                script.Commands.Count == 0 &&
                script.ModifiedAt > preClearModifiedAt &&
                script.EstimatedDuration == TimeSpan.Zero;
            
            if (!clearProperties)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Property: Command Validation Consistency
    /// For any command, validation should be consistent with its parameters.
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property]
    public bool CommandValidationConsistency(int x, int y, NonNegativeInt delayMs)
    {
        var delay = TimeSpan.FromMilliseconds(delayMs.Get);
        
        // Test MouseMoveCommand validation
        var mouseMoveValid = new MouseMoveCommand(new Point(Math.Abs(x), Math.Abs(y))) { Delay = delay };
        var mouseMoveInvalid = new MouseMoveCommand(new Point(-1, -1)) { Delay = delay };
        
        if (mouseMoveValid.IsValid() != true || mouseMoveInvalid.IsValid() != false)
            return false;

        // Test MouseClickCommand validation
        var mouseClickValid = new MouseClickCommand(new Point(Math.Abs(x), Math.Abs(y)), MouseButton.Left, ClickType.Click) { Delay = delay };
        var mouseClickInvalid = new MouseClickCommand(new Point(-1, -1), MouseButton.Left, ClickType.Click) { Delay = delay };
        
        if (mouseClickValid.IsValid() != true || mouseClickInvalid.IsValid() != false)
            return false;

        // Test KeyboardCommand validation
        var keyboardValid = new KeyboardCommand("Hello") { Delay = delay };
        var keyboardInvalid = new KeyboardCommand("") { Delay = delay };
        
        if (keyboardValid.IsValid() != true || keyboardInvalid.IsValid() != false)
            return false;

        // Test SleepCommand validation
        var sleepValid = new SleepCommand(TimeSpan.FromMilliseconds(Math.Abs(delayMs.Get))) { Delay = delay };
        var sleepInvalid = new SleepCommand(TimeSpan.FromMilliseconds(-1)) { Delay = delay };
        
        if (sleepValid.IsValid() != true || sleepInvalid.IsValid() != false)
            return false;

        return true;
    }
}
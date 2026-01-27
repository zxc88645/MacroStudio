using FsCheck;
using FsCheck.Xunit;
using MacroNex.Domain.Entities;
using MacroNex.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroNex.Tests.Infrastructure;

public class SerializationRoundTripPropertyTests
{
    [Property]
    // Feature: macro-studio, Property 6: Serialization Round-Trip Consistency
    // Validates: Requirements 6.1, 6.2, 6.3, 6.4, 6.5, 6.6
    public bool SerializationRoundTripConsistency(NonEmptyString scriptName, PositiveInt commandCount)
    {
        var name = scriptName.Get;
        if (string.IsNullOrWhiteSpace(name))
            return true;

        var count = Math.Min(commandCount.Get, 25);

        // Use a unique temp directory per test case to avoid cross-test interference.
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroNex.Tests", "storage", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var logger = NullLogger<JsonFileStorageService>.Instance;
            var storage = new JsonFileStorageService(logger, tempDir);

            var script = TestData.CreateScriptWithCommands(name.Trim(), count);

            // Export to a separate file (portable export)
            var exportPath = Path.Combine(tempDir, "export.json");
            storage.ExportScriptAsync(script, exportPath).GetAwaiter().GetResult();

            // Import back
            var imported = storage.ImportScriptAsync(exportPath).GetAwaiter().GetResult();

            return TestData.ScriptEquivalent(script, imported);
        }
        catch
        {
            return false;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore cleanup failures */ }
        }
    }

    private static class TestData
    {
        public static Script CreateScriptWithCommands(string name, int count)
        {
            var createdAt = DateTime.UtcNow.AddMinutes(-5);
            var modifiedAt = createdAt.AddSeconds(1);

            var commands = new List<Command>();

            for (var i = 0; i < count; i++)
            {
                var delay = TimeSpan.FromMilliseconds(i * 10);
                var cmdCreatedAt = createdAt.AddMilliseconds(i);

                Command cmd = (i % 5) switch
                {
                    0 => new MouseMoveCommand(Guid.NewGuid(), delay, cmdCreatedAt, new MacroNex.Domain.ValueObjects.Point(i, i + 1)),
                    1 => new MouseClickCommand(
                        Guid.NewGuid(),
                        delay,
                        cmdCreatedAt,
                        MacroNex.Domain.ValueObjects.MouseButton.Left,
                        MacroNex.Domain.ValueObjects.ClickType.Click),
                    2 => new KeyboardCommand(Guid.NewGuid(), delay, cmdCreatedAt, $"Text{i}", Array.Empty<MacroNex.Domain.ValueObjects.VirtualKey>()),
                    3 => new KeyPressCommand(Guid.NewGuid(), delay, cmdCreatedAt, MacroNex.Domain.ValueObjects.VirtualKey.VK_A, isDown: (i % 2 == 0)),
                    _ => new SleepCommand(Guid.NewGuid(), delay, cmdCreatedAt, TimeSpan.FromMilliseconds(50 + i)),
                };

                commands.Add(cmd);
            }

            return new Script(Guid.NewGuid(), name, commands, createdAt, modifiedAt);
        }

        public static bool ScriptEquivalent(Script a, Script b)
        {
            if (a.Id != b.Id) return false;
            if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal)) return false;
            if (a.CreatedAt != b.CreatedAt) return false;
            if (a.ModifiedAt != b.ModifiedAt) return false;
            if (a.CommandCount != b.CommandCount) return false;

            for (var i = 0; i < a.CommandCount; i++)
            {
                var ac = a.Commands[i];
                var bc = b.Commands[i];

                if (ac.Id != bc.Id) return false;
                if (ac.Delay != bc.Delay) return false;
                if (ac.CreatedAt != bc.CreatedAt) return false;
                if (ac.GetType() != bc.GetType()) return false;

                switch (ac)
                {
                    case MouseMoveCommand am when bc is MouseMoveCommand bm:
                        if (am.Position != bm.Position) return false;
                        break;
                    case MouseClickCommand am when bc is MouseClickCommand bm:
                        if (am.Button != bm.Button) return false;
                        if (am.Type != bm.Type) return false;
                        break;
                    case KeyboardCommand ak when bc is KeyboardCommand bk:
                        if (ak.Text != bk.Text) return false;
                        if (ak.Keys.Count != bk.Keys.Count) return false;
                        for (var k = 0; k < ak.Keys.Count; k++)
                            if (ak.Keys[k] != bk.Keys[k]) return false;
                        break;
                    case KeyPressCommand akp when bc is KeyPressCommand bkp:
                        if (akp.Key != bkp.Key) return false;
                        if (akp.IsDown != bkp.IsDown) return false;
                        break;
                    case SleepCommand aslp when bc is SleepCommand bslp:
                        if (aslp.Duration != bslp.Duration) return false;
                        break;
                    default:
                        return false;
                }
            }

            return true;
        }
    }
}


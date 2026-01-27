using MacroNex.Application.Services;
using MacroNex.Domain.Entities;
using MacroNex.Domain.Interfaces;
using MacroNex.Domain.ValueObjects;
using MacroNex.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MacroNex.Tests.Application;

/// <summary>
/// Tests for ScriptManager hotkey unregistration when scripts are deleted.
/// </summary>
public class ScriptManagerHotkeyUnregisterTests
{
    [Fact]
    public async Task DeleteScriptAsync_WithTrackedHotkey_RemovesHotkeyFromHookMapping()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroNex.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockHookService = new Mock<IScriptHotkeyHookService>();
            IReadOnlyDictionary<Guid, HotkeyDefinition>? lastMapping = null;
            mockHookService.Setup(s => s.SetScriptHotkeys(It.IsAny<IReadOnlyDictionary<Guid, HotkeyDefinition>>()))
                .Callback<IReadOnlyDictionary<Guid, HotkeyDefinition>>(m => lastMapping = new Dictionary<Guid, HotkeyDefinition>(m));

            var storageService = new JsonFileStorageService(
                NullLogger<JsonFileStorageService>.Instance, tempDir);
            var scriptManager = new ScriptManager(
                storageService,
                mockHookService.Object,
                NullLogger<ScriptManager>.Instance);

            // Create script with hotkey
            var script = await scriptManager.CreateScriptAsync("Test Script");
            var hotkey = HotkeyDefinition.Create(
                "Test Hotkey",
                HotkeyModifiers.Control | HotkeyModifiers.Shift,
                VirtualKey.VK_F1,
                HotkeyTriggerMode.Once);
            script.TriggerHotkey = hotkey;

            // Update hotkey through ScriptManager (this should update hook mapping)
            await scriptManager.UpdateScriptAsync(script);

            Assert.NotNull(lastMapping);
            Assert.True(lastMapping!.ContainsKey(script.Id));

            // Act: Delete script
            var deleted = await scriptManager.DeleteScriptAsync(script.Id);

            // Assert
            Assert.True(deleted);
            Assert.NotNull(lastMapping);
            Assert.False(lastMapping!.ContainsKey(script.Id));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task DeleteScriptAsync_WithUntrackedHotkey_RemovesHotkeyFromHookMapping()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroNex.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockHookService = new Mock<IScriptHotkeyHookService>();
            IReadOnlyDictionary<Guid, HotkeyDefinition>? lastMapping = null;
            mockHookService.Setup(s => s.SetScriptHotkeys(It.IsAny<IReadOnlyDictionary<Guid, HotkeyDefinition>>()))
                .Callback<IReadOnlyDictionary<Guid, HotkeyDefinition>>(m => lastMapping = new Dictionary<Guid, HotkeyDefinition>(m));

            var storageService = new JsonFileStorageService(
                NullLogger<JsonFileStorageService>.Instance, tempDir);
            var scriptManager = new ScriptManager(
                storageService,
                mockHookService.Object,
                NullLogger<ScriptManager>.Instance);

            // Create script with hotkey
            var script = await scriptManager.CreateScriptAsync("Test Script");
            var hk = HotkeyDefinition.Create(
                "Test Hotkey",
                HotkeyModifiers.Control | HotkeyModifiers.Shift,
                VirtualKey.VK_F2,
                HotkeyTriggerMode.Once);
            script.TriggerHotkey = hk;

            await scriptManager.UpdateScriptAsync(script);
            Assert.NotNull(lastMapping);
            Assert.True(lastMapping!.ContainsKey(script.Id));

            // Act: Delete script
            var deleted = await scriptManager.DeleteScriptAsync(script.Id);

            Assert.True(deleted);
            Assert.NotNull(lastMapping);
            Assert.False(lastMapping!.ContainsKey(script.Id));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task DeleteScriptAsync_WithHotkeyMatchingByModifiersKey_RemovesFromHookMapping()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroNex.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockHookService = new Mock<IScriptHotkeyHookService>();
            IReadOnlyDictionary<Guid, HotkeyDefinition>? lastMapping = null;
            mockHookService.Setup(s => s.SetScriptHotkeys(It.IsAny<IReadOnlyDictionary<Guid, HotkeyDefinition>>()))
                .Callback<IReadOnlyDictionary<Guid, HotkeyDefinition>>(m => lastMapping = new Dictionary<Guid, HotkeyDefinition>(m));

            var storageService = new JsonFileStorageService(
                NullLogger<JsonFileStorageService>.Instance, tempDir);
            var scriptManager = new ScriptManager(
                storageService,
                mockHookService.Object,
                NullLogger<ScriptManager>.Instance);

            // Create script with hotkey that has same Modifiers/Key/TriggerMode but different ID
            var script = await scriptManager.CreateScriptAsync("Test Script");
            var scriptHotkey = HotkeyDefinition.Create(
                "Script Hotkey",
                HotkeyModifiers.Control | HotkeyModifiers.Alt,
                VirtualKey.VK_F3,
                HotkeyTriggerMode.RepeatWhileHeld);
            script.TriggerHotkey = scriptHotkey;

            // Save script
            await scriptManager.UpdateScriptAsync(script);

            Assert.NotNull(lastMapping);
            Assert.True(lastMapping!.ContainsKey(script.Id));

            // Act: Delete script
            var deleted = await scriptManager.DeleteScriptAsync(script.Id);

            // Assert
            Assert.True(deleted);
            Assert.NotNull(lastMapping);
            Assert.False(lastMapping!.ContainsKey(script.Id));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task DeleteScriptAsync_WithoutHotkey_LeavesHookMappingEmpty()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroNex.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockHookService = new Mock<IScriptHotkeyHookService>();
            IReadOnlyDictionary<Guid, HotkeyDefinition>? lastMapping = null;
            mockHookService.Setup(s => s.SetScriptHotkeys(It.IsAny<IReadOnlyDictionary<Guid, HotkeyDefinition>>()))
                .Callback<IReadOnlyDictionary<Guid, HotkeyDefinition>>(m => lastMapping = new Dictionary<Guid, HotkeyDefinition>(m));

            var storageService = new JsonFileStorageService(
                NullLogger<JsonFileStorageService>.Instance, tempDir);
            var scriptManager = new ScriptManager(
                storageService,
                mockHookService.Object,
                NullLogger<ScriptManager>.Instance);

            // Create script without hotkey
            var script = await scriptManager.CreateScriptAsync("Test Script");
            // No hotkey set

            // Act: Delete script
            var deleted = await scriptManager.DeleteScriptAsync(script.Id);

            // Assert
            Assert.True(deleted);
            // No hotkeys should have been set (or if called, should be empty)
            if (lastMapping != null)
                Assert.Empty(lastMapping);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task DeleteScriptAsync_WithHotkeyNotInCache_RemovesFromHookMapping()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroNex.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockHookService = new Mock<IScriptHotkeyHookService>();
            IReadOnlyDictionary<Guid, HotkeyDefinition>? lastMapping = null;
            mockHookService.Setup(s => s.SetScriptHotkeys(It.IsAny<IReadOnlyDictionary<Guid, HotkeyDefinition>>()))
                .Callback<IReadOnlyDictionary<Guid, HotkeyDefinition>>(m => lastMapping = new Dictionary<Guid, HotkeyDefinition>(m));

            var storageService = new JsonFileStorageService(
                NullLogger<JsonFileStorageService>.Instance, tempDir);
            var scriptManager = new ScriptManager(
                storageService,
                mockHookService.Object,
                NullLogger<ScriptManager>.Instance);

            // Create script with hotkey
            var script = await scriptManager.CreateScriptAsync("Test Script");
            var hotkey = HotkeyDefinition.Create(
                "Test Hotkey",
                HotkeyModifiers.Control,
                VirtualKey.VK_F4,
                HotkeyTriggerMode.Once);
            script.TriggerHotkey = hotkey;
            await scriptManager.UpdateScriptAsync(script);

            // Clear cache by creating a new ScriptManager instance
            var scriptManager2 = new ScriptManager(
                storageService,
                mockHookService.Object,
                NullLogger<ScriptManager>.Instance);

            // Act: Delete script (not in cache)
            var deleted = await scriptManager2.DeleteScriptAsync(script.Id);

            // Assert
            Assert.True(deleted);
            Assert.NotNull(lastMapping);
            Assert.False(lastMapping!.ContainsKey(script.Id));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}

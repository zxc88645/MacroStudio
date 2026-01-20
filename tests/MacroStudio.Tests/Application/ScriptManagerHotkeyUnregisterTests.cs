using MacroStudio.Application.Services;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MacroStudio.Tests.Application;

/// <summary>
/// Tests for ScriptManager hotkey unregistration when scripts are deleted.
/// </summary>
public class ScriptManagerHotkeyUnregisterTests
{
    [Fact]
    public async Task DeleteScriptAsync_WithTrackedHotkey_UnregistersHotkey()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroStudio.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockHotkeyService = new Mock<IGlobalHotkeyService>();
            var registeredHotkeys = new List<HotkeyDefinition>();
            var unregisteredHotkeys = new List<HotkeyDefinition>();

            mockHotkeyService.Setup(s => s.RegisterHotkeyAsync(It.IsAny<HotkeyDefinition>()))
                .Returns<HotkeyDefinition>(hotkey =>
                {
                    registeredHotkeys.Add(hotkey);
                    return Task.CompletedTask;
                });

            mockHotkeyService.Setup(s => s.UnregisterHotkeyAsync(It.IsAny<HotkeyDefinition>()))
                .Returns<HotkeyDefinition>(hotkey =>
                {
                    unregisteredHotkeys.Add(hotkey);
                    return Task.CompletedTask;
                });

            mockHotkeyService.Setup(s => s.GetRegisteredHotkeysAsync())
                .ReturnsAsync(() => registeredHotkeys.AsEnumerable());

            var storageService = new JsonFileStorageService(
                NullLogger<JsonFileStorageService>.Instance, tempDir);
            var scriptManager = new ScriptManager(
                storageService,
                mockHotkeyService.Object,
                NullLogger<ScriptManager>.Instance);

            // Create script with hotkey
            var script = await scriptManager.CreateScriptAsync("Test Script");
            var hotkey = HotkeyDefinition.Create(
                "Test Hotkey",
                HotkeyModifiers.Control | HotkeyModifiers.Shift,
                VirtualKey.VK_F1,
                HotkeyTriggerMode.Once);
            script.TriggerHotkey = hotkey;

            // Register hotkey through ScriptManager (this tracks it)
            await scriptManager.UpdateScriptAsync(script);

            // Verify hotkey was registered
            Assert.Contains(hotkey, registeredHotkeys);
            Assert.Empty(unregisteredHotkeys);

            // Act: Delete script
            var deleted = await scriptManager.DeleteScriptAsync(script.Id);

            // Assert
            Assert.True(deleted);
            Assert.Contains(hotkey, unregisteredHotkeys);
            mockHotkeyService.Verify(s => s.UnregisterHotkeyAsync(hotkey), Times.Once);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task DeleteScriptAsync_WithUntrackedHotkey_StillUnregistersHotkey()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroStudio.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockHotkeyService = new Mock<IGlobalHotkeyService>();
            var registeredHotkeys = new List<HotkeyDefinition>();
            var unregisteredHotkeys = new List<HotkeyDefinition>();

            // Simulate hotkey registered elsewhere (e.g., MainViewModel)
            var hotkey = HotkeyDefinition.Create(
                "Test Hotkey",
                HotkeyModifiers.Control | HotkeyModifiers.Shift,
                VirtualKey.VK_F2,
                HotkeyTriggerMode.Once);
            registeredHotkeys.Add(hotkey);

            mockHotkeyService.Setup(s => s.RegisterHotkeyAsync(It.IsAny<HotkeyDefinition>()))
                .Returns<HotkeyDefinition>(h =>
                {
                    registeredHotkeys.Add(h);
                    return Task.CompletedTask;
                });

            mockHotkeyService.Setup(s => s.UnregisterHotkeyAsync(It.IsAny<HotkeyDefinition>()))
                .Returns<HotkeyDefinition>(h =>
                {
                    unregisteredHotkeys.Add(h);
                    registeredHotkeys.Remove(h);
                    return Task.CompletedTask;
                });

            mockHotkeyService.Setup(s => s.GetRegisteredHotkeysAsync())
                .ReturnsAsync(() => registeredHotkeys.AsEnumerable());

            var storageService = new JsonFileStorageService(
                NullLogger<JsonFileStorageService>.Instance, tempDir);
            var scriptManager = new ScriptManager(
                storageService,
                mockHotkeyService.Object,
                NullLogger<ScriptManager>.Instance);

            // Create script with hotkey (same Modifiers/Key/TriggerMode but different instance)
            var script = await scriptManager.CreateScriptAsync("Test Script");
            // Use a different HotkeyDefinition instance with same Modifiers/Key/TriggerMode
            var scriptHotkey = new HotkeyDefinition(
                Guid.NewGuid(), // Different ID
                "Script Hotkey", // Different name
                hotkey.Modifiers,
                hotkey.Key,
                hotkey.TriggerMode);
            script.TriggerHotkey = scriptHotkey;

            // Save script but don't register through ScriptManager (simulating MainViewModel registration)
            await scriptManager.UpdateScriptAsync(script);
            // Manually register through hotkey service (simulating MainViewModel)
            await mockHotkeyService.Object.RegisterHotkeyAsync(hotkey);

            // Verify hotkey is registered
            Assert.Contains(hotkey, registeredHotkeys);
            Assert.Empty(unregisteredHotkeys);

            // Act: Delete script
            var deleted = await scriptManager.DeleteScriptAsync(script.Id);

            // Assert: Should still unregister even though not tracked
            Assert.True(deleted);
            // Should attempt unregister with script's hotkey
            mockHotkeyService.Verify(s => s.UnregisterHotkeyAsync(It.IsAny<HotkeyDefinition>()), Times.AtLeastOnce);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task DeleteScriptAsync_WithHotkeyMatchingByModifiersKey_UnregistersCorrectHotkey()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroStudio.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockHotkeyService = new Mock<IGlobalHotkeyService>();
            var registeredHotkeys = new List<HotkeyDefinition>();
            var unregisteredHotkeys = new List<HotkeyDefinition>();

            // Register a hotkey with specific Modifiers/Key/TriggerMode
            var registeredHotkey = HotkeyDefinition.Create(
                "Registered Hotkey",
                HotkeyModifiers.Control | HotkeyModifiers.Alt,
                VirtualKey.VK_F3,
                HotkeyTriggerMode.RepeatWhileHeld);
            registeredHotkeys.Add(registeredHotkey);

            mockHotkeyService.Setup(s => s.RegisterHotkeyAsync(It.IsAny<HotkeyDefinition>()))
                .Returns<HotkeyDefinition>(h =>
                {
                    registeredHotkeys.Add(h);
                    return Task.CompletedTask;
                });

            mockHotkeyService.Setup(s => s.UnregisterHotkeyAsync(It.IsAny<HotkeyDefinition>()))
                .Returns<HotkeyDefinition>(h =>
                {
                    // First attempt might fail if ID doesn't match
                    var matching = registeredHotkeys.FirstOrDefault(rh =>
                        rh.Modifiers == h.Modifiers &&
                        rh.Key == h.Key &&
                        rh.TriggerMode == h.TriggerMode);
                    
                    if (matching != null)
                    {
                        unregisteredHotkeys.Add(matching);
                        registeredHotkeys.Remove(matching);
                    }
                    else
                    {
                        throw new HotkeyRegistrationException("Hotkey not found");
                    }
                    return Task.CompletedTask;
                });

            mockHotkeyService.Setup(s => s.GetRegisteredHotkeysAsync())
                .ReturnsAsync(() => registeredHotkeys.AsEnumerable());

            var storageService = new JsonFileStorageService(
                NullLogger<JsonFileStorageService>.Instance, tempDir);
            var scriptManager = new ScriptManager(
                storageService,
                mockHotkeyService.Object,
                NullLogger<ScriptManager>.Instance);

            // Create script with hotkey that has same Modifiers/Key/TriggerMode but different ID
            var script = await scriptManager.CreateScriptAsync("Test Script");
            var scriptHotkey = new HotkeyDefinition(
                Guid.NewGuid(), // Different ID
                "Script Hotkey", // Different name
                registeredHotkey.Modifiers,
                registeredHotkey.Key,
                registeredHotkey.TriggerMode);
            script.TriggerHotkey = scriptHotkey;

            // Save script
            await scriptManager.UpdateScriptAsync(script);

            // Verify hotkey is registered
            Assert.Contains(registeredHotkey, registeredHotkeys);
            Assert.Empty(unregisteredHotkeys);

            // Act: Delete script
            var deleted = await scriptManager.DeleteScriptAsync(script.Id);

            // Assert: Should find and unregister matching hotkey
            Assert.True(deleted);
            Assert.Contains(registeredHotkey, unregisteredHotkeys);
            Assert.DoesNotContain(registeredHotkey, registeredHotkeys);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task DeleteScriptAsync_WithoutHotkey_DoesNotCallUnregister()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroStudio.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockHotkeyService = new Mock<IGlobalHotkeyService>();

            var storageService = new JsonFileStorageService(
                NullLogger<JsonFileStorageService>.Instance, tempDir);
            var scriptManager = new ScriptManager(
                storageService,
                mockHotkeyService.Object,
                NullLogger<ScriptManager>.Instance);

            // Create script without hotkey
            var script = await scriptManager.CreateScriptAsync("Test Script");
            // No hotkey set

            // Act: Delete script
            var deleted = await scriptManager.DeleteScriptAsync(script.Id);

            // Assert
            Assert.True(deleted);
            mockHotkeyService.Verify(s => s.UnregisterHotkeyAsync(It.IsAny<HotkeyDefinition>()), Times.Never);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task DeleteScriptAsync_WithHotkeyNotInCache_LoadsFromStorageAndUnregisters()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroStudio.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var mockHotkeyService = new Mock<IGlobalHotkeyService>();
            var unregisteredHotkeys = new List<HotkeyDefinition>();

            mockHotkeyService.Setup(s => s.UnregisterHotkeyAsync(It.IsAny<HotkeyDefinition>()))
                .Returns<HotkeyDefinition>(h =>
                {
                    unregisteredHotkeys.Add(h);
                    return Task.CompletedTask;
                });

            var storageService = new JsonFileStorageService(
                NullLogger<JsonFileStorageService>.Instance, tempDir);
            var scriptManager = new ScriptManager(
                storageService,
                mockHotkeyService.Object,
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
                mockHotkeyService.Object,
                NullLogger<ScriptManager>.Instance);

            // Act: Delete script (not in cache)
            var deleted = await scriptManager2.DeleteScriptAsync(script.Id);

            // Assert: Should load from storage and unregister
            Assert.True(deleted);
            Assert.Contains(hotkey, unregisteredHotkeys);
            mockHotkeyService.Verify(s => s.UnregisterHotkeyAsync(hotkey), Times.Once);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}

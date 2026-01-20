using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Infrastructure.Adapters;
using MacroStudio.Infrastructure.Win32;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using static MacroStudio.Infrastructure.Win32.Win32Structures;

namespace MacroStudio.Tests.Infrastructure;

/// <summary>
/// Unit tests for Win32GlobalHotkeyService.
/// </summary>
public class Win32GlobalHotkeyServiceTests : IDisposable
{
    private readonly Mock<ILogger<Win32GlobalHotkeyService>> _mockLogger;
    private readonly Win32GlobalHotkeyService _hotkeyService;
    private readonly FakeWin32HotkeyApi _fakeApi;

    public Win32GlobalHotkeyServiceTests()
    {
        _mockLogger = new Mock<ILogger<Win32GlobalHotkeyService>>();
        _fakeApi = new FakeWin32HotkeyApi();
        _hotkeyService = new Win32GlobalHotkeyService(_mockLogger.Object, _fakeApi);
    }

    [Fact]
    public void Constructor_WithValidLogger_ShouldInitializeSuccessfully()
    {
        // Arrange & Act
        using var service = new Win32GlobalHotkeyService(_mockLogger.Object, new FakeWin32HotkeyApi());

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Win32GlobalHotkeyService(null!, new FakeWin32HotkeyApi()));
    }

    [Fact]
    public async Task IsReadyAsync_WhenServiceIsInitialized_ShouldReturnTrue()
    {
        // Arrange
        // Wait a moment for the message loop to start
        await Task.Delay(100);

        // Act
        var isReady = await _hotkeyService.IsReadyAsync();

        // Assert
        Assert.True(isReady);
    }

    [Fact]
    public async Task RegisterHotkeyAsync_WithNullHotkey_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _hotkeyService.RegisterHotkeyAsync(null!));
    }

    [Fact]
    public async Task RegisterHotkeyAsync_WithInvalidHotkey_ShouldThrowHotkeyRegistrationException()
    {
        // Arrange
        var invalidHotkey = HotkeyDefinition.Create("Invalid", HotkeyModifiers.None, VirtualKey.VK_CONTROL);

        // Act & Assert
        await Assert.ThrowsAsync<HotkeyRegistrationException>(() => _hotkeyService.RegisterHotkeyAsync(invalidHotkey));
    }

    [Fact]
    public async Task RegisterHotkeyAsync_WithValidHotkey_ShouldRegisterSuccessfully()
    {
        // Arrange
        var hotkey = HotkeyDefinition.Create("Test Hotkey", HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey.VK_F1);

        try
        {
            // Act
            await _hotkeyService.RegisterHotkeyAsync(hotkey);

            // Assert
            var isRegistered = await _hotkeyService.IsHotkeyRegisteredAsync(hotkey);
            Assert.True(isRegistered);

            var registeredHotkeys = await _hotkeyService.GetRegisteredHotkeysAsync();
            Assert.Contains(hotkey, registeredHotkeys);
        }
        finally
        {
            // Cleanup
            try
            {
                await _hotkeyService.UnregisterHotkeyAsync(hotkey);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task RegisterHotkeyAsync_WithDuplicateHotkey_ShouldNotThrow()
    {
        // Arrange
        var hotkey = HotkeyDefinition.Create("Test Hotkey", HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey.VK_F2);

        try
        {
            // Act
            await _hotkeyService.RegisterHotkeyAsync(hotkey);
            await _hotkeyService.RegisterHotkeyAsync(hotkey); // Should not throw

            // Assert
            var registeredHotkeys = await _hotkeyService.GetRegisteredHotkeysAsync();
            Assert.Single(registeredHotkeys, h => h.Equals(hotkey));
        }
        finally
        {
            // Cleanup
            try
            {
                await _hotkeyService.UnregisterHotkeyAsync(hotkey);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task RegisterHotkeyAsync_WithConflictingHotkey_ShouldThrowHotkeyRegistrationException()
    {
        // Arrange
        var hotkey1 = HotkeyDefinition.Create("Test Hotkey 1", HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey.VK_F3);
        var hotkey2 = HotkeyDefinition.Create("Test Hotkey 2", HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey.VK_F3);

        try
        {
            // Act
            await _hotkeyService.RegisterHotkeyAsync(hotkey1);

            // Assert
            await Assert.ThrowsAsync<HotkeyRegistrationException>(() => _hotkeyService.RegisterHotkeyAsync(hotkey2));
        }
        finally
        {
            // Cleanup
            try
            {
                await _hotkeyService.UnregisterHotkeyAsync(hotkey1);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task UnregisterHotkeyAsync_WithNullHotkey_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _hotkeyService.UnregisterHotkeyAsync(null!));
    }

    [Fact]
    public async Task UnregisterHotkeyAsync_WithUnregisteredHotkey_ShouldNotThrow()
    {
        // Arrange
        var hotkey = HotkeyDefinition.Create("Unregistered Hotkey", HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey.VK_F4);

        // Act & Assert (should not throw)
        await _hotkeyService.UnregisterHotkeyAsync(hotkey);
    }

    [Fact]
    public async Task UnregisterHotkeyAsync_WithRegisteredHotkey_ShouldUnregisterSuccessfully()
    {
        // Arrange
        var hotkey = HotkeyDefinition.Create("Test Hotkey", HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey.VK_F5);
        await _hotkeyService.RegisterHotkeyAsync(hotkey);

        // Act
        await _hotkeyService.UnregisterHotkeyAsync(hotkey);

        // Assert
        var isRegistered = await _hotkeyService.IsHotkeyRegisteredAsync(hotkey);
        Assert.False(isRegistered);

        var registeredHotkeys = await _hotkeyService.GetRegisteredHotkeysAsync();
        Assert.DoesNotContain(hotkey, registeredHotkeys);
    }

    [Fact]
    public async Task UnregisterAllHotkeysAsync_WithMultipleRegisteredHotkeys_ShouldUnregisterAll()
    {
        // Arrange
        var hotkey1 = HotkeyDefinition.Create("Test Hotkey 1", HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey.VK_F6);
        var hotkey2 = HotkeyDefinition.Create("Test Hotkey 2", HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey.VK_F7);
        var hotkey3 = HotkeyDefinition.Create("Test Hotkey 3", HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey.VK_F8);

        await _hotkeyService.RegisterHotkeyAsync(hotkey1);
        await _hotkeyService.RegisterHotkeyAsync(hotkey2);
        await _hotkeyService.RegisterHotkeyAsync(hotkey3);

        // Act
        await _hotkeyService.UnregisterAllHotkeysAsync();

        // Assert
        var registeredHotkeys = await _hotkeyService.GetRegisteredHotkeysAsync();
        Assert.Empty(registeredHotkeys);

        Assert.False(await _hotkeyService.IsHotkeyRegisteredAsync(hotkey1));
        Assert.False(await _hotkeyService.IsHotkeyRegisteredAsync(hotkey2));
        Assert.False(await _hotkeyService.IsHotkeyRegisteredAsync(hotkey3));
    }

    [Fact]
    public async Task GetRegisteredHotkeysAsync_WithNoRegisteredHotkeys_ShouldReturnEmptyCollection()
    {
        // Act
        var registeredHotkeys = await _hotkeyService.GetRegisteredHotkeysAsync();

        // Assert
        Assert.Empty(registeredHotkeys);
    }

    [Fact]
    public async Task GetRegisteredHotkeysAsync_WithRegisteredHotkeys_ShouldReturnAllRegisteredHotkeys()
    {
        // Arrange
        var hotkey1 = HotkeyDefinition.Create("Test Hotkey 1", HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey.VK_F9);
        var hotkey2 = HotkeyDefinition.Create("Test Hotkey 2", HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey.VK_F10);

        try
        {
            await _hotkeyService.RegisterHotkeyAsync(hotkey1);
            await _hotkeyService.RegisterHotkeyAsync(hotkey2);

            // Act
            var registeredHotkeys = await _hotkeyService.GetRegisteredHotkeysAsync();

            // Assert
            Assert.Equal(2, registeredHotkeys.Count());
            Assert.Contains(hotkey1, registeredHotkeys);
            Assert.Contains(hotkey2, registeredHotkeys);
        }
        finally
        {
            // Cleanup
            try
            {
                await _hotkeyService.UnregisterAllHotkeysAsync();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task IsHotkeyRegisteredAsync_WithNullHotkey_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _hotkeyService.IsHotkeyRegisteredAsync(null!));
    }

    [Fact]
    public async Task IsHotkeyRegisteredAsync_WithUnregisteredHotkey_ShouldReturnFalse()
    {
        // Arrange
        var hotkey = HotkeyDefinition.Create("Unregistered Hotkey", HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey.VK_F11);

        // Act
        var isRegistered = await _hotkeyService.IsHotkeyRegisteredAsync(hotkey);

        // Assert
        Assert.False(isRegistered);
    }

    [Fact]
    public async Task IsHotkeyRegisteredAsync_WithRegisteredHotkey_ShouldReturnTrue()
    {
        // Arrange
        var hotkey = HotkeyDefinition.Create("Test Hotkey", HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey.VK_F12);

        try
        {
            await _hotkeyService.RegisterHotkeyAsync(hotkey);

            // Act
            var isRegistered = await _hotkeyService.IsHotkeyRegisteredAsync(hotkey);

            // Assert
            Assert.True(isRegistered);
        }
        finally
        {
            // Cleanup
            try
            {
                await _hotkeyService.UnregisterHotkeyAsync(hotkey);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    public void Dispose()
    {
        try
        {
            _hotkeyService?.UnregisterAllHotkeysAsync().Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignore cleanup errors
        }
        
        _hotkeyService?.Dispose();
    }

    private sealed class FakeWin32HotkeyApi : IWin32HotkeyApi
    {
        private readonly object _lockObject = new();
        private readonly HashSet<(uint modifiers, uint vk)> _registered = new();
        private uint _lastError;

        public bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk)
        {
            lock (_lockObject)
            {
                var key = (fsModifiers, vk);
                if (_registered.Contains(key))
                {
                    _lastError = Win32Api.ERROR_HOTKEY_ALREADY_REGISTERED;
                    return false;
                }

                _registered.Add(key);
                _lastError = 0;
                return true;
            }
        }

        public bool UnregisterHotKey(IntPtr hWnd, int id)
        {
            // Service tracks IDs and calls us; for test purposes we just succeed.
            _lastError = 0;
            return true;
        }

        public bool PeekMessage(out MSG msg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg)
        {
            msg = default;
            return false;
        }

        public bool TranslateMessage(ref MSG msg) => true;
        public IntPtr DispatchMessage(ref MSG msg) => IntPtr.Zero;
        public bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam) => true;
        public uint GetCurrentThreadId() => 1;
        public uint GetLastError() => _lastError;
    }
}
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroNex.Application.Services;
using MacroNex.Domain.Interfaces;
using MacroNex.Domain.ValueObjects;
using MacroNex.Presentation.Utilities;
using System.Collections.ObjectModel;

namespace MacroNex.Presentation.ViewModels;

/// <summary>
/// ViewModel for debugging and testing Arduino connection and input simulation.
/// </summary>
public partial class DebugViewModel : ObservableObject
{
    private readonly ArduinoConnectionService _arduinoConnectionService;
    private readonly ILoggingService _loggingService;
    private readonly IInputSimulatorFactory _inputSimulatorFactory;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private ArduinoConnectionState arduinoConnectionState = ArduinoConnectionState.Disconnected;

    [ObservableProperty]
    private string? connectedPortName;

    [ObservableProperty]
    private ObservableCollection<string> availableSerialPorts = new();

    [ObservableProperty]
    private string? selectedSerialPort;

    [ObservableProperty]
    private string statusMessage = "就緒";

    [ObservableProperty]
    private bool isAutoConnecting;

    // Input test properties
    [ObservableProperty]
    private int testMoveX = 500;

    [ObservableProperty]
    private int testMoveY = 500;

    [ObservableProperty]
    private int testRelativeX = 50;

    [ObservableProperty]
    private int testRelativeY = 50;

    [ObservableProperty]
    private string testKeyText = "A";

    [ObservableProperty]
    private string testTypeText = "Hello World";

    [ObservableProperty]
    private string currentCursorPosition = "未知";

    public DebugViewModel(
        ArduinoConnectionService arduinoConnectionService,
        ILoggingService loggingService,
        IInputSimulatorFactory inputSimulatorFactory,
        ISettingsService settingsService)
    {
        _arduinoConnectionService = arduinoConnectionService ?? throw new ArgumentNullException(nameof(arduinoConnectionService));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _inputSimulatorFactory = inputSimulatorFactory ?? throw new ArgumentNullException(nameof(inputSimulatorFactory));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        _arduinoConnectionService.ConnectionStateChanged += OnArduinoConnectionStateChanged;

        // Initialize state
        ArduinoConnectionState = _arduinoConnectionService.ConnectionState;
        ConnectedPortName = _arduinoConnectionService.ConnectedPortName;

        // Load available ports
        _ = RefreshPortsAsync();
    }

    private void OnArduinoConnectionStateChanged(object? sender, Domain.Interfaces.ArduinoConnectionStateChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ArduinoConnectionState = e.NewState;
            ConnectedPortName = e.PortName;
            OnPropertyChanged(nameof(ArduinoConnectionState));
            OnPropertyChanged(nameof(ConnectedPortName));
        });
    }

    [RelayCommand]
    private async Task RefreshPortsAsync()
    {
        try
        {
            var ports = await _arduinoConnectionService.GetAvailablePortsAsync();
            AvailableSerialPorts.Clear();
            foreach (var port in ports)
            {
                AvailableSerialPorts.Add(port);
            }
            StatusMessage = $"已刷新，找到 {ports.Count} 個串口";
        }
        catch (Exception ex)
        {
            StatusMessage = $"刷新串口失敗：{ex.Message}";
            await _loggingService.LogErrorAsync("Failed to refresh serial ports", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanAutoConnect))]
    private async Task AutoConnectAsync()
    {
        IsAutoConnecting = true;
        StatusMessage = "正在自動連接 Arduino...";
        AutoConnectCommand.NotifyCanExecuteChanged();

        try
        {
            var success = await _arduinoConnectionService.AutoConnectAsync();
            if (success)
            {
                StatusMessage = $"自動連接成功！已連接到 {_arduinoConnectionService.ConnectedPortName}";
            }
            else
            {
                StatusMessage = "自動連接失敗：未找到可用的 Arduino 設備";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"自動連接失敗：{ex.Message}";
            await _loggingService.LogErrorAsync("Failed to auto-connect to Arduino", ex);
        }
        finally
        {
            IsAutoConnecting = false;
            AutoConnectCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanAutoConnect() => !IsAutoConnecting && ArduinoConnectionState != ArduinoConnectionState.Connected && ArduinoConnectionState != ArduinoConnectionState.Connecting;

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedSerialPort))
            return;

        StatusMessage = $"正在連接到 {SelectedSerialPort}...";
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();

        try
        {
            await _arduinoConnectionService.ConnectAsync(SelectedSerialPort);
            StatusMessage = $"已連接到 {SelectedSerialPort}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"連接失敗：{ex.Message}";
            await _loggingService.LogErrorAsync("Failed to connect to Arduino", ex);
        }
        finally
        {
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanConnect()
        => !string.IsNullOrWhiteSpace(SelectedSerialPort) &&
           ArduinoConnectionState != ArduinoConnectionState.Connected &&
           ArduinoConnectionState != ArduinoConnectionState.Connecting;

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        StatusMessage = "正在斷開連接...";
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();

        try
        {
            await _arduinoConnectionService.DisconnectAsync();
            StatusMessage = "已斷開連接";
        }
        catch (Exception ex)
        {
            StatusMessage = $"斷開連接失敗：{ex.Message}";
            await _loggingService.LogErrorAsync("Failed to disconnect from Arduino", ex);
        }
        finally
        {
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanDisconnect() => ArduinoConnectionState == ArduinoConnectionState.Connected;

    #region Input Simulation Tests

    private async Task<IInputSimulator> GetInputSimulatorAsync()
    {
        var settings = await _settingsService.LoadAsync();
        return _inputSimulatorFactory.GetInputSimulator(settings.GlobalInputMode);
    }

    [RelayCommand]
    private async Task GetCursorPositionAsync()
    {
        try
        {
            var simulator = await GetInputSimulatorAsync();
            var pos = await simulator.GetCursorPositionAsync();
            CurrentCursorPosition = $"X: {pos.X}, Y: {pos.Y}";
            StatusMessage = $"當前游標位置: {CurrentCursorPosition}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"獲取游標位置失敗：{ex.Message}";
            await _loggingService.LogErrorAsync("Failed to get cursor position", ex);
        }
    }

    [RelayCommand]
    private async Task TestMouseMoveAbsoluteAsync()
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            var simulator = _inputSimulatorFactory.GetInputSimulator(settings.GlobalInputMode);
            var pos = new Point(TestMoveX, TestMoveY);

            StatusMessage = $"正在移動滑鼠到 ({TestMoveX}, {TestMoveY})... 模式: {settings.GlobalInputMode}";

            if (settings.GlobalInputMode == InputMode.LowLevel)
            {
                await simulator.SimulateMouseMoveLowLevelAsync(pos);
            }
            else
            {
                await simulator.SimulateMouseMoveAsync(pos);
            }

            StatusMessage = $"滑鼠已移動到 ({TestMoveX}, {TestMoveY}) - 模式: {settings.GlobalInputMode}";
            await _loggingService.LogInfoAsync("Mouse move test", new Dictionary<string, object>
            {
                { "X", TestMoveX },
                { "Y", TestMoveY },
                { "Mode", settings.GlobalInputMode.ToString() }
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"滑鼠移動失敗：{ex.Message}";
            await _loggingService.LogErrorAsync("Failed to move mouse", ex);
        }
    }

    [RelayCommand]
    private async Task TestMouseMoveRelativeAsync()
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            var simulator = _inputSimulatorFactory.GetInputSimulator(settings.GlobalInputMode);

            StatusMessage = $"正在相對移動滑鼠 ({TestRelativeX}, {TestRelativeY})... 模式: {settings.GlobalInputMode}";

            if (settings.GlobalInputMode == InputMode.LowLevel)
            {
                await simulator.SimulateMouseMoveRelativeLowLevelAsync(TestRelativeX, TestRelativeY);
            }
            else
            {
                await simulator.SimulateMouseMoveRelativeAsync(TestRelativeX, TestRelativeY);
            }

            StatusMessage = $"滑鼠已相對移動 ({TestRelativeX}, {TestRelativeY}) - 模式: {settings.GlobalInputMode}";
            await _loggingService.LogInfoAsync("Mouse relative move test", new Dictionary<string, object>
            {
                { "DeltaX", TestRelativeX },
                { "DeltaY", TestRelativeY },
                { "Mode", settings.GlobalInputMode.ToString() }
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"滑鼠相對移動失敗：{ex.Message}";
            await _loggingService.LogErrorAsync("Failed to move mouse relative", ex);
        }
    }

    [RelayCommand]
    private async Task TestMouseClickLeftAsync()
    {
        try
        {
            var simulator = await GetInputSimulatorAsync();
            await simulator.SimulateMouseClickAsync(MouseButton.Left, ClickType.Click);
            StatusMessage = "滑鼠左鍵點擊完成";
        }
        catch (Exception ex)
        {
            StatusMessage = $"滑鼠點擊失敗：{ex.Message}";
            await _loggingService.LogErrorAsync("Failed to click mouse", ex);
        }
    }

    [RelayCommand]
    private async Task TestMouseClickRightAsync()
    {
        try
        {
            var simulator = await GetInputSimulatorAsync();
            await simulator.SimulateMouseClickAsync(MouseButton.Right, ClickType.Click);
            StatusMessage = "滑鼠右鍵點擊完成";
        }
        catch (Exception ex)
        {
            StatusMessage = $"滑鼠右鍵點擊失敗：{ex.Message}";
            await _loggingService.LogErrorAsync("Failed to right click mouse", ex);
        }
    }

    [RelayCommand]
    private async Task TestKeyPressAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(TestKeyText))
            {
                StatusMessage = "請輸入要測試的按鍵";
                return;
            }

            var simulator = await GetInputSimulatorAsync();
            var keyChar = TestKeyText.ToUpperInvariant()[0];
            
            // Try to parse as VirtualKey
            VirtualKey vk;
            if (char.IsLetter(keyChar))
            {
                var vkName = $"VK_{keyChar}";
                if (!Enum.TryParse<VirtualKey>(vkName, out vk))
                {
                    StatusMessage = $"無法解析按鍵: {TestKeyText}";
                    return;
                }
            }
            else if (char.IsDigit(keyChar))
            {
                var vkName = $"VK_{keyChar}";
                if (!Enum.TryParse<VirtualKey>(vkName, out vk))
                {
                    StatusMessage = $"無法解析按鍵: {TestKeyText}";
                    return;
                }
            }
            else
            {
                StatusMessage = $"僅支援字母和數字按鍵測試";
                return;
            }

            StatusMessage = $"正在按下並釋放按鍵 {TestKeyText}...";
            await simulator.SimulateKeyPressAsync(vk, true);
            await Task.Delay(50);
            await simulator.SimulateKeyPressAsync(vk, false);
            StatusMessage = $"按鍵 {TestKeyText} 測試完成";
        }
        catch (Exception ex)
        {
            StatusMessage = $"按鍵測試失敗：{ex.Message}";
            await _loggingService.LogErrorAsync("Failed to test key press", ex);
        }
    }

    [RelayCommand]
    private async Task TestTypeTextAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(TestTypeText))
            {
                StatusMessage = "請輸入要測試的文字";
                return;
            }

            var simulator = await GetInputSimulatorAsync();
            StatusMessage = $"正在輸入文字...";
            await simulator.SimulateKeyboardInputAsync(TestTypeText);
            StatusMessage = $"文字輸入完成: {TestTypeText}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"文字輸入失敗：{ex.Message}";
            await _loggingService.LogErrorAsync("Failed to type text", ex);
        }
    }

    #endregion
}

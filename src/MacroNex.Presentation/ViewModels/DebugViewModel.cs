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

    // Mouse Calibration properties
    [ObservableProperty]
    private bool isCalibrating;

    [ObservableProperty]
    private int calibrationProgress;

    [ObservableProperty]
    private string calibrationStatus = "尚未校準";

    [ObservableProperty]
    private ObservableCollection<CalibrationPointDisplay> calibrationPoints = new();

    [ObservableProperty]
    private bool hasCalibrationData;

    [ObservableProperty]
    private string? lastCalibrationTime;

    [ObservableProperty]
    private string calibrationFormula = "";

    private CancellationTokenSource? _calibrationCts;

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

        // Load existing calibration data
        _ = LoadCalibrationDataAsync();
    }

    private void OnArduinoConnectionStateChanged(object? sender, Domain.Interfaces.ArduinoConnectionStateChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ArduinoConnectionState = e.NewState;
            ConnectedPortName = e.PortName;
            OnPropertyChanged(nameof(ArduinoConnectionState));
            OnPropertyChanged(nameof(ConnectedPortName));

            // Notify all connection-dependent commands to update their CanExecute state
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
            AutoConnectCommand.NotifyCanExecuteChanged();
            StartCalibrationCommand.NotifyCanExecuteChanged();
            ClearCalibrationCommand.NotifyCanExecuteChanged();
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

    #region Mouse Calibration

    private async Task LoadCalibrationDataAsync()
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            if (settings.MouseCalibration != null && settings.MouseCalibration.IsValid)
            {
                HasCalibrationData = true;
                LastCalibrationTime = settings.MouseCalibration.CalibratedAt.ToString("yyyy-MM-dd HH:mm:ss");
                CalibrationStatus = settings.MouseCalibration.GetSummary();
                CalibrationFormula = GenerateFormulaDisplay(settings.MouseCalibration);
                
                // Populate display points
                CalibrationPoints.Clear();
                foreach (var point in settings.MouseCalibration.PointsX)
                {
                    CalibrationPoints.Add(new CalibrationPointDisplay
                    {
                        HidDelta = point.HidDelta,
                        ActualPixelDelta = point.ActualPixelDelta,
                        Axis = "X"
                    });
                }
                foreach (var point in settings.MouseCalibration.PointsY)
                {
                    CalibrationPoints.Add(new CalibrationPointDisplay
                    {
                        HidDelta = point.HidDelta,
                        ActualPixelDelta = point.ActualPixelDelta,
                        Axis = "Y"
                    });
                }
            }
            else
            {
                HasCalibrationData = false;
                CalibrationStatus = "尚未校準";
                CalibrationFormula = "";
            }
        }
        catch (Exception ex)
        {
            await _loggingService.LogErrorAsync("Failed to load calibration data", ex);
        }
    }

    private string GenerateFormulaDisplay(MouseCalibrationData data)
    {
        if (!data.IsValid) return "";

        var avgRatioX = data.PointsX.Count > 0
            ? data.PointsX.Where(p => p.HidDelta != 0).Average(p => p.ActualPixelDelta / p.HidDelta)
            : 1.0;
        var avgRatioY = data.PointsY.Count > 0
            ? data.PointsY.Where(p => p.HidDelta != 0).Average(p => p.ActualPixelDelta / p.HidDelta)
            : 1.0;

        return $"Pixel_X ≈ HID_ΔX × {avgRatioX:F3}\nPixel_Y ≈ HID_ΔY × {avgRatioY:F3}\n(非線性查找表插值)";
    }

    [RelayCommand(CanExecute = nameof(CanStartCalibration))]
    private async Task StartCalibrationAsync()
    {
        if (ArduinoConnectionState != ArduinoConnectionState.Connected)
        {
            StatusMessage = "請先連接 Arduino";
            return;
        }

        IsCalibrating = true;
        CalibrationProgress = 0;
        CalibrationStatus = "正在準備校準，請勿觸碰滑鼠...";
        CalibrationPoints.Clear();
        _calibrationCts = new CancellationTokenSource();

        StartCalibrationCommand.NotifyCanExecuteChanged();
        CancelCalibrationCommand.NotifyCanExecuteChanged();

        try
        {
            // 顯示提示窗口，等待 1 秒
            await ShowCalibrationWarningAsync();
            
            await RunCalibrationAsync(_calibrationCts.Token);
        }
        catch (OperationCanceledException)
        {
            CalibrationStatus = "校準已取消";
            StatusMessage = "校準已取消";
        }
        catch (Exception ex)
        {
            CalibrationStatus = $"校準失敗：{ex.Message}";
            StatusMessage = $"校準失敗：{ex.Message}";
            await _loggingService.LogErrorAsync("Calibration failed", ex);
        }
        finally
        {
            IsCalibrating = false;
            _calibrationCts?.Dispose();
            _calibrationCts = null;
            StartCalibrationCommand.NotifyCanExecuteChanged();
            CancelCalibrationCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanStartCalibration() => !IsCalibrating && ArduinoConnectionState == ArduinoConnectionState.Connected;

    private async Task ShowCalibrationWarningAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // 暗色系配色
            var darkBackground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30));
            var warningColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 200, 60));
            var subTextColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 180));
            var borderColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80));

            var warningWindow = new System.Windows.Window
            {
                Title = "滑鼠校準",
                Width = 350,
                Height = 150,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                WindowStyle = System.Windows.WindowStyle.None,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                Topmost = true,
                ShowInTaskbar = false,
                Background = darkBackground,
                AllowsTransparency = true,
                BorderBrush = borderColor,
                BorderThickness = new System.Windows.Thickness(1)
            };

            var stackPanel = new System.Windows.Controls.StackPanel
            {
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(20)
            };

            var warningText = new System.Windows.Controls.TextBlock
            {
                Text = "請勿移動滑鼠！",
                FontSize = 20,
                FontWeight = System.Windows.FontWeights.Bold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 0, 0, 10),
                Foreground = warningColor
            };

            var subText = new System.Windows.Controls.TextBlock
            {
                Text = "校準即將開始...",
                FontSize = 14,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Foreground = subTextColor
            };

            stackPanel.Children.Add(warningText);
            stackPanel.Children.Add(subText);
            warningWindow.Content = stackPanel;

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                warningWindow.Close();
                tcs.SetResult(true);
            };

            warningWindow.Loaded += (s, e) => timer.Start();
            warningWindow.Show();
        });

        await tcs.Task;
    }

    [RelayCommand(CanExecute = nameof(CanCancelCalibration))]
    private void CancelCalibration()
    {
        _calibrationCts?.Cancel();
    }

    private bool CanCancelCalibration() => IsCalibrating;

    [RelayCommand(CanExecute = nameof(CanClearCalibration))]
    private async Task ClearCalibrationAsync()
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            settings.MouseCalibration = null;
            await _settingsService.SaveAsync(settings);

            HasCalibrationData = false;
            LastCalibrationTime = null;
            CalibrationStatus = "校準數據已清除";
            CalibrationFormula = "";
            CalibrationPoints.Clear();
            StatusMessage = "校準數據已清除";
        }
        catch (Exception ex)
        {
            StatusMessage = $"清除校準數據失敗：{ex.Message}";
            await _loggingService.LogErrorAsync("Failed to clear calibration data", ex);
        }
    }

    private bool CanClearCalibration() => !IsCalibrating && HasCalibrationData;

    private async Task RunCalibrationAsync(CancellationToken cancellationToken)
    {
        // Test deltas for calibration (both positive values, we'll test both directions)
        int[] testDeltas = { 5, 10, 20, 30, 50, 75, 100, 150, 200, 300 };
        
        var pointsX = new List<CalibrationPoint>();
        var pointsY = new List<CalibrationPoint>();

        int totalSteps = testDeltas.Length * 4; // X+, X-, Y+, Y- for each delta
        int currentStep = 0;

        // Get screen center for resetting cursor position
        var screenWidth = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
        var screenHeight = (int)System.Windows.SystemParameters.PrimaryScreenHeight;
        int centerX = screenWidth / 2;
        int centerY = screenHeight / 2;

        foreach (var delta in testDeltas)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Test X+ direction
            CalibrationStatus = $"測試 X 軸正向移動 (HID Δ={delta})...";
            var resultXPos = await TestSingleCalibrationPointAsync(delta, 0, centerX, centerY, cancellationToken);
            if (resultXPos.HasValue)
            {
                pointsX.Add(new CalibrationPoint { HidDelta = delta, ActualPixelDelta = resultXPos.Value });
                AddCalibrationPointDisplay(delta, resultXPos.Value, "X+");
            }
            currentStep++;
            CalibrationProgress = (int)(currentStep * 100.0 / totalSteps);

            // Test X- direction
            cancellationToken.ThrowIfCancellationRequested();
            CalibrationStatus = $"測試 X 軸負向移動 (HID Δ={-delta})...";
            var resultXNeg = await TestSingleCalibrationPointAsync(-delta, 0, centerX, centerY, cancellationToken);
            if (resultXNeg.HasValue)
            {
                // Store absolute values for the lookup table
                var absResult = Math.Abs(resultXNeg.Value);
                // Average with positive result if available
                var existingPoint = pointsX.FirstOrDefault(p => p.HidDelta == delta);
                if (existingPoint != null)
                {
                    existingPoint.ActualPixelDelta = (existingPoint.ActualPixelDelta + absResult) / 2;
                }
                AddCalibrationPointDisplay(-delta, resultXNeg.Value, "X-");
            }
            currentStep++;
            CalibrationProgress = (int)(currentStep * 100.0 / totalSteps);

            // Test Y+ direction
            cancellationToken.ThrowIfCancellationRequested();
            CalibrationStatus = $"測試 Y 軸正向移動 (HID Δ={delta})...";
            var resultYPos = await TestSingleCalibrationPointAsync(0, delta, centerX, centerY, cancellationToken);
            if (resultYPos.HasValue)
            {
                pointsY.Add(new CalibrationPoint { HidDelta = delta, ActualPixelDelta = resultYPos.Value });
                AddCalibrationPointDisplay(delta, resultYPos.Value, "Y+");
            }
            currentStep++;
            CalibrationProgress = (int)(currentStep * 100.0 / totalSteps);

            // Test Y- direction
            cancellationToken.ThrowIfCancellationRequested();
            CalibrationStatus = $"測試 Y 軸負向移動 (HID Δ={-delta})...";
            var resultYNeg = await TestSingleCalibrationPointAsync(0, -delta, centerX, centerY, cancellationToken);
            if (resultYNeg.HasValue)
            {
                var absResult = Math.Abs(resultYNeg.Value);
                var existingPoint = pointsY.FirstOrDefault(p => p.HidDelta == delta);
                if (existingPoint != null)
                {
                    existingPoint.ActualPixelDelta = (existingPoint.ActualPixelDelta + absResult) / 2;
                }
                AddCalibrationPointDisplay(-delta, resultYNeg.Value, "Y-");
            }
            currentStep++;
            CalibrationProgress = (int)(currentStep * 100.0 / totalSteps);
        }

        // Save calibration data
        var calibrationData = new MouseCalibrationData
        {
            CalibratedAt = DateTime.Now,
            PointsX = pointsX,
            PointsY = pointsY
        };

        var settings = await _settingsService.LoadAsync();
        settings.MouseCalibration = calibrationData;
        await _settingsService.SaveAsync(settings);

        // Update UI
        HasCalibrationData = true;
        LastCalibrationTime = calibrationData.CalibratedAt.ToString("yyyy-MM-dd HH:mm:ss");
        CalibrationStatus = calibrationData.GetSummary();
        CalibrationFormula = GenerateFormulaDisplay(calibrationData);
        CalibrationProgress = 100;
        StatusMessage = "校準完成！";

        await _loggingService.LogInfoAsync("Mouse calibration completed", new Dictionary<string, object>
        {
            { "PointsX", pointsX.Count },
            { "PointsY", pointsY.Count }
        });
    }

    private async Task<double?> TestSingleCalibrationPointAsync(int deltaX, int deltaY, int resetX, int resetY, CancellationToken cancellationToken)
    {
        try
        {
            // Move cursor to center first using SetCursorPos (bypass Arduino for reset)
            SetCursorPosWin32(resetX, resetY);
            await Task.Delay(100, cancellationToken);

            // Get actual starting position
            var startPos = GetCursorPosWin32();
            
            // Send HID move command via Arduino
            var command = new ArduinoMouseMoveRelativeCommand(deltaX, deltaY);
            await _arduinoConnectionService.SendCommandAsync(command);
            
            // Wait for the move to complete
            await Task.Delay(100, cancellationToken);

            // Get ending position
            var endPos = GetCursorPosWin32();

            // Calculate actual pixel movement
            double actualDelta = deltaX != 0 
                ? endPos.X - startPos.X 
                : endPos.Y - startPos.Y;

            return actualDelta;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await _loggingService.LogErrorAsync($"Failed to test calibration point ({deltaX}, {deltaY})", ex);
            return null;
        }
    }

    private void AddCalibrationPointDisplay(int hidDelta, double actualPixelDelta, string direction)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            CalibrationPoints.Add(new CalibrationPointDisplay
            {
                HidDelta = hidDelta,
                ActualPixelDelta = actualPixelDelta,
                Axis = direction
            });
        });
    }

    // Win32 API for direct cursor manipulation during calibration
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private void SetCursorPosWin32(int x, int y)
    {
        SetCursorPos(x, y);
    }

    private Point GetCursorPosWin32()
    {
        GetCursorPos(out POINT pt);
        return new Point(pt.X, pt.Y);
    }

    #endregion
}

/// <summary>
/// Display model for calibration points in the DataGrid.
/// </summary>
public class CalibrationPointDisplay
{
    public int HidDelta { get; set; }
    public double ActualPixelDelta { get; set; }
    public string Axis { get; set; } = "";
    public double Ratio => HidDelta != 0 ? ActualPixelDelta / HidDelta : 0;
}

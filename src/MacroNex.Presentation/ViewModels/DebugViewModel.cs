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

    // Manual calibration input properties
    [ObservableProperty]
    private double manualRatioX = 1.0;

    [ObservableProperty]
    private double manualRatioY = 1.0;

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
                
                // Calculate average ratios for manual input fields
                var avgRatioX = settings.MouseCalibration.PointsX.Count > 0
                    ? settings.MouseCalibration.PointsX.Where(p => p.HidDelta != 0).Average(p => p.ActualPixelDelta / p.HidDelta)
                    : 1.0;
                var avgRatioY = settings.MouseCalibration.PointsY.Count > 0
                    ? settings.MouseCalibration.PointsY.Where(p => p.HidDelta != 0).Average(p => p.ActualPixelDelta / p.HidDelta)
                    : 1.0;
                ManualRatioX = avgRatioX;
                ManualRatioY = avgRatioY;
                
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
                ManualRatioX = 1.0;
                ManualRatioY = 1.0;
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

        var sb = new System.Text.StringBuilder();

        // 顯示平均比例
        var avgRatioX = data.PointsX.Count > 0
            ? data.PointsX.Where(p => p.HidDelta != 0).Average(p => p.ActualPixelDelta / p.HidDelta)
            : 1.0;
        var avgRatioY = data.PointsY.Count > 0
            ? data.PointsY.Where(p => p.HidDelta != 0).Average(p => p.ActualPixelDelta / p.HidDelta)
            : 1.0;

        sb.AppendLine($"平均比例: X={avgRatioX:F3}, Y={avgRatioY:F3}");

        // 根據曲線類型顯示不同信息
        string curveTypeStr = data.CurveType switch
        {
            AccelerationCurveType.WindowsEnhanced => "Windows 增強指標精確度",
            AccelerationCurveType.Polynomial => "多項式擬合",
            _ => "線性"
        };
        sb.AppendLine($"曲線類型: {curveTypeStr}");

        // 如果是 Windows 加速模式，顯示加速曲線控制點
        if (data.CurveType == AccelerationCurveType.WindowsEnhanced && 
            data.AccelerationThresholds.Length >= 2)
        {
            sb.AppendLine("加速曲線控制點:");
            for (int i = 0; i < data.AccelerationThresholds.Length; i++)
            {
                sb.AppendLine($"  速度 {data.AccelerationThresholds[i]:F1} → 增益 {data.AccelerationGains[i]:F3}");
            }
        }
        // 顯示多項式模型（如果有）
        else if (data.PolynomialCoefficientsX.Length >= 2)
        {
            sb.Append("X 軸模型: HID = ");
            sb.Append(FormatPolynomial(data.PolynomialCoefficientsX));
            sb.AppendLine();
            
            if (data.PolynomialCoefficientsY.Length >= 2)
            {
                sb.Append("Y 軸模型: HID = ");
                sb.Append(FormatPolynomial(data.PolynomialCoefficientsY));
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatPolynomial(double[] coefficients)
    {
        if (coefficients.Length == 0) return "N/A";
        
        var terms = new List<string>();
        
        if (Math.Abs(coefficients[0]) > 0.001)
            terms.Add($"{coefficients[0]:F2}");
        
        if (coefficients.Length > 1 && Math.Abs(coefficients[1]) > 0.001)
            terms.Add($"{coefficients[1]:F3}×P");
        
        if (coefficients.Length > 2 && Math.Abs(coefficients[2]) > 0.00001)
            terms.Add($"{coefficients[2]:F5}×P²");

        return terms.Count > 0 ? string.Join(" + ", terms) : "0";
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

    [RelayCommand(CanExecute = nameof(CanSaveManualCalibration))]
    private async Task SaveManualCalibrationAsync()
    {
        try
        {
            // Create calibration data from manual ratios
            // We'll create a simple two-point calibration: one at 0 and one at a reference point
            var calibrationData = new MouseCalibrationData
            {
                CalibratedAt = DateTime.Now,
                PointsX = new List<CalibrationPoint>
                {
                    new CalibrationPoint { HidDelta = 0, ActualPixelDelta = 0 },
                    new CalibrationPoint { HidDelta = 100, ActualPixelDelta = 100 * ManualRatioX }
                },
                PointsY = new List<CalibrationPoint>
                {
                    new CalibrationPoint { HidDelta = 0, ActualPixelDelta = 0 },
                    new CalibrationPoint { HidDelta = 100, ActualPixelDelta = 100 * ManualRatioY }
                }
            };

            var settings = await _settingsService.LoadAsync();
            settings.MouseCalibration = calibrationData;
            await _settingsService.SaveAsync(settings);

            // Update UI
            HasCalibrationData = true;
            LastCalibrationTime = calibrationData.CalibratedAt.ToString("yyyy-MM-dd HH:mm:ss") + " (手動)";
            CalibrationStatus = $"手動設定 - X: {ManualRatioX:F3}, Y: {ManualRatioY:F3}";
            CalibrationFormula = $"Pixel_X = HID_ΔX × {ManualRatioX:F3}\nPixel_Y = HID_ΔY × {ManualRatioY:F3}\n(手動設定線性比例)";
            
            // Update display points
            CalibrationPoints.Clear();
            CalibrationPoints.Add(new CalibrationPointDisplay { HidDelta = 100, ActualPixelDelta = 100 * ManualRatioX, Axis = "X" });
            CalibrationPoints.Add(new CalibrationPointDisplay { HidDelta = 100, ActualPixelDelta = 100 * ManualRatioY, Axis = "Y" });

            StatusMessage = "手動校準數據已保存";
            await _loggingService.LogInfoAsync("Manual mouse calibration saved", new Dictionary<string, object>
            {
                { "RatioX", ManualRatioX },
                { "RatioY", ManualRatioY }
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存手動校準失敗：{ex.Message}";
            await _loggingService.LogErrorAsync("Failed to save manual calibration", ex);
        }
    }

    private bool CanSaveManualCalibration() => !IsCalibrating && ManualRatioX > 0 && ManualRatioY > 0;

    private async Task RunCalibrationAsync(CancellationToken cancellationToken)
    {
        // 智能測試點分布：
        // - 小範圍密集（捕捉低速非線性）
        // - 中範圍適中
        // - 大範圍稀疏（驗證線性區域）
        int[] testDeltas = { 3, 5, 8, 12, 18, 25, 35, 50, 70, 100, 150, 200 };
        
        // 每個點採樣次數（取中位數減少噪音）
        const int SamplesPerPoint = 3;
        
        var rawPointsX = new List<(int delta, List<double> samples)>();
        var rawPointsY = new List<(int delta, List<double> samples)>();

        int totalSteps = testDeltas.Length * 2 * SamplesPerPoint;
        int currentStep = 0;

        // Get screen center for resetting cursor position
        var screenWidth = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
        var screenHeight = (int)System.Windows.SystemParameters.PrimaryScreenHeight;
        int centerX = screenWidth / 2;
        int centerY = screenHeight / 2;

        // Phase 1: 收集多次採樣數據
        CalibrationStatus = "階段 1/3：收集採樣數據...";
        
        foreach (var delta in testDeltas)
        {
            var samplesXPos = new List<double>();
            var samplesXNeg = new List<double>();
            var samplesYPos = new List<double>();
            var samplesYNeg = new List<double>();

            for (int sample = 0; sample < SamplesPerPoint; sample++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Test X axis (both directions)
                CalibrationStatus = $"測試 X 軸 (HID Δ={delta}) - 採樣 {sample + 1}/{SamplesPerPoint}...";
                
                var resultXPos = await TestSingleCalibrationPointAsync(delta, 0, centerX, centerY, cancellationToken);
                if (resultXPos.HasValue && resultXPos.Value > 0)
                    samplesXPos.Add(resultXPos.Value);
                currentStep++;
                
                var resultXNeg = await TestSingleCalibrationPointAsync(-delta, 0, centerX, centerY, cancellationToken);
                if (resultXNeg.HasValue)
                    samplesXNeg.Add(Math.Abs(resultXNeg.Value));

                // Test Y axis (both directions)  
                CalibrationStatus = $"測試 Y 軸 (HID Δ={delta}) - 採樣 {sample + 1}/{SamplesPerPoint}...";
                
                var resultYPos = await TestSingleCalibrationPointAsync(0, delta, centerX, centerY, cancellationToken);
                if (resultYPos.HasValue && resultYPos.Value > 0)
                    samplesYPos.Add(resultYPos.Value);
                currentStep++;
                
                var resultYNeg = await TestSingleCalibrationPointAsync(0, -delta, centerX, centerY, cancellationToken);
                if (resultYNeg.HasValue)
                    samplesYNeg.Add(Math.Abs(resultYNeg.Value));

                CalibrationProgress = (int)(currentStep * 60.0 / totalSteps); // 60% for sampling
            }

            // 合併正負方向的採樣
            var allSamplesX = samplesXPos.Concat(samplesXNeg).ToList();
            var allSamplesY = samplesYPos.Concat(samplesYNeg).ToList();
            
            if (allSamplesX.Count > 0)
                rawPointsX.Add((delta, allSamplesX));
            if (allSamplesY.Count > 0)
                rawPointsY.Add((delta, allSamplesY));
        }

        // Phase 2: 異常值過濾和數據處理
        CalibrationStatus = "階段 2/3：分析和過濾數據...";
        CalibrationProgress = 70;
        
        var pointsX = ProcessCalibrationSamples(rawPointsX);
        var pointsY = ProcessCalibrationSamples(rawPointsY);

        // 顯示處理後的校準點
        CalibrationPoints.Clear();
        foreach (var point in pointsX)
            AddCalibrationPointDisplay(point.HidDelta, point.ActualPixelDelta, "X");
        foreach (var point in pointsY)
            AddCalibrationPointDisplay(point.HidDelta, point.ActualPixelDelta, "Y");

        // Phase 3: 擬合非線性模型
        CalibrationStatus = "階段 3/3：擬合非線性模型...";
        CalibrationProgress = 85;

        var calibrationData = new MouseCalibrationData
        {
            CalibratedAt = DateTime.Now,
            PointsX = pointsX,
            PointsY = pointsY
        };

        // 執行多項式擬合
        calibrationData.FitPolynomial(degree: 2);

        var settings = await _settingsService.LoadAsync();
        settings.MouseCalibration = calibrationData;
        await _settingsService.SaveAsync(settings);

        // Update UI
        HasCalibrationData = true;
        LastCalibrationTime = calibrationData.CalibratedAt.ToString("yyyy-MM-dd HH:mm:ss");
        CalibrationStatus = calibrationData.GetSummary();
        CalibrationFormula = GenerateFormulaDisplay(calibrationData);
        CalibrationProgress = 100;
        
        string accelerationInfo = calibrationData.HasMouseAcceleration ? "（檢測到滑鼠加速）" : "（線性模式）";
        StatusMessage = $"校準完成！{accelerationInfo}";

        // Update manual ratio inputs with calibrated values
        var avgRatioX = pointsX.Count > 0
            ? pointsX.Where(p => p.HidDelta != 0).Average(p => p.ActualPixelDelta / p.HidDelta)
            : 1.0;
        var avgRatioY = pointsY.Count > 0
            ? pointsY.Where(p => p.HidDelta != 0).Average(p => p.ActualPixelDelta / p.HidDelta)
            : 1.0;
        ManualRatioX = avgRatioX;
        ManualRatioY = avgRatioY;

        await _loggingService.LogInfoAsync("Mouse calibration completed", new Dictionary<string, object>
        {
            { "PointsX", pointsX.Count },
            { "PointsY", pointsY.Count },
            { "HasAcceleration", calibrationData.HasMouseAcceleration },
            { "PolynomialDegree", 2 }
        });
    }

    /// <summary>
    /// 處理採樣數據：取中位數並過濾異常值
    /// </summary>
    private List<CalibrationPoint> ProcessCalibrationSamples(List<(int delta, List<double> samples)> rawPoints)
    {
        var result = new List<CalibrationPoint>();

        foreach (var (delta, samples) in rawPoints)
        {
            if (samples.Count == 0) continue;

            // 過濾明顯的異常值（超出 IQR 1.5 倍）
            var filteredSamples = FilterOutliers(samples);
            
            if (filteredSamples.Count == 0) continue;

            // 取中位數作為最終值
            var sortedSamples = filteredSamples.OrderBy(x => x).ToList();
            double median = sortedSamples.Count % 2 == 0
                ? (sortedSamples[sortedSamples.Count / 2 - 1] + sortedSamples[sortedSamples.Count / 2]) / 2.0
                : sortedSamples[sortedSamples.Count / 2];

            result.Add(new CalibrationPoint
            {
                HidDelta = delta,
                ActualPixelDelta = median
            });
        }

        return result;
    }

    /// <summary>
    /// 使用 IQR 方法過濾異常值
    /// </summary>
    private List<double> FilterOutliers(List<double> values)
    {
        if (values.Count < 3) return values;

        var sorted = values.OrderBy(x => x).ToList();
        int q1Index = sorted.Count / 4;
        int q3Index = 3 * sorted.Count / 4;
        
        double q1 = sorted[q1Index];
        double q3 = sorted[q3Index];
        double iqr = q3 - q1;
        
        double lowerBound = q1 - 1.5 * iqr;
        double upperBound = q3 + 1.5 * iqr;

        return values.Where(v => v >= lowerBound && v <= upperBound).ToList();
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

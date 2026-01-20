using System.Windows;
using System.Windows.Input;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Presentation.ViewModels;

namespace MacroStudio.Presentation.Views;

public partial class HotkeyCaptureWindow : Window
{
    private HotkeyModifiers _currentModifiers = HotkeyModifiers.None;
    private VirtualKey? _currentKey = null;
    private bool _isCapturing = true;
    private HotkeyTriggerMode _selectedTriggerMode = HotkeyTriggerMode.Once;
    private bool _swallowKeystroke = true;

    public HotkeyDefinition? ResultHotkey { get; private set; }

    public HotkeyCaptureWindow(HotkeyDefinition? existingHotkey = null)
    {
        InitializeComponent();
        
        if (existingHotkey != null)
        {
            _currentModifiers = existingHotkey.Modifiers;
            _currentKey = existingHotkey.Key;
            _selectedTriggerMode = existingHotkey.TriggerMode;
            _swallowKeystroke = existingHotkey.SwallowKeystroke;
            SwallowKeystrokeCheck.IsChecked = _swallowKeystroke;
            
            // Update radio buttons based on existing hotkey mode
            if (_selectedTriggerMode == HotkeyTriggerMode.RepeatWhileHeld)
            {
                RepeatModeRadio.IsChecked = true;
            }
            else
            {
                OnceModeRadio.IsChecked = true;
            }
            
            UpdateDisplay();
        }

        // Focus the window to capture keys
        Loaded += (s, e) => Focus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isCapturing) return;

        e.Handled = true;

        // Update modifiers
        _currentModifiers = HotkeyModifiers.None;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            _currentModifiers |= HotkeyModifiers.Control;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            _currentModifiers |= HotkeyModifiers.Alt;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            _currentModifiers |= HotkeyModifiers.Shift;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            _currentModifiers |= HotkeyModifiers.Windows;

        // Convert WPF Key to VirtualKey
        var virtualKey = ConvertWpfKeyToVirtualKey(e.Key);
        
        // Ignore modifier keys as the main key
        if (virtualKey.HasValue && !IsModifierKey(virtualKey.Value))
        {
            _currentKey = virtualKey.Value;
            UpdateDisplay();
        }
        else
        {
            // Update display even if only modifiers are pressed (for visual feedback)
            UpdateDisplay();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Prevent default behavior
        e.Handled = true;
    }

    private VirtualKey? ConvertWpfKeyToVirtualKey(Key key)
    {
        return key switch
        {
            Key.Back => VirtualKey.VK_BACK,
            Key.Tab => VirtualKey.VK_TAB,
            Key.Return => VirtualKey.VK_RETURN,
            Key.Escape => VirtualKey.VK_ESCAPE,
            Key.Space => VirtualKey.VK_SPACE,
            Key.PageUp => VirtualKey.VK_PRIOR,
            Key.PageDown => VirtualKey.VK_NEXT,
            Key.End => VirtualKey.VK_END,
            Key.Home => VirtualKey.VK_HOME,
            Key.Left => VirtualKey.VK_LEFT,
            Key.Up => VirtualKey.VK_UP,
            Key.Right => VirtualKey.VK_RIGHT,
            Key.Down => VirtualKey.VK_DOWN,
            Key.Insert => VirtualKey.VK_INSERT,
            Key.Delete => VirtualKey.VK_DELETE,
            Key.D0 => VirtualKey.VK_0,
            Key.D1 => VirtualKey.VK_1,
            Key.D2 => VirtualKey.VK_2,
            Key.D3 => VirtualKey.VK_3,
            Key.D4 => VirtualKey.VK_4,
            Key.D5 => VirtualKey.VK_5,
            Key.D6 => VirtualKey.VK_6,
            Key.D7 => VirtualKey.VK_7,
            Key.D8 => VirtualKey.VK_8,
            Key.D9 => VirtualKey.VK_9,
            Key.A => VirtualKey.VK_A,
            Key.B => VirtualKey.VK_B,
            Key.C => VirtualKey.VK_C,
            Key.D => VirtualKey.VK_D,
            Key.E => VirtualKey.VK_E,
            Key.F => VirtualKey.VK_F,
            Key.G => VirtualKey.VK_G,
            Key.H => VirtualKey.VK_H,
            Key.I => VirtualKey.VK_I,
            Key.J => VirtualKey.VK_J,
            Key.K => VirtualKey.VK_K,
            Key.L => VirtualKey.VK_L,
            Key.M => VirtualKey.VK_M,
            Key.N => VirtualKey.VK_N,
            Key.O => VirtualKey.VK_O,
            Key.P => VirtualKey.VK_P,
            Key.Q => VirtualKey.VK_Q,
            Key.R => VirtualKey.VK_R,
            Key.S => VirtualKey.VK_S,
            Key.T => VirtualKey.VK_T,
            Key.U => VirtualKey.VK_U,
            Key.V => VirtualKey.VK_V,
            Key.W => VirtualKey.VK_W,
            Key.X => VirtualKey.VK_X,
            Key.Y => VirtualKey.VK_Y,
            Key.Z => VirtualKey.VK_Z,
            Key.F1 => VirtualKey.VK_F1,
            Key.F2 => VirtualKey.VK_F2,
            Key.F3 => VirtualKey.VK_F3,
            Key.F4 => VirtualKey.VK_F4,
            Key.F5 => VirtualKey.VK_F5,
            Key.F6 => VirtualKey.VK_F6,
            Key.F7 => VirtualKey.VK_F7,
            Key.F8 => VirtualKey.VK_F8,
            Key.F9 => VirtualKey.VK_F9,
            Key.F10 => VirtualKey.VK_F10,
            Key.F11 => VirtualKey.VK_F11,
            Key.F12 => VirtualKey.VK_F12,
            Key.NumPad0 => VirtualKey.VK_NUMPAD0,
            Key.NumPad1 => VirtualKey.VK_NUMPAD1,
            Key.NumPad2 => VirtualKey.VK_NUMPAD2,
            Key.NumPad3 => VirtualKey.VK_NUMPAD3,
            Key.NumPad4 => VirtualKey.VK_NUMPAD4,
            Key.NumPad5 => VirtualKey.VK_NUMPAD5,
            Key.NumPad6 => VirtualKey.VK_NUMPAD6,
            Key.NumPad7 => VirtualKey.VK_NUMPAD7,
            Key.NumPad8 => VirtualKey.VK_NUMPAD8,
            Key.NumPad9 => VirtualKey.VK_NUMPAD9,
            Key.Multiply => VirtualKey.VK_MULTIPLY,
            Key.Add => VirtualKey.VK_ADD,
            Key.Subtract => VirtualKey.VK_SUBTRACT,
            Key.Decimal => VirtualKey.VK_DECIMAL,
            Key.Divide => VirtualKey.VK_DIVIDE,
            _ => null
        };
    }

    private bool IsModifierKey(VirtualKey key)
    {
        return key == VirtualKey.VK_SHIFT ||
               key == VirtualKey.VK_CONTROL ||
               key == VirtualKey.VK_MENU ||
               key == VirtualKey.VK_LWIN ||
               key == VirtualKey.VK_RWIN ||
               key == VirtualKey.VK_LSHIFT ||
               key == VirtualKey.VK_RSHIFT ||
               key == VirtualKey.VK_LCONTROL ||
               key == VirtualKey.VK_RCONTROL ||
               key == VirtualKey.VK_LMENU ||
               key == VirtualKey.VK_RMENU;
    }

    private void UpdateDisplay()
    {
        if (_currentKey.HasValue)
        {
            var hotkey = new HotkeyDefinition(
                Guid.NewGuid(),
                "Script Hotkey",
                _currentModifiers,
                _currentKey.Value,
                _selectedTriggerMode,
                SwallowKeystrokeCheck.IsChecked == true
            );
            
            if (hotkey.IsValid())
            {
                HotkeyDisplayText.Text = hotkey.GetDisplayString();
                IsHotkeyValid = true;
            }
            else
            {
                HotkeyDisplayText.Text = "無效的熱鍵組合";
                HotkeyDisplayText.Foreground = System.Windows.Media.Brushes.Red;
                IsHotkeyValid = false;
            }
        }
        else if (_currentModifiers != HotkeyModifiers.None)
        {
            HotkeyDisplayText.Text = $"{_currentModifiers.GetDisplayString()} + ...";
            IsHotkeyValid = false;
        }
        else
        {
            HotkeyDisplayText.Text = "請按下按鍵...";
            HotkeyDisplayText.Foreground = (System.Windows.Media.Brush)FindResource("TextBrush");
            IsHotkeyValid = false;
        }
    }

    public bool IsHotkeyValid
    {
        get => (bool)GetValue(IsHotkeyValidProperty);
        set => SetValue(IsHotkeyValidProperty, value);
    }

    public static readonly DependencyProperty IsHotkeyValidProperty =
        DependencyProperty.Register(nameof(IsHotkeyValid), typeof(bool), typeof(HotkeyCaptureWindow), new PropertyMetadata(false));

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _currentModifiers = HotkeyModifiers.None;
        _currentKey = null;
        UpdateDisplay();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ResultHotkey = null;
        DialogResult = false;
        Close();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentKey.HasValue)
        {
            ResultHotkey = new HotkeyDefinition(
                Guid.NewGuid(),
                "Script Hotkey",
                _currentModifiers,
                _currentKey.Value,
                _selectedTriggerMode,
                SwallowKeystrokeCheck.IsChecked == true
            );
            
            if (ResultHotkey.IsValid())
            {
                DialogResult = true;
                Close();
            }
        }
    }

    private void TriggerModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender == OnceModeRadio)
        {
            _selectedTriggerMode = HotkeyTriggerMode.Once;
        }
        else if (sender == RepeatModeRadio)
        {
            _selectedTriggerMode = HotkeyTriggerMode.RepeatWhileHeld;
        }
    }
}

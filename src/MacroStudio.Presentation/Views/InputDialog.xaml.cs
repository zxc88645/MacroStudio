using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace MacroStudio.Presentation.Views;

public partial class InputDialog : Window
{
    private readonly Vm _vm;

    public InputDialog(string title, string subtitle, string label, string initialValue = "")
    {
        InitializeComponent();
        _vm = new Vm(title, subtitle, label, initialValue);
        _vm.RequestClose += (_, ok) =>
        {
            DialogResult = ok;
            Close();
        };
        DataContext = _vm;
    }

    public string ValueText => _vm.ValueText;

    private sealed partial class Vm : ObservableObject
    {
        public event EventHandler<bool>? RequestClose;

        [ObservableProperty] private string titleText;
        [ObservableProperty] private string subtitleText;
        [ObservableProperty] private string labelText;
        [ObservableProperty] private string valueText;

        public Vm(string title, string subtitle, string label, string initialValue)
        {
            titleText = title;
            subtitleText = subtitle;
            labelText = label;
            valueText = initialValue;
        }

        [RelayCommand]
        private void Ok() => RequestClose?.Invoke(this, true);

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke(this, false);
    }
}


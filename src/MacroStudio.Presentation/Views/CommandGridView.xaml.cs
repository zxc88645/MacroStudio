using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit;
using MacroStudio.Presentation.ViewModels;

namespace MacroStudio.Presentation.Views;

public partial class CommandGridView : UserControl
{
    public CommandGridView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (AddMouseButton != null)
        {
            AddMouseButton.Click -= OnAddMouseButtonClick;
            AddMouseButton.Click += OnAddMouseButtonClick;
        }

        // Configure AvalonEdit options
        if (ScriptEditor != null)
        {
            ScriptEditor.Options.EnableHyperlinks = false;
            ScriptEditor.Options.EnableEmailHyperlinks = false;
            ScriptEditor.Options.EnableRectangularSelection = true;
            ScriptEditor.Options.EnableVirtualSpace = false;
            ScriptEditor.Options.IndentationSize = 4;
            ScriptEditor.Options.ConvertTabsToSpaces = false;
            ScriptEditor.Options.AllowScrollBelowDocument = false;

            // Keep VM caret/selection in sync so commands can insert at caret.
            ScriptEditor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
            ScriptEditor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
            ScriptEditor.TextArea.SelectionChanged -= OnSelectionChanged;
            ScriptEditor.TextArea.SelectionChanged += OnSelectionChanged;

            // Initial sync
            SyncCaretAndSelection();
        }
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e) => SyncCaretAndSelection();

    private void OnSelectionChanged(object? sender, EventArgs e) => SyncCaretAndSelection();

    private void SyncCaretAndSelection()
    {
        if (ScriptEditor == null) return;

        if (DataContext is MainViewModel mainVm && mainVm.CommandGrid != null)
        {
            mainVm.CommandGrid.CaretOffset = ScriptEditor.CaretOffset;
            mainVm.CommandGrid.SelectionStart = ScriptEditor.SelectionStart;
            mainVm.CommandGrid.SelectionLength = ScriptEditor.SelectionLength;
        }
    }

    private void OnAddMouseButtonClick(object sender, RoutedEventArgs e)
    {
        if (AddMouseButton?.ContextMenu == null)
            return;

        AddMouseButton.ContextMenu.PlacementTarget = AddMouseButton;
        AddMouseButton.ContextMenu.IsOpen = true;
    }
}


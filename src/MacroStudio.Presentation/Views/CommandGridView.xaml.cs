using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit;

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


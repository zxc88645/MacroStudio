using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Document;
using System.Windows.Media;
using MacroNex.Presentation.ViewModels;
using MacroNex.Presentation.Utilities;

namespace MacroNex.Presentation.Views;

public partial class CommandGridView : UserControl
{
    private AvalonEditTextMarkerService? _markerService;
    private CommandGridViewModel? _boundVm;
    private ErrorLineRenderer? _errorLineRenderer;

    public CommandGridView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
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

            // Diagnostics underline renderer
            _markerService ??= new AvalonEditTextMarkerService(ScriptEditor.Document);
            if (!ScriptEditor.TextArea.TextView.BackgroundRenderers.Contains(_markerService))
            {
                ScriptEditor.TextArea.TextView.BackgroundRenderers.Add(_markerService);
            }

            // Even more reliable: draw a full-width error line background/underline directly in the TextView.
            _errorLineRenderer ??= new ErrorLineRenderer();
            if (!ScriptEditor.TextArea.TextView.BackgroundRenderers.Contains(_errorLineRenderer))
            {
                ScriptEditor.TextArea.TextView.BackgroundRenderers.Add(_errorLineRenderer);
            }

            TryBindDiagnostics();
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        TryBindDiagnostics();
    }

    private void TryBindDiagnostics()
    {
        if (DataContext is not MainViewModel mainVm)
            return;

        if (_boundVm != null)
            _boundVm.DiagnosticsChanged -= OnDiagnosticsChanged;

        _boundVm = mainVm.CommandGrid;
        _boundVm.DiagnosticsChanged -= OnDiagnosticsChanged;
        _boundVm.DiagnosticsChanged += OnDiagnosticsChanged;

        RenderDiagnostics(_boundVm);
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

    private void OnDiagnosticsChanged(object? sender, EventArgs e)
    {
        if (sender is CommandGridViewModel vm)
        {
            RenderDiagnostics(vm);
        }
    }

    private void RenderDiagnostics(CommandGridViewModel vm)
    {
        if (ScriptEditor?.Document == null || _markerService == null)
            return;

        _markerService.Clear();
        ScriptEditor.ToolTip = null;
        _errorLineRenderer?.Clear();

        if (!vm.HasDiagnostic || vm.DiagnosticLine <= 0)
        {
            ScriptEditor.TextArea.TextView.InvalidateVisual();
            return;
        }

        try
        {
            var line = ScriptEditor.Document.GetLineByNumber(vm.DiagnosticLine);
            // Underline the whole line to avoid cases where the reported column is at EOF/EOL,
            // which can yield no geometry rectangles (thus no visible underline).
            var offset = line.Offset;
            var length = Math.Max(1, line.Length);
            var msg = vm.DiagnosticMessage ?? string.Empty;
            _markerService.SetSingleMarker(offset, length, msg);
            ScriptEditor.ToolTip = msg;

            // Highlight the entire error line (full width) so it's always visible on dark theme.
            _errorLineRenderer?.SetErrorLine(line.LineNumber);

            ScriptEditor.TextArea.TextView.InvalidateVisual();
        }
        catch
        {
            // best effort; never crash UI due to diagnostics
        }
    }

    private sealed class ErrorLineRenderer : IBackgroundRenderer
    {
        private int _errorLine = -1;
        private readonly Brush _bg = new SolidColorBrush(Color.FromArgb(60, 220, 38, 38)); // red w/ alpha
        private readonly Pen _underline = new Pen(Brushes.IndianRed, 1.5);

        public ErrorLineRenderer()
        {
            _bg.Freeze();
            _underline.Freeze();
        }

        public KnownLayer Layer => KnownLayer.Background;

        public void SetErrorLine(int lineNumber) => _errorLine = lineNumber;
        public void Clear() => _errorLine = -1;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_errorLine <= 0 || textView.Document == null)
                return;

            textView.EnsureVisualLines();

            foreach (var vl in textView.VisualLines)
            {
                // Match any visual line fragment which belongs to the target document line.
                var docLine = vl.FirstDocumentLine;
                if (docLine == null || docLine.LineNumber != _errorLine)
                    continue;

                var y = vl.VisualTop - textView.VerticalOffset;
                var rect = new Rect(0, y, Math.Max(0, textView.ActualWidth), vl.Height);

                // Fill background across full width (works even if the line is empty).
                drawingContext.DrawRectangle(_bg, null, rect);

                // Underline across full width.
                var underlineY = rect.Bottom - 1;
                drawingContext.DrawLine(_underline, new System.Windows.Point(rect.Left, underlineY), new System.Windows.Point(rect.Right, underlineY));
            }
        }
    }
}


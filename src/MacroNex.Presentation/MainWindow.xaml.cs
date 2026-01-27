using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using MacroNex.Presentation.ViewModels;

namespace MacroNex.Presentation;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        DataContext = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));

        // Subscribe to log entries collection changes to auto-scroll
        if (mainViewModel.Logging?.Entries is INotifyCollectionChanged notifyCollection)
        {
            notifyCollection.CollectionChanged += OnLogEntriesCollectionChanged;
        }
    }

    private void OnLogEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Auto-scroll to bottom when new items are added
        if (e.Action == NotifyCollectionChangedAction.Add && LogsListBox.Items.Count > 0)
        {
            // 使用較低的優先級，確保在集合更新完成後再滾動
            // 避免在集合變更過程中訪問 Items，導致同步問題
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (LogsListBox.Items.Count > 0)
                    {
                        var lastItem = LogsListBox.Items[LogsListBox.Items.Count - 1];
                        LogsListBox.ScrollIntoView(lastItem);
                    }
                }
                catch
                {
                    // 忽略滾動錯誤，避免崩潰
                }
            }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }
    }
}
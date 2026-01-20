using System.Windows;
using System.Windows.Threading;

namespace MacroStudio.Presentation.Views;

public partial class CountdownWindow : Window
{
    private readonly DispatcherTimer _timer;
    private DateTime _endsAtUtc;

    public CountdownWindow()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => UpdateCountdown();
    }

    public Task ShowCountdownAsync(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource();
        _endsAtUtc = DateTime.UtcNow.Add(duration);

        Closed += (_, _) => tcs.TrySetResult();

        CountdownText.Text = Math.Ceiling(duration.TotalSeconds).ToString("0");
        Show();
        _timer.Start();

        return tcs.Task;
    }

    private void UpdateCountdown()
    {
        var remaining = _endsAtUtc - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            _timer.Stop();
            Close();
            return;
        }

        CountdownText.Text = Math.Ceiling(remaining.TotalSeconds).ToString("0");
    }
}


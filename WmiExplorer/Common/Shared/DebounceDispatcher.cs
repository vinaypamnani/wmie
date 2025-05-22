using System.Windows.Threading;

namespace WmiExplorer.Common.Shared;

/// <summary>
/// Utility for debouncing actions in WPF ViewModels.
/// </summary>
public class DebounceDispatcher : IDisposable
{
    private Action? _action;
    private readonly DispatcherTimer _timer;

    public DebounceDispatcher(int milliseconds = 150)
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        _timer.Tick += Timer_Tick;
    }

    public void Debounce(Action action)
    {
        _action = action;
        _timer.Stop();
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _timer.Stop();
        _action?.Invoke();
    }

    #region IDisposable

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
    }

    #endregion
}
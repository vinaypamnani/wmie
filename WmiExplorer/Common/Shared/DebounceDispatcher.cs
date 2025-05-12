using System;
using System.Windows.Threading;

namespace WmiExplorer.Common.Shared
{
    /// <summary>
    /// Utility for debouncing actions in WPF ViewModels.
    /// </summary>
    public class DebounceDispatcher : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private Action? _action;

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

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
        }
    }
}

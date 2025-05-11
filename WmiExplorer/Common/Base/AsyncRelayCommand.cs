using System.Windows.Input;

namespace WmiExplorer.Common.Base
{
    /// <summary>
    /// Implementation of ICommand that allows for async command execution
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<bool>? _canExecute;
        private readonly Func<Task> _execute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke() ?? true);
        }

        public async void Execute(object? parameter)
        {
            // The key fix: ignore the parameter since this command doesn't use it
            if (CanExecute(parameter))
            {
                try
                {
                    _isExecuting = true;
                    CommandManager.InvalidateRequerySuggested();
                    await _execute();
                }
                catch (Exception ex)
                {
                    // Log any exception that might occur during execution
                    System.Diagnostics.Debug.WriteLine($"Error executing command: {ex.Message}");
                    throw; // Rethrow to preserve the exception
                }
                finally
                {
                    _isExecuting = false;
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
    }

    /// <summary>
    /// Implementation of ICommand that allows for async command execution with a parameter
    /// </summary>
    public class AsyncRelayCommand<T> : ICommand
    {
        private readonly Func<T, bool>? _canExecute;
        private readonly Func<T, Task> _execute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<T, Task> execute, Func<T, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            if (_isExecuting) return false;

            if (parameter is T t)
                return _canExecute?.Invoke(t) ?? true;
            return true;
        }

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;

            try
            {
                _isExecuting = true;
                CommandManager.InvalidateRequerySuggested();

                if (parameter is T t)
                    await _execute(t);
                else if (default(T) != null)
                    await _execute(default!);
            }
            catch (Exception ex)
            {
                // Log any exception that might occur during execution
                System.Diagnostics.Debug.WriteLine($"Error executing command with parameter: {ex.Message}");
                throw; // Rethrow to preserve the exception
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}
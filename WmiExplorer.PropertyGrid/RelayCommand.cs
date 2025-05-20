using System.Windows.Input;

namespace WmiExplorer.PropertyGrid
{
    /// <summary>
    /// A command implementation that relays its functionality to the provided delegates
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Predicate<object?> _canExecute;
        private readonly Action<object?> _execute;

        /// <summary>
        /// Creates a new relay command that can always execute
        /// </summary>
        /// <param name="execute">The execution logic</param>
        public RelayCommand(Action<object?> execute) : this(execute, null)
        {
        }

        /// <summary>
        /// Creates a new relay command
        /// </summary>
        /// <param name="execute">The execution logic</param>
        /// <param name="canExecute">The execution status logic</param>
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (_ => true);
        }

        /// <summary>
        /// Event raised when the CanExecute state changes
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>
        /// Determines if this command can be executed
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            return _canExecute(parameter);
        }

        /// <summary>
        /// Executes this command
        /// </summary>
        public void Execute(object? parameter)
        {
            _execute(parameter);
        }
    }

    /// <summary>
    /// A generic ICommand implementation that supports command parameters.
    /// </summary>
    /// <typeparam name="T">The command parameter type.</typeparam>
    public class RelayCommand<T> : ICommand
    {
        private readonly Predicate<T?>? _canExecute;
        private readonly Action<T?> _execute;

        /// <summary>
        /// Creates a new command that can always execute.
        /// </summary>
        /// <param name="execute">The execution logic with parameter.</param>
        public RelayCommand(Action<T?> execute) : this(execute, null) { }

        /// <summary>
        /// Creates a new command.
        /// </summary>
        /// <param name="execute">The execution logic with parameter.</param>
        /// <param name="canExecute">The execution status logic with parameter.</param>
        public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Raised when the ability to execute the command changes.
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>
        /// Determines whether this command can execute with the given parameter.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
        /// <returns>True if the command can execute, false otherwise.</returns>
        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter is T t ? t : default);
        }

        /// <summary>
        /// Executes this command with the given parameter.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
        public void Execute(object? parameter)
        {
            _execute(parameter is T t ? t : default);
        }
    }
}
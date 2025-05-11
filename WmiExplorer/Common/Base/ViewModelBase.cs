using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WmiExplorer.Common.Base
{
    /// <summary>
    /// Base class for all ViewModels with INotifyPropertyChanged implementation
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
        }

        /// <summary>
        /// Raises the PropertyChanged event for multiple properties
        /// </summary>
        /// <param name="propertyNames">Names of properties that changed</param>
        protected void OnPropertyChanged(params string[] propertyNames)
        {
            if (propertyNames == null || propertyNames.Length == 0)
                return;

            foreach (var name in propertyNames)
            {
                OnPropertyChanged(name);
            }
        }

        /// <summary>
        /// Sets a property value and raises PropertyChanged if the value changes
        /// </summary>
        /// <typeparam name="T">The property type</typeparam>
        /// <param name="field">The backing field reference</param>
        /// <param name="value">The new value</param>
        /// <param name="propertyName">Name of the property (auto-populated by compiler)</param>
        /// <returns>True if the property was changed, false if the value is the same</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Sets property value, raises PropertyChanged, and executes an action if the value changes
        /// </summary>
        /// <typeparam name="T">The property type</typeparam>
        /// <param name="field">The backing field reference</param>
        /// <param name="value">The new value</param>
        /// <param name="onChanged">Action to execute if the value changes</param>
        /// <param name="propertyName">Name of the property (auto-populated by compiler)</param>
        /// <returns>True if the property was changed, false if the value is the same</returns>
        protected bool SetProperty<T>(ref T field, T value, Action onChanged, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            onChanged?.Invoke();
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
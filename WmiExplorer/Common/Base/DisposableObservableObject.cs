using CommunityToolkit.Mvvm.ComponentModel;

namespace WmiExplorer.Common.Base;

/// <summary>
/// Base class that combines ObservableObject functionality with IDisposable
/// Provides automatic tracking and cleanup of disposable resources
/// </summary>
public abstract partial class DisposableObservableObject : ObservableObject, IDisposable
{
    private readonly List<IDisposable> _disposables = new();

    /// <summary>
    /// Throws an ObjectDisposedException if this object has been disposed
    /// </summary>
    protected void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    /// <summary>
    /// Tracks a disposable resource to be automatically disposed when this object is disposed
    /// </summary>
    /// <typeparam name="T">The type of the disposable resource</typeparam>
    /// <param name="disposable">The disposable resource to track</param>
    /// <returns>The same disposable for fluent API usage</returns>
    protected T TrackDisposable<T>(T disposable) where T : IDisposable
    {
        if (disposable == null)
            throw new ArgumentNullException(nameof(disposable));

        // Don't track if we're already disposed
        if (_isDisposed)
        {
            disposable.Dispose();
            throw new ObjectDisposedException(GetType().Name);
        }

        _disposables.Add(disposable);
        return disposable;
    }

    /// <summary>
    /// Removes a tracked disposable resource without disposing it
    /// </summary>
    /// <typeparam name="T">The type of the disposable resource</typeparam>
    /// <param name="disposable">The disposable resource to untrack</param>
    /// <returns>True if the resource was being tracked and was removed</returns>
    protected bool UntrackDisposable<T>(T disposable) where T : IDisposable
    {
        if (disposable == null)
            return false;

        return _disposables.Remove(disposable);
    }

    #region IDisposable
    private bool _isDisposed;

    /// <summary>
    /// Releases resources
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                // Clean up all tracked disposable resources
                foreach (var disposable in _disposables)
                {
                    disposable?.Dispose();
                }
                _disposables.Clear();
            }

            _isDisposed = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing resources
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
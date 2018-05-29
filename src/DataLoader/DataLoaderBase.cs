using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Provides the base functionality for implementing a data loader.
    /// </summary>
    public abstract class DataLoaderBase<TFetchResult> : IDataLoader
    {
        /// <summary>
        /// To be implemented in derived classes to execute the relevant query/request.
        /// The result is used to complete the <see cref="Completion"/> task.
        /// </summary>
        public abstract Task<TFetchResult> Fetch();

        /// <summary>
        /// Gets the loader's owning context.
        /// </summary>
        public DataLoaderContext Context => _context;
        private DataLoaderContext _context;

        /// <summary>
        /// Represents the result of the next fetch operation.
        /// </summary>
        protected Task<TFetchResult> Completion => _completionSource.Value.Task;
        private Lazy<TaskCompletionSource<TFetchResult>> _completionSource;

        /// <summary>
        /// Creates a new loader.
        /// </summary>
        /// <param name="context">Context the loader will be bound to.</param>
        protected internal DataLoaderBase(DataLoaderContext context)
        {
            _completionSource = CreateNewLazyCompletionSource();
            _context = context;
        }

        /// <summary>
        /// Executes the <see cref="Fetch"/> method implemented by the derived type,
        /// before setting the result on the <see cref="Completion"/> task.
        /// </summary>
        /// <remarks>
        /// Only one loader per context will be fetching at any given time.
        /// As soon as a fetch completes, the next waiting loader will be signaled.
        /// This allows us to keep loading data simultaneously in the background while we process the current items.
        /// </remarks>
        public void Trigger()
        {
            ThrowIfDisposed();

            Fetch().ContinueWith((task, state) =>
            {
                // Fetch more data while we process our results.
                Context.ReleaseFetchLock();

                // Continuations should run synchronously here.
                ((TaskCompletionSource<TFetchResult>)state).SetResult(task.Result);

                // Then flush any loaders that were called from the continuations.
                Context.CommitLoadersOnThread();
            }
            , Interlocked.Exchange(ref _completionSource, CreateNewLazyCompletionSource()).Value
            , CancellationToken.None
            , TaskContinuationOptions.ExecuteSynchronously
            , TaskScheduler.Default);
        }

        /// <summary>
        /// Prepares the loader to execute by queueing it to the context.
        /// </summary>
        private void Prepare() => Context.EnlistLoader(this);

        /// <summary>
        /// Creates a new <see cref="Lazy{TaskCompletionSource{TFetchResult}}"/>.
        /// When the value is instantiated the loader will also schedule itself with the 
        /// <see cref="Context"/> to be executed when appropriate.
        /// </summary>
        private Lazy<TaskCompletionSource<TFetchResult>> CreateNewLazyCompletionSource()
        {
            return new Lazy<TaskCompletionSource<TFetchResult>>(() =>
                {
                    Prepare();
                    return new TaskCompletionSource<TFetchResult>();
                });
        }

#region Disposing

        protected bool IsDisposed { get; private set; }

        protected void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        /// <summary>
        /// Disposes of the loader and cancels any pending or in-progress load operations.
        /// It's usually not necessary to call this explicitly - the context should manage
        /// loader disposal appropriately.
        /// </summary>
        /// <remarks>
        /// <para>Loaders obtained from a context will automatically by disposed when the context is disposed.
        /// Upon disposal, the current <see cref="Completion"/> task will be canceled and any future attempts
        /// to trigger the loader will result in an exception.</para>
        /// <para>Implementations deriving from this type should be careful not to return cached results
        /// after disposal, unless that is the intended behaviour.</para>
        /// </remarks>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                if (_completionSource.IsValueCreated)
                {
                    _completionSource.Value.TrySetCanceled();
                }
            }
        }

#endregion
    }
}
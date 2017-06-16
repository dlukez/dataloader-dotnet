using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    public interface IDataLoader<T> : IDataLoader
    {
        Task<IEnumerable<T>> LoadAsync();
    }

    public class DataLoaderRoot<T> : IDataLoader<T>
    {
        private readonly object _lock = new object();
        private readonly Func<Task<IEnumerable<T>>> _fetchDelegate;
        private readonly DataLoaderContext _boundContext;
        private TaskCompletionSource<IEnumerable<T>> _tcs = new TaskCompletionSource<IEnumerable<T>>();
        private bool _isQueued;
        private bool _isExecuting;

        /// <summary>
        /// Creates a new <see cref="DataLoaderRoot{T}"/>.
        /// </summary>
        public DataLoaderRoot(Func<Task<IEnumerable<T>>> fetchDelegate)
        {
            _fetchDelegate = fetchDelegate;
        }

        /// <summary>
        /// Creates a new <see cref="DataLoaderRoot{T}"/> bound to the specified context.
        /// </summary>
        internal DataLoaderRoot(Func<Task<IEnumerable<T>>> fetch, DataLoaderContext boundContext) : this(fetch)
        {
            _boundContext = boundContext;
        }

        /// <summary>
        /// Gets the context visible to the loader which is either the loader is
        /// bound to if available, otherwise the current ambient context.
        /// </summary>
        /// <seealso cref="DataLoaderContext.Current"/>
        public DataLoaderContext Context => _boundContext ?? DataLoaderContext.Current;

        /// <summary>
        /// Indicates the loader's current status.
        /// </summary>
        public DataLoaderStatus Status =>
            _isExecuting
                ? DataLoaderStatus.Executing
                : _isQueued
                    ? DataLoaderStatus.WaitingToExecute
                    : DataLoaderStatus.Idle;

        /// <summary>
        /// Schedules the loader to run, returning a Task representing the result.
        public Task<IEnumerable<T>> LoadAsync()
        {
            lock (_lock)
            {
                if (!_isQueued) Context?.QueueLoader(this);
                _isQueued = true;
            }

            return _tcs.Task;
        }

        /// <summary>
        /// Executes the fetch delegate and resolves the promise.
        /// </summary>
        public async Task ExecuteAsync()
        {
            _isExecuting = true;
            try
            {
                TaskCompletionSource<IEnumerable<T>> tcs;
                lock (_lock)
                {
                    tcs = Interlocked.Exchange(ref _tcs, new TaskCompletionSource<IEnumerable<T>>());
                    _isQueued = false;
                }
                tcs.SetResult(await _fetchDelegate().ConfigureAwait(false));
            }
            finally { _isExecuting = false; }
        }
    }
}
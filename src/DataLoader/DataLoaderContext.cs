using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Defines a context for <see cref="DataLoader"/> instances.
    /// </summary>
    public sealed class DataLoaderContext : IDisposable
    {
        private readonly TaskCompletionSource<object> _trigger;
        private Task _promiseChain;
        // private CancellationTokenSource _disposalSignal = new CancellationTokenSource();
        // private CancellationToken _cancellationToken;

        private int _nextCacheId = 1;

        /// <summary>
        /// Creates a new <see cref="DataLoaderContext"/>. 
        /// </summary>
        public DataLoaderContext()
        {
            _trigger = new TaskCompletionSource<object>();
            _promiseChain = _trigger.Task;
        }

        /// <summary>
        /// Stores loaders attached to this context.
        /// </summary>
        private readonly ConcurrentDictionary<object, IDataLoader> Cache = new ConcurrentDictionary<object, IDataLoader>();

        /// <summary>
        /// Retrieves a cached loader for the given key, creating one if none is found.
        /// </summary>
        public IDataLoader<TKey, TValue> GetLoader<TKey, TValue>(object key, FetchDelegate<TKey, TValue> fetch)
        {
            CheckDisposed();

            return (IDataLoader<TKey, TValue>) Cache.GetOrAdd(key, _ =>
                new DataLoader<TKey, TValue>(fetch, this));
        }

        /// <summary>
        /// Queues a loader to be executed.
        /// </summary>
        internal void AddPending(IDataLoader loader)
        {
            CheckDisposed();

            if (!Cache.Values.Contains(loader))
                Cache.TryAdd(_nextCacheId++, loader);

            _promiseChain = ContinueWith(loader.ExecuteAsync);
        }

        /// <summary>
        /// Creates a continuation that runs after the last promise in the chain.
        /// </summary>
        private async Task ContinueWith(Func<Task> func)
        {
            await _promiseChain.ConfigureAwait(false);
            // _cancellationToken.ThrowIfCancellationRequested();
            await func();
        }

        /// <summary>
        /// Triggers pending loaders.
        /// </summary>
        public void Start()
        {
            if (_trigger.Task.IsCompleted) throw new InvalidOperationException();
            _trigger.SetResult(null);
        }

        /// <summary>
        /// Triggers pending loaders.
        /// </summary>
        // public void Start(CancellationToken token)
        // {
        //     if (_trigger.Task.IsCompleted) throw new InvalidOperationException();
        //     _cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(_disposalSignal.Token, token).Token;
        //     _trigger.SetResult(null);
        // }

        #region Ambient context

#if NET45

        public static DataLoaderContext Current => null;
        private static void SetCurrentContext(DataLoaderContext context) {}

#else

        private static readonly AsyncLocal<DataLoaderContext> _localContext = new AsyncLocal<DataLoaderContext>();

        /// <summary>
        /// Represents ambient data local to a given load operation started using the static <see cref="Run"/> method.
        /// </summary>
        public static DataLoaderContext Current => _localContext.Value;

        /// <summary>
        /// Sets the <see cref="DataLoaderContext"/> visible from the <see cref="Current"/> Current property.
        /// </summary>
        /// <param name="context"></param>
        private static void SetCurrentContext(DataLoaderContext context)
        {
            _localContext.Value = context;
        }

#endif

        #endregion

        /// <summary>
        /// Runs code within a new loader context before firing any pending
        /// <see cref="DataLoader{TKey,TReturn}"/> requests.
        /// </summary>
        public static async Task<T> Run<T>(Func<DataLoaderContext, Task<T>> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            var prevCtx = DataLoaderContext.Current;
            try
            {
                var loadCtx = new DataLoaderContext();
                SetCurrentContext(loadCtx);

                var task = func(loadCtx);
                if (task == null) throw new InvalidOperationException("No task provided.");

                loadCtx.Start();

                // TODO - Add a unit test verifying this.
                // Need to await here to prevent the context from being reset prematurely.
                return await task.ConfigureAwait(false);
            }
            finally { SetCurrentContext(prevCtx); }
        }

        #region IDisposable

        public bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                // _disposalSignal.Cancel();
            }
        }

        private void CheckDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
        }

        #endregion
    }
}
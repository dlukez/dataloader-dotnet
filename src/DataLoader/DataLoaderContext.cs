using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Defines a context for <see cref="DataLoader"/> instances.
    /// </summary>
    public sealed class DataLoaderContext
    {
        private TaskCompletionSource<object> _trigger;
        private Task _promiseChain;
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
        private readonly ConcurrentDictionary<object, IDataLoader> _cache = new ConcurrentDictionary<object, IDataLoader>();

        /// <summary>
        /// Retrieves a cached loader for the given key, creating one if none is found.
        /// </summary>
        public IDataLoader<TKey, TReturn> GetLoader<TKey, TReturn>(object key, FetchDelegate<TKey, TReturn> fetch)
        {
            return (IDataLoader<TKey, TReturn>) _cache.GetOrAdd(key, _ =>
                new DataLoader<TKey, TReturn>(fetch, this));
        }

        /// <summary>
        /// Queues a loader to be executed.
        /// </summary>
        internal void AddPending(IDataLoader loader)
        {
            if (!_cache.Values.Contains(loader))
                _cache.TryAdd(_nextCacheId++, loader);

            _promiseChain = ContinueWith(loader.ExecuteAsync);
        }

        /// <summary>
        /// Creates a continuation that runs after the last promise in the chain.
        /// </summary>
        private async Task ContinueWith(Func<Task> func)
        {
            await _promiseChain.ConfigureAwait(false);
            await func().ConfigureAwait(false);
        }

        /// <summary>
        /// Starts firing pending loaders asynchronously.
        /// </summary>
        public void Start()
        {
            _trigger.SetResult(null);
        }

        /// <summary>
        /// Indicates whether loaders in this context are being executed.
        /// </summary>
        public bool IsRunning => _trigger.Task.IsCompleted;

        #region Ambient context

#if NET45

        internal static DataLoaderContext Current => null;
        internal static void SetCurrentContext(DataLoaderContext context) {}

#else

        private static readonly AsyncLocal<DataLoaderContext> _localContext = new AsyncLocal<DataLoaderContext>();

        /// <summary>
        /// Represents ambient data local to the current load operation.
        /// <seealso cref="DataLoaderContext.Run{T}"/>
        /// </summary>
        public static DataLoaderContext Current => _localContext.Value;

        /// <summary>
        /// Sets the <see cref="DataLoaderContext"/> visible from the <see cref="DataLoaderContext.Current"/> Current property.
        /// </summary>
        /// <param name="context"></param>
        internal static void SetCurrentContext(DataLoaderContext context)
        {
            _localContext.Value = context;
        }

#endif

#endregion

        /// <summary>
        /// Runs code within a new loader context before firing any pending
        /// <see cref="DataLoader{TKey,TReturn}"/> requests.
        /// </summary>
        public static Task<T> Run<T>(Func<Task<T>> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            using (var scope = new DataLoaderScope())
            {
                var task = func();
                if (task == null) throw new InvalidOperationException("No task provided");
                return task;
            }
        }

        /// <summary>
        /// Runs code within a new loader context before firing any pending
        /// <see cref="DataLoader{TKey,TReturn}"/> requests.
        /// </summary>
        public static async Task<T> Run<T>(Func<DataLoaderContext, Task<T>> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            using (var scope = new DataLoaderScope())
            {
                var task = func(scope.Context);
                if (task == null) throw new InvalidOperationException("No task provided.");
                scope.Context.Start();
                return await task.ConfigureAwait(false);
            }
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Defines a context for creating and executing <see cref="DataLoader{TKey,TReturn}"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class contains any data required by <see cref="DataLoader{TKey,TReturn}"/> instances and is responsible for managing their execution.
    /// </para>
    /// <para>
    /// Loaders enlist themselves with the context active at the time when the <see cref="DataLoader{TKey,TReturn}.LoadAsync"/> method is called.
    /// Later, when the context is triggered (using the <see cref="Process"/> method), the queue will be processed and each loader executed
    /// in the order they were enlisted.
    /// </para>
    /// <para>
    /// The context should wait until each loader has fetched its data and any continuations have run, before moving on to the next loader.
    /// This allows for keys to be collected from continuation code and also fetched by subsequent loaders as batches.
    /// </para>
    /// </remarks>
    public sealed class DataLoaderContext
    {
        private static int _nextId = 1;
        private int _id = _nextId++;
        private readonly ConcurrentDictionary<object, IDataLoader> _cache = new ConcurrentDictionary<object, IDataLoader>();
        private ConcurrentQueue<IDataLoader> _loaderQueue = new ConcurrentQueue<IDataLoader>();
        internal bool _isProcessing;

        internal DataLoaderContext()
        {
        }

        /// <summary>
        /// Retrieves a cached loader for the given key, creating one if none is found.
        /// </summary>
        public IDataLoader<TKey, TReturn> GetOrCreateLoader<TKey, TReturn>(object key, Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetchDelegate)
        {
            return (IDataLoader<TKey, TReturn>)_cache.GetOrAdd(key, _ => new DataLoader<TKey, TReturn>(fetchDelegate, this));
        }

        /// <summary>
        /// Retrieves a cached loader for the given key, creating one if none is found.
        /// </summary>
        public IDataLoader<T> GetOrCreateLoader<T>(object key, Func<Task<IEnumerable<T>>> fetchDelegate)
        {
            return (IDataLoader<T>)_cache.GetOrAdd(key, _ => new DataLoaderRoot<T>(fetchDelegate, this));
        }

        /// <summary>
        /// Queues a loader for later execution.
        /// </summary>
        internal void QueueLoader(IDataLoader loader) => _loaderQueue.Enqueue(loader);

        /// <summary>
        /// Asynchronously executes loaders until there are none remaining.
        /// </summary>
        /// <remarks>
        /// Loaders will fetch exclusively (i.e. one at a time) but complete concurrently. This allows us
        /// to process the results efficiently and avoids hitting the DB with multiple parallel requests,
        /// which usually hurts performance.
        /// </remarks>
        internal async void Process()
        {
            if (_isProcessing) throw new InvalidOperationException();
            if (_loaderQueue.Count == 0) return;
            _isProcessing = true;
            try
            {
                while (_loaderQueue.TryDequeue(out IDataLoader loader))
                {
                    await loader.ExecuteAsync().ConfigureAwait(false);
                }
            }
            finally { _isProcessing = false; }
        }

#if FEATURE_ASYNCLOCAL
        private static readonly AsyncLocal<DataLoaderContext> _localContext = new AsyncLocal<DataLoaderContext>();

        /// <summary>
        /// Represents the ambient context governing the current load operation.
        /// <seealso cref="Run{Task{T}}(Func{Task{T}})"/>
        /// </summary>
        public static DataLoaderContext Current => _localContext.Value;

        /// <summary>
        /// Sets the currently visible ambient loader context.
        /// </summary>
        /// <remarks>
        /// If available, <see cref="DataLoader"/> instances that are not explicitly bound to a context
        /// will register themselves with the ambient context when the load method is called and the
        /// batch is empty.
        /// </remarks>
        internal static void SetLoaderContext(DataLoaderContext context)
        {
            _localContext.Value = context;
        }
#endif

#region Run Methods
#if FEATURE_ASYNCLOCAL
        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static Task<T> Run<T>(Func<Task<T>> func)
        {
            return Run(_ => func());
        }

        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static Task Run(Func<Task> func)
        {
            return Run(_ => func());
        }
#endif

        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static async Task<T> Run<T>(Func<DataLoaderContext, Task<T>> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            var loadCtx = new DataLoaderContext();
            var syncCtx = new DataLoaderSynchronizationContext(loadCtx);
            using (new DataLoaderContextSwitcher(loadCtx))
            using (new SynchronizationContextSwitcher(syncCtx))
            {
                var result = func(loadCtx);
                loadCtx.Process();
                return await result.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static async Task Run(Func<DataLoaderContext, Task> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            var loadCtx = new DataLoaderContext();
            var syncCtx = new DataLoaderSynchronizationContext(loadCtx);
            using (new DataLoaderContextSwitcher(loadCtx))
            using (new SynchronizationContextSwitcher(syncCtx))
            {
                var result = func(loadCtx);
                loadCtx.Process();
                await result.ConfigureAwait(false);
            }
        }
#endregion

#region Ambient context
        /// <summary>
        /// Temporarily switches out the current DataLoaderContext and SynchronizationContext until disposed.
        /// </summary>
        private class DataLoaderContextSwitcher : IDisposable
        {
            private readonly DataLoaderContext _prevLoadCtx;

            public DataLoaderContextSwitcher(DataLoaderContext loadCtx)
            {
                _prevLoadCtx = DataLoaderContext.Current;
                DataLoaderContext.SetLoaderContext(loadCtx);
            }

            public void Dispose()
            {
                DataLoaderContext.SetLoaderContext(_prevLoadCtx);
            }
        }
#endregion
    }
}

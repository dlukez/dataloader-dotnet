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
    /// Later, when the context is completed (using the <see cref="CompleteAsync"/> method), the queue will be processed and each loader executed
    /// in the order they were enlisted.
    /// </para>
    /// <para>
    /// The context should wait until each loader has fetched its data and any continuations have run, before moving on to the next loader.
    /// This allows for keys to be collected from continuation code and also fetched by subsequent loaders as batches.
    /// </para>
    /// </remarks>
    public sealed class DataLoaderContext
    {
        private readonly ConcurrentDictionary<object, IDataLoader> _cache = new ConcurrentDictionary<object, IDataLoader>();
        private ConcurrentQueue<IDataLoader> _loaderQueue = new ConcurrentQueue<IDataLoader>();
        private bool _isCompleting;

        internal DataLoaderContext()
        {
        }

        /// <summary>
        /// Retrieves a cached loader for the given key, creating one if none is found.
        /// </summary>
        public IDataLoader<TKey, TReturn> GetOrCreateLoader<TKey, TReturn>(object key, Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetch)
        {
            return (IDataLoader<TKey, TReturn>)_cache.GetOrAdd(key, _ => new DataLoader<TKey, TReturn>(fetch, this));
        }

        /// <summary>
        /// Queues a loader for later execution.
        /// </summary>
        internal void QueueLoader(IDataLoader loader)
        {
            _loaderQueue.Enqueue(loader);
        }

        /// <summary>
        /// Processes the queue of loaders until there are none remaining.
        /// </summary>
        internal async Task CompleteAsync()
        {
            if (_isCompleting) throw new InvalidOperationException();

            _isCompleting = true;
            try
            {
                var tasks = new List<Task>();
                while (tasks.Count > 0 || _loaderQueue.Count > 0)
                {
                    if (_loaderQueue.Count > 0)
                    {
                        // Process loaders in sets, in an attempt to fetch as early as possible while
                        // allowing enough time for continuations to run and add keys to the batch.
                        // We're essentially trying to find the balance between fetching too often
                        // (more round-trips) and fetching too little (waiting with nothing to do).
                        var queue = Interlocked.Exchange(ref _loaderQueue, new ConcurrentQueue<IDataLoader>());
                        while (queue.TryDequeue(out IDataLoader loader))
                        {
                            // Allow one loader to fetch at a time (awaiters will continue concurrently).
                            tasks.Add(await loader.ExecuteAsync().ConfigureAwait(false));
                        }
                    }

                    // Wait until a loader has finished executing (waiters have completed) before firing more loaders.
                    tasks.Remove(await Task.WhenAny(tasks).ConfigureAwait(false));
                }
            }
            finally { _isCompleting = false; }
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
#else
        internal static DataLoaderContext Current => null;
        internal static void SetLoaderContext(DataLoaderContext context) {}
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

        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static Task Run(Action action)
        {
            return Run(_ => action());
        }
#endif

        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static async Task<T> Run<T>(Func<DataLoaderContext, Task<T>> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            var context = new DataLoaderContext();
            using (new DataLoaderContextSwitcher(context))
            using (new SynchronizationContextSwitcher(null))
            {
                var result = func(context);
                await context.CompleteAsync().ConfigureAwait(false);
                return await result.ConfigureAwait(false);
                // try:
                //context.CompleteAsync();
                //return await result.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static async Task Run(Func<DataLoaderContext, Task> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            var context = new DataLoaderContext();
            using (new DataLoaderContextSwitcher(context))
            using (new SynchronizationContextSwitcher(null))
            {
                var result = func(context);
                await context.CompleteAsync().ConfigureAwait(false);
                await result.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static async Task Run(Action<DataLoaderContext> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var context = new DataLoaderContext();
            using (new DataLoaderContextSwitcher(context))
            using (new SynchronizationContextSwitcher(null))
            {
                action(context);
                await context.CompleteAsync().ConfigureAwait(false);
            }
        }
        #endregion

        #region Context switchers
        /// <summary>
        /// Switches out the data loader context and restores it when disposed.
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

        /// <summary>
        /// Switches out the synchronization context and restores it when disposed.
        /// </summary>
        private class SynchronizationContextSwitcher : IDisposable
        {
            private readonly SynchronizationContext _prevSyncCtx;

            public SynchronizationContextSwitcher(SynchronizationContext syncCtx)
            {
                _prevSyncCtx = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(syncCtx);
            }

            public void Dispose()
            {
                SynchronizationContext.SetSynchronizationContext(_prevSyncCtx);
            }
        }
    }
    #endregion
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace DataLoader
{
    /// <summary>
    /// Defines a context for creating and executing <see cref="DataLoader"/> instances.
    /// </summary>
    /// <remarks>
    /// This class contains any data required by <see cref="DataLoader"/> instances and is responsible for managing their execution.
    ///
    /// Loaders enlist themselves with the context active at the time when their <code>Load</code> method is called.
    /// When the <see cref="CompleteAsync"/> method is called on the context, it begins executing the enlisted loaders.
    /// Loaders are executed serially, since parallel requests to a database are generally not conducive to good performance or throughput.
    ///
    /// The context will try to wait until each loader - as well as continuations attached to each promise it hands out - finish executing
    /// before moving on to the next. The purpose of this is to allow loaders to enlist or reenlist themselves so that they too are processed
    /// as part the context's completion.
    /// </remarks>
    public sealed class DataLoaderContext
    {
        private readonly ConcurrentQueue<IDataLoader> _loaderQueue = new ConcurrentQueue<IDataLoader>();
        private readonly ConcurrentDictionary<object, IDataLoader> _cache = new ConcurrentDictionary<object, IDataLoader>();

        private readonly object _lock = new object();

        private bool _isCompleting;

        internal DataLoaderContext()
        {
        }

        /// <summary>
        /// Retrieves a cached loader for the given key, creating one if none is found.
        /// </summary>
        public IDataLoader<TKey, TReturn> GetOrCreateLoader<TKey, TReturn>(object key, Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetcher)
        {
            return (IDataLoader<TKey, TReturn>)_cache.GetOrAdd(key, _ => new DataLoader<TKey, TReturn>(fetcher, this));
        }

        /// <summary>
        /// Begins processing the waiting loaders, firing them sequentially until there are none remaining.
        /// </summary>
        /// <remarks>
        /// Loaders are fired in the order that they are first called. Once a context has been completed it cannot be reused.
        /// </remarks>
        internal async Task CompleteAsync()
        {
            if (_isCompleting) throw new InvalidOperationException();

            _isCompleting = true;
            try
            {
                while (_loaderQueue.TryDequeue(out IDataLoader loader))
                {
                    await loader.ExecuteAsync().ConfigureAwait(false);
                }
            }
            finally { _isCompleting = false; }
        }

        /// <summary>
        /// Queues a loader for later execution.
        /// </summary>
        internal void QueueLoader(IDataLoader loader)
        {
            _loaderQueue.Enqueue(loader);
        }

#if FEATURE_ASYNCLOCAL
        private static AsyncLocal<DataLoaderContext> _localContext = new AsyncLocal<DataLoaderContext>();

        /// <summary>
        /// Represents ambient data local to the current load operation.
        /// <seealso cref="DataLoaderContext.Run{T}(Func{T}})"/>
        /// </summary>
        public static DataLoaderContext Current => _localContext.Value;

        /// <summary>
        /// Sets the currently visible ambient loader context.
        /// </summary>
        /// <remarks>
        /// If available, <see cref="DataLoader"/> instances that are not explicitly bound to a context
        /// will register themselves with the ambient context when their load method is first called.
        /// </remarks>
        public static void SetLoaderContext(DataLoaderContext context)
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
        /// Switches out the data loader context, restoring the old one when disposed.
        /// </summary>
        private class DataLoaderContextSwitcher : IDisposable
        {
            private DataLoaderContext _prevCtx;

            public DataLoaderContextSwitcher(DataLoaderContext context)
            {
                _prevCtx = DataLoaderContext.Current;
                DataLoaderContext.SetLoaderContext(context);
            }

            public void Dispose()
            {
                DataLoaderContext.SetLoaderContext(_prevCtx);
            }
        }

        /// <summary>
        /// Switches out the synchronization context, restoring the old one when disposed.
        /// </summary>
        private class SynchronizationContextSwitcher : IDisposable
        {
            private SynchronizationContext _prevSyncCtx;

            public SynchronizationContextSwitcher(SynchronizationContext context)
            {
                _prevSyncCtx = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(context);
            }

            public void Dispose()
            {
                SynchronizationContext.SetSynchronizationContext(_prevSyncCtx);
            }
        }
    }
#endregion
}
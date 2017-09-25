using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
    /// Later, when the context is triggered (using the <see cref="Execute"/> method), the queue will be processed and each loader executed
    /// in the order they were enlisted.
    /// </para>
    /// <para>
    /// The context should wait until each loader has fetched its data and any continuations have run, before moving on to the next loader.
    /// This allows for keys to be collected from continuation code and also fetched by subsequent loaders as batches.
    /// </para>
    /// </remarks>
    public sealed class DataLoaderContext : IDisposable
    {
        private readonly ThreadLocal<Queue<IDataLoader>> _localQueue = new ThreadLocal<Queue<IDataLoader>>(() => new Queue<IDataLoader>());
        private readonly ConcurrentDictionary<object, IDataLoader> _loaderCache = new ConcurrentDictionary<object, IDataLoader>();
        private readonly AsyncAutoResetEvent _fetchLock = new AsyncAutoResetEvent(true);
        private readonly TaskFactory _taskFactory;

        /// <summary>
        /// Creates a new instance of a context.
        /// </summary>
        /// <remarks>
        /// Reserved for internal use only - consumers should use the static <see cref="Run"/> method.
        /// </remarks>
        internal DataLoaderContext()
        {
            SyncContext = new DataLoaderSynchronizationContext(this);
            TaskScheduler = new DataLoaderTaskScheduler(this);
            _taskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler);
        }

        /// <summary>
        /// Gets the synchronization context associated with this context.
        /// </summary>
        internal SynchronizationContext SyncContext { get; }

        /// <summary>
        /// Gets the task scheduler associated with this context.
        /// </summary>
        internal TaskScheduler TaskScheduler { get; }

        /// <summary>
        /// Queues a task to be executed.
        /// </summary>
        internal void QueueLoader(IDataLoader loader)
        {
            ThrowIfDisposed();
            _localQueue.Value.Enqueue(loader);
        }

        /// <summary>
        /// Dequeues and executes the next loader.
        /// </summary>
        internal void SignalNext()
        {
            ThrowIfDisposed();
            _fetchLock.Set();
        }

        /// <summary>
        /// Dequeues and executes all the pending loaders on this thread.
        /// </summary>
        internal void FlushLoadersOnThread()
        {
            var localQueue = _localQueue.Value;
            while (localQueue.Count > 0)
            {
                _fetchLock.WaitAsync().ContinueWith(
                    (_, state) => ((IDataLoader)state).Trigger()
                    , localQueue.Dequeue()
                    , CancellationToken.None
                    , TaskContinuationOptions.RunContinuationsAsynchronously
                    , TaskScheduler.Default);
            }
        }

        /// <summary>
        /// Indicates whether this context has been disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Disposes of the context. No further loaders/continuations will be attached.
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                foreach (var loader in _loaderCache.Values)
                {
                    loader.Dispose();
                }
            }
        }

        /// <summary>
        /// Verifies that the context has not been disposed of and throws an exception if so.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        #region Loader Factory

        /// <summary>
        /// Gets or creates a loader instance for the given key.
        /// </summary>
        public IDataLoader<TReturn> GetOrCreateLoader<TReturn>(object key, Func<Task<TReturn>> fetchDelegate)
        {
            return (IDataLoader<TReturn>)_loaderCache.GetOrAdd(key, _ => new RootDataLoader<TReturn>(fetchDelegate, this));
        }

        /// <summary>
        /// Gets or creates a loader instance for the given key.
        /// </summary>
        public IDataLoader<TKey, TReturn> GetOrCreateLoader<TKey, TReturn>(object key, Func<IEnumerable<TKey>, Task<Dictionary<TKey, TReturn>>> fetchDelegate)
        {
            return (IDataLoader<TKey, TReturn>)_loaderCache.GetOrAdd(key, _ => new ObjectDataLoader<TKey, TReturn>(fetchDelegate, this));
        }

        /// <summary>
        /// Gets or creates a loader instance for the given key.
        /// </summary>
        public IDataLoader<TKey, IEnumerable<TReturn>> GetOrCreateLoader<TKey, TReturn>(object key, Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetchDelegate)
        {
            return (IDataLoader<TKey, IEnumerable<TReturn>>)_loaderCache.GetOrAdd(key, _ => new CollectionDataLoader<TKey, TReturn>(fetchDelegate, this));
        }

        #endregion

        #region Ambient Context

        private static readonly AsyncLocal<DataLoaderContext> _localContext = new AsyncLocal<DataLoaderContext>();

        /// <summary>
        /// Represents the ambient context governing the current load operation.
        /// </summary>
        public static DataLoaderContext Current => _localContext.Value;

        /// <summary>
        /// Sets the currently visible ambient loader context.
        /// </summary>
        /// <remarks>
        /// If available, <see cref="DataLoader"/> instances that are not explicitly bound to a context
        /// will register themselves with the ambient context when the load method is called and
        /// the loader is not yet scheduled for execution.
        /// </remarks>
        internal static void SetLoaderContext(DataLoaderContext context) => _localContext.Value = context;

        #endregion

        #region Static `Run` Method

        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static Task<T> Run<T>(Func<Task<T>> func) => Run(_ => func());

        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static Task Run(Func<Task> func) => Run(_ => func());

        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static async Task<T> Run<T>(Func<DataLoaderContext, Task<T>> func)
        {
            if (func == null) throw new ArgumentNullException();

            using (var loadCtx = new DataLoaderContext())
            using (new DataLoaderContextSwitcher(loadCtx))
            {
                return await loadCtx._taskFactory.StartNew(() => func(loadCtx)).Unwrap().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static async Task Run(Func<DataLoaderContext, Task> func)
        {
            if (func == null) throw new ArgumentNullException();

            using (var loadCtx = new DataLoaderContext())
            using (new DataLoaderContextSwitcher(loadCtx))
            {
                await loadCtx._taskFactory.StartNew(() => func(loadCtx)).Unwrap().ConfigureAwait(false);
            }
        }

        #endregion

        /// <summary>
        /// Temporarily switches out the current DataLoaderContext until disposed.
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

    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Defines a context for creating and executing loader instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class contains any data required by loaders and is responsible for managing their execution.
    /// </para>
    /// <para>
    /// Loaders enlist themselves with the context active at the time when their respective load method is called.
    /// The governing context will fire each loader in the order they were enlisted.
    /// </para>
    /// <para>
    /// The context should wait until each loader has fetched its data and any continuations have run before moving on to the next loader.
    /// This allows for keys to be collected from continuation code and also fetched by subsequent loaders as batches.
    /// </para>
    /// </remarks>
    public sealed class DataLoaderContext : IDisposable
    {
        private readonly ThreadLocal<Queue<IDataLoader>> _localQueue = new ThreadLocal<Queue<IDataLoader>>(() => new Queue<IDataLoader>());
        private readonly ConcurrentDictionary<object, IDataLoader> _loaderCache = new ConcurrentDictionary<object, IDataLoader>();
        private readonly AsyncAutoResetEvent _fetchLock = new AsyncAutoResetEvent();
        private readonly TaskFactory _taskFactory;

        /// <remarks>
        /// Use the public static <see cref="o:DataLoaderContext.Run"/> methods - they ensure a context's lifecycle is appropriately managed.
        /// </remarks>
        private DataLoaderContext()
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
        /// Represents the ambient context governing the current load operation.
        /// </summary>
        public static DataLoaderContext Current => (SynchronizationContext.Current as DataLoaderSynchronizationContext)?.UnderlyingContext;

        /// <summary>
        /// Adds a loader to the current thread's local queue.
        /// </summary>
        internal void EnlistLoader(IDataLoader loader)
        {
            ThrowIfDisposed();
            _localQueue.Value.Enqueue(loader);
        }

        /// <summary>
        /// Transitions loaders from the local thread's queue into the ready queue.
        /// </summary>
        internal void CommitLoadersOnThread()
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
        /// Releases the next loader in the ready queue.
        /// </summary>
        internal void ReleaseFetchLock() => _fetchLock.Set();

#region Factory methods

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

#region Disposal

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

#endregion

#region Static `Run` method

        /// <summary>
        /// Runs a user-specified delegate inside a new loader context, which keeps track of loaders called by the delegate 
        /// and defers query execution until after the delegate has returned.
        /// </summary>
        /// <remarks>
        /// Continuations are handled in the same way - loaders called in continuation code will fire after all the
        /// continuations for that particular result have run.
        /// </remarks>
        public static async Task<T> Run<T>(Func<Task<T>> func)
        {
            if (func == null) throw new ArgumentNullException();

            using (var loadCtx = new DataLoaderContext())
            {
                return await loadCtx._taskFactory
                    .StartNew(func)
                    .Unwrap().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs a user-specified delegate inside a new loader context, which keeps track of loaders called by the delegate 
        /// and defers query execution until after the delegate has returned.
        /// </summary>
        /// <remarks>
        /// Continuations are handled in the same way - loaders called in continuation code will fire after all the
        /// continuations for that particular result have run.
        /// </remarks>
        public static async Task Run(Func<Task> func)
        {
            if (func == null) throw new ArgumentNullException();

            using (var loadCtx = new DataLoaderContext())
            {
                await loadCtx._taskFactory
                    .StartNew(func)
                    .Unwrap().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs a user-specified delegate inside a new loader context, which keeps track of loaders called by the delegate 
        /// and defers query execution until after the delegate has returned.
        /// </summary>
        /// <remarks>
        /// Continuations are handled in the same way - loaders called in continuation code will fire after all the
        /// continuations for that particular result have run.
        /// </remarks>
        public static async Task<T> Run<T>(Func<DataLoaderContext, Task<T>> func)
        {
            if (func == null) throw new ArgumentNullException();

            using (var loadCtx = new DataLoaderContext())
            {
                return await loadCtx._taskFactory
                    .StartNew(() => func(loadCtx))
                    .Unwrap().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs a user-specified delegate inside a new loader context, which keeps track of loaders called by the delegate 
        /// and defers query execution until after the delegate has returned.
        /// </summary>
        /// <remarks>
        /// Continuations are handled in the same way - loaders called in continuation code will fire after all the
        /// continuations for that particular result have run.
        /// </remarks>
        public static async Task Run(Func<DataLoaderContext, Task> func)
        {
            if (func == null) throw new ArgumentNullException();

            using (var loadCtx = new DataLoaderContext())
            {
                await loadCtx._taskFactory
                    .StartNew(() => func(loadCtx))
                    .Unwrap().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs a user-specified delegate inside a new loader context, which keeps track of loaders called by the delegate 
        /// and defers query execution until after the delegate has returned.
        /// </summary>
        /// <remarks>
        /// Continuations are handled in the same way - loaders called in continuation code will fire after all the
        /// continuations for that particular result have run.
        /// </remarks>
        public static async Task<T> Run<T>(Func<DataLoaderContext, CancellationToken, Task<T>> func, CancellationToken cancellationToken)
        {
            if (func == null) throw new ArgumentNullException();

            using (var loadCtx = new DataLoaderContext())
            {
                return await loadCtx._taskFactory
                    .StartNew(() => func(loadCtx, cancellationToken), cancellationToken)
                    .Unwrap().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs a user-specified delegate inside a new loader context, which keeps track of loaders called by the delegate 
        /// and defers query execution until after the delegate has returned.
        /// </summary>
        /// <remarks>
        /// Continuations are handled in the same way - loaders called in continuation code will fire after all the
        /// continuations for that particular result have run.
        /// </remarks>
        public static async Task Run(Func<DataLoaderContext, CancellationToken, Task> func, CancellationToken cancellationToken)
        {
            if (func == null) throw new ArgumentNullException();

            using (var loadCtx = new DataLoaderContext())
            {
                await loadCtx._taskFactory
                    .StartNew(() => func(loadCtx, cancellationToken), cancellationToken)
                    .Unwrap().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs a user-specified delegate inside a new loader context, which keeps track of loaders called by the delegate 
        /// and defers query execution until after the delegate has returned.
        /// </summary>
        /// <remarks>
        /// Continuations are handled in the same way - loaders called in continuation code will fire after all the
        /// continuations for that particular result have run.
        /// </remarks>
        public static async Task<T> Run<T>(Func<CancellationToken, Task<T>> func, CancellationToken cancellationToken)
        {
            if (func == null) throw new ArgumentNullException();

            using (var loadCtx = new DataLoaderContext())
            {
                return await loadCtx._taskFactory
                    .StartNew(() => func(cancellationToken), cancellationToken)
                    .Unwrap().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs a user-specified delegate inside a new loader context, which keeps track of loaders called by the delegate 
        /// and defers query execution until after the delegate has returned.
        /// </summary>
        /// <remarks>
        /// Continuations are handled in the same way - loaders called in continuation code will fire after all the
        /// continuations for that particular result have run.
        /// </remarks>
        public static async Task Run(Func<CancellationToken, Task> func, CancellationToken cancellationToken)
        {
            if (func == null) throw new ArgumentNullException();

            using (var loadCtx = new DataLoaderContext())
            {
                await loadCtx._taskFactory
                    .StartNew(() => func(cancellationToken), cancellationToken)
                    .Unwrap().ConfigureAwait(false);
            }
        }

#endregion

#region TaskScheduler and SynchronizationContext

        private class DataLoaderTaskScheduler : TaskScheduler
        {
            private readonly DataLoaderContext _context;

            public DataLoaderTaskScheduler(DataLoaderContext context)
            {
                _context = context;
            }

            protected override IEnumerable<Task> GetScheduledTasks() => throw new NotImplementedException();
            protected override void QueueTask(Task task) => _context.SyncContext.Post(_ => TryExecuteTask(task), null);
            protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => TryExecuteTask(task);
        }

        /// <summary>
        /// Custom synchronization context to work with the async/await infrastructure.
        /// </summary>
        internal class DataLoaderSynchronizationContext : SynchronizationContext
        {
            /// <summary>
            /// Creates a new <see cref="DataLoaderSynchronizationContext"/>.
            /// </summary>
            internal DataLoaderSynchronizationContext(DataLoaderContext context)
            {
                UnderlyingContext = context;
            }

            /// <summary>
            /// Gets the underlying <see cref="DataLoaderContext"/>.
            /// </summary>
            public DataLoaderContext UnderlyingContext { get; }

            /// <summary>
            /// Synchronously invokes the given callback before preparing to fire any new loaders.
            /// </summary>
            /// <remarks>
            /// <para>This method is called by the async/await infrastructure.</para>
            /// <para>Usually this method would run asynchronously, however we call it synchronously instead
            /// because we want to let all the continuation code finish before firing additional loaders.</para>
            /// <para>The callback will execute with this as the current synchronization context.</para>
            /// </remarks>
            public override void Post(SendOrPostCallback d, object state)
            {
                var prevCtx = SynchronizationContext.Current;
                var wasCurrentContext = prevCtx == this;
                if (!wasCurrentContext) SynchronizationContext.SetSynchronizationContext(this);
                try
                {
                    d(state);
                    UnderlyingContext.CommitLoadersOnThread();
                }
                finally
                {
                    if (!wasCurrentContext) SynchronizationContext.SetSynchronizationContext(prevCtx);
                }
            }

            /// <see cref="Post"/>
            public override void Send(SendOrPostCallback d, object state) => Post(d, state);
        }

#endregion
    }
}

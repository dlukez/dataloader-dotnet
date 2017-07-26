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
    /// Defines a context for creating and executing <see cref="BatchDataLoader{TKey,TReturn}"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>This class contains any data required by <see cref="BatchDataLoader{TKey,TReturn}"/> instances and is responsible for managing their execution.</para>
    /// <para>Loaders enlist themselves with the context active at the time when the <see cref="BatchDataLoader{TKey,TReturn}.LoadAsync"/> method is called.
    /// Later, when the context is triggered (using the <see cref="Execute"/> method), the queue will be processed and each loader executed
    /// in the order they were enlisted.</para>
    /// <para>The context should wait until each loader has fetched its data and any continuations have run, before moving on to the next loader.
    /// This allows for keys to be collected from continuation code and also fetched by subsequent loaders as batches.</para>
    /// </remarks>
    public sealed class DataLoaderContext : IDisposable
    {
        private static int _lastId = 0;
        private readonly int _id = ++_lastId;
        public int Id => _id;

        private readonly object _lock = new object();

        private readonly DataLoaderTaskScheduler _taskScheduler;
        private readonly TaskFactory _taskFactory;

        private readonly DataLoaderFactory _loaderFactory;
        private readonly ConcurrentQueue<IDataLoader> _loaderQueue;

        /// <summary>
        /// Creates a new instance of a context.
        /// </summary>
        /// <remarks>
        /// Reserved for internal use only - public consumers should use the static <see cref="Run"/> method.
        /// </remarks>
        internal DataLoaderContext()
        {
            _loaderQueue = new ConcurrentQueue<IDataLoader>();
            _loaderFactory = new DataLoaderFactory(this);

            _taskScheduler = new DataLoaderTaskScheduler(this);
            _taskFactory = new TaskFactory(_taskScheduler);
        }

        /// <summary>
        /// Provides methods for obtaining loader instances in this context.
        /// </summary>
        public DataLoaderFactory Factory => _loaderFactory;

        /// <summary>
        /// Exposes a task scheduler intended for scheduling data loader completion tasks.
        /// </summary>
        public TaskScheduler TaskScheduler => _taskScheduler;

        /// <summary>
        /// Schedules a loader to be executed.
        /// </summary>
        internal void Enqueue(IDataLoader loader)
        {
            ThrowIfDisposed();
            _taskFactory.StartNew(loader.FetchAndCompleteAsync);
        }

        /// <summary>
        /// Schedules an action to be executed.
        /// </summary>
        internal void Enqueue(Action action)
        {
            ThrowIfDisposed();
            _taskFactory.StartNew(action);
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
            ThrowIfDisposed();
            IsDisposed = true;
        }

        /// <summary>
        /// Throws an exception if the context is disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - !! Context disposed !!");
                throw new ObjectDisposedException(GetType().Name);
            }
        }

#region Ambient Context

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
        public static void Run(Action action) => Run(_ => action());

        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static async Task<T> Run<T>(Func<DataLoaderContext, Task<T>> func)
        {
            if (func == null) throw new ArgumentNullException();

            using (var loadCtx = new DataLoaderContext())
            using (new DataLoaderContextSwitcher(loadCtx))
            using (new SynchronizationContextSwitcher(new DataLoaderSynchronizationContext(loadCtx)))
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
            using (new SynchronizationContextSwitcher(new DataLoaderSynchronizationContext(loadCtx)))
            {
                await loadCtx._taskFactory.StartNew(() => func(loadCtx)).Unwrap().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static void Run(Action<DataLoaderContext> action)
        {
            if (action == null) throw new ArgumentNullException();

            using (var loadCtx = new DataLoaderContext())
            using (new DataLoaderContextSwitcher(loadCtx))
            using (new SynchronizationContextSwitcher(new DataLoaderSynchronizationContext(loadCtx)))
            {
                loadCtx._taskFactory.StartNew(() => action(loadCtx)).Wait();
            }
        }

#endregion

    }

    /// <summary>
    /// Temporarily switches out the current DataLoaderContext until disposed.
    /// </summary>
    internal class DataLoaderContextSwitcher : IDisposable
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

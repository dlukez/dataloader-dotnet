using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        public int Id { get; } = ++_lastId;

        private readonly DataLoaderFactory _loaderFactory;
        private readonly DataLoaderTaskScheduler _taskScheduler;
        private readonly ConcurrentExclusiveSchedulerPair _schedulerPair;
        private readonly TaskFactory _taskFactory;

        internal readonly AsyncAutoResetEvent _autoResetEvent;
        internal ConcurrentQueue<IDataLoader> _loaderQueue;

        /// <summary>
        /// Creates a new instance of a context.
        /// </summary>
        /// <remarks>
        /// Reserved for internal use only - public consumers should use the static <see cref="Run"/> method.
        /// </remarks>
        private DataLoaderContext()
        {
            _autoResetEvent = new AsyncAutoResetEvent(true);

            _loaderQueue = new ConcurrentQueue<IDataLoader>();
            _loaderFactory = new DataLoaderFactory(this);

            _schedulerPair = new ConcurrentExclusiveSchedulerPair();
            
            _taskScheduler = new DataLoaderTaskScheduler(this);
            _taskFactory = new TaskFactory(_taskScheduler);
        }

        /// <summary>
        /// Provides methods for obtaining loader instances in this context.
        /// </summary>
        public DataLoaderFactory Factory => _loaderFactory;

        /// <summary>
        /// Gets a scheduler that should be used for completing load operations.
        /// </summary>
        public TaskScheduler Scheduler => _schedulerPair.ExclusiveScheduler;

        /// <summary>
        /// Returns a Task that is completed when the next loader is due to execute.
        /// </summary>
        internal void EnqueueLoader(IDataLoader loader)
        {
            _loaderQueue.Enqueue(loader);

            _autoResetEvent.WaitAsync().ContinueWith(
                TriggerLoader,
                CancellationToken.None,
                TaskContinuationOptions.RunContinuationsAsynchronously,
                Scheduler);
        
            void TriggerLoader(Task task)
            {
                if (_loaderQueue.TryDequeue(out var next)) next.ExecuteAsync();
                else Debug.Fail("Queue should never be empty at this point.");
            }
        }

        /// <summary>
        /// Signals to the context that a loader has finished fetching, the next waiting loader if one is available.
        /// </summary>
        internal void TriggerNext()
        {
            _autoResetEvent.Set();
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
            _taskScheduler.Dispose();
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
        public static Task<T> Run<T>(Func<DataLoaderContext, Task<T>> func)
        {
            if (func == null) throw new ArgumentNullException();

            var loadCtx = new DataLoaderContext();
            using (new DataLoaderContextSwitcher(loadCtx))
            {
                var task = loadCtx._taskFactory.StartNew(() => func(loadCtx)).Unwrap();

                // task.ContinueWith(delegate { loadCtx._taskScheduler.Complete(); });

                return task;
            }
        }

        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static Task Run(Func<DataLoaderContext, Task> func)
        {
            if (func == null) throw new ArgumentNullException();

            var loadCtx = new DataLoaderContext();
            using (new DataLoaderContextSwitcher(loadCtx))
            {
                var task = loadCtx._taskFactory.StartNew(() => func(loadCtx)).Unwrap();

                // task.ContinueWith(delegate { loadCtx._taskScheduler.Complete(); });

                return task;
            }
        }

        /// <summary>
        /// Runs code within a new loader context before firing any pending loaders.
        /// </summary>
        public static void Run(Action<DataLoaderContext> action)
        {
            if (action == null) throw new ArgumentNullException();

            var loadCtx = new DataLoaderContext();
            using (new DataLoaderContextSwitcher(loadCtx))
            {
                action(loadCtx);
                // loadCtx._taskScheduler.ProcessUntilEmpty();
                // loadCtx._taskScheduler.Complete();
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

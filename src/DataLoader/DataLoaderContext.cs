using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Defines a context for <see cref="DataLoader"/> instances.
    /// </summary>
    /// <remarks>
    /// This class contains any data required by <see cref="DataLoader"/> instances and is responsible for managing their execution.
    ///e
    /// Loaders enlist themselves with the context active at the time when a <code>Load</code> method is called on a loader instance.
    /// When the <see cref="DataLoaderContext.Complete">Complete</see> method is called on the context, it begins executing these waiting loaders.
    /// Loaders are executed serially, since  parallel requests to a database are generally not conducive to good performance or throughput.
    ///
    /// The context will try to wait until each loader - as well as continuations attached to each promise it hands out - finish executing
    /// before moving on to the next. The purpose of this is to allow loaders to enlist or reenlist themselves so that they too are processed
    /// as part the context's completion.
    /// </remarks>
    public sealed class DataLoaderContext
    {
        private readonly Queue<IDataLoader> _queue = new Queue<IDataLoader>();
        private readonly ConcurrentDictionary<object, IDataLoader> _cache = new ConcurrentDictionary<object, IDataLoader>();
        private TaskCompletionSource<object> _completionSource = new TaskCompletionSource<object>();

        internal DataLoaderContext()
        {
        }

        /// <summary>
        /// Retrieves a cached loader for the given key, creating one if none is found.
        /// </summary>
        public IDataLoader<TKey, TReturn> GetLoader<TKey, TReturn>(object key, FetchDelegate<TKey, TReturn> fetch)
        {
            return (IDataLoader<TKey, TReturn>)_cache.GetOrAdd(key, _ =>
               new DataLoader<TKey, TReturn>(fetch, this));
        }

        /// <summary>
        /// Indicates whether loaders have been fired and are in progress.
        /// </summary>
        public bool IsCompleting { get; private set; }

        /// <summary>
        /// Represents whether this context has been completed.
        /// </summary>
        public Task Completion => _completionSource.Task;

        /// <summary>
        /// Begins processing the waiting loaders, firing them sequentially until there are none remaining.
        /// </summary>
        /// <remarks>
        /// Loaders are fired in the order that they are first called. Once completed the context cannot be reused.
        /// </remarks>
        public async void Complete()
        {
            if (IsCompleting) throw new InvalidOperationException();
            IsCompleting = true;

            try
            {
                while (_queue.Count > 0)
                {
                    await _queue.Dequeue().ExecuteAsync().ConfigureAwait(false);
                }

                _completionSource.SetResult(null);
            }
            catch (OperationCanceledException)
            {
                _completionSource.SetCanceled();
            }
            catch (Exception e)
            {
                _completionSource.SetException(e);
            }

            IsCompleting = false;
        }

        /// <summary>
        /// Queues a loader to be executed.
        /// </summary>
        internal void AddToQueue(IDataLoader loader)
        {
            _queue.Enqueue(loader);
        }

#if NET45

        internal static DataLoaderContext Current => null;
        internal static void SetCurrentContext(DataLoaderContext context) {}

#else

        private static readonly AsyncLocal<DataLoaderContext> LocalContext = new AsyncLocal<DataLoaderContext>();

        /// <summary>
        /// Represents ambient data local to the current load operation.
        /// <seealso cref="DataLoaderContext.Run{T}(Func{Task{T}})"/>
        /// </summary>
        public static DataLoaderContext Current => LocalContext.Value;

        /// <summary>
        /// Sets the <see cref="DataLoaderContext"/> visible from the <see cref="DataLoaderContext.Current"/>  property.
        /// </summary>
        /// <param name="context"></param>
        internal static void SetCurrentContext(DataLoaderContext context)
        {
            LocalContext.Value = context;
        }

#endif

#if !NET45

        /// <summary>
        /// Runs code within a new loader context before firing any pending
        /// <see cref="DataLoader{TKey,TReturn}">DataLoader</see> instances.
        /// </summary>
        public static Task<T> Run<T>(Func<Task<T>> func)
        {
            return Run(_ => func());
        }

        /// <summary>
        /// Runs code within a new loader context before firing any pending
        /// <see cref="DataLoader{TKey,TReturn}">DataLoader</see> instances.
        /// </summary>
        public static Task Run(Func<Task> func)
        {
            return Run(_ => func());
        }

#endif

        /// <summary>
        /// Runs code within a new loader context before firing any pending
        /// <see cref="DataLoader{TKey,TReturn}">DataLoader</see> instances.
        /// </summary>
        public static Task<T> Run<T>(Func<DataLoaderContext, Task<T>> func)
        {
            return Task.Run<T>(() =>
            {
                using (var scope = new DataLoaderScope())
                {
                    var task = func(scope.Context);
                    if (task == null) throw new InvalidOperationException("No task provided.");
                    return task;
                }
            });
        }

        /// <summary>
        /// Runs code within a new loader context before firing any pending
        /// <see cref="DataLoader{TKey,TReturn}">DataLoader</see> instances.
        /// </summary>
        public static Task Run(Func<DataLoaderContext, Task> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            // TODO
            //
            // For some reason, using `Task.Run` causes <see cref="TaskCompletionSource{T}"/> to run continuations
            // synchronously, which prevents the main loop from continuing on to the next loader before they're done.
            //
            // I presume this is because once we're inside the ThreadPool, continuations will be scheduled using the
            // local queues (in LIFO order) instead of the global queue (which executes in FIFO order). This is really
            // a hack I think - the same thing should be accomplished using a custom TaskScheduler or custom awaiter.
            //
            return Task.Run(() =>
            {
                using (var scope = new DataLoaderScope())
                {
                    var task = func(scope.Context);
                    if (task == null) throw new InvalidOperationException("No task provided.");
                    return task;
                }
            });
        }
    }
}
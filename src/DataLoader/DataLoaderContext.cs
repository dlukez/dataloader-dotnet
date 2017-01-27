using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Defines a context for <see cref="DataLoader"/> instances.
    /// </summary>
    public sealed class DataLoaderContext
    {
        private readonly Queue<IDataLoader> _queue = new Queue<IDataLoader>();
        private readonly TaskCompletionSource<object> _completionSource = new TaskCompletionSource<object>();
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
        /// Indicates whether loaders have been started.
        /// </summary>
        public bool IsLoading { get; private set; }

        /// <summary>
        /// Represents whether this context has finished executing loaders.
        /// </summary>
        public Task Completion => _completionSource.Task;

        /// <summary>
        /// Starts firing pending loaders.
        /// </summary>
        public async Task ExecuteAsync()
        {
            if (IsLoading) throw new InvalidOperationException();
            var sw = Stopwatch.StartNew();
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Context executing");
            IsLoading = true;
            while (_queue.Count > 0) await _queue.Dequeue().ExecuteAsync().ConfigureAwait(false);
            IsLoading = false;
            _completionSource.SetResult(null);
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Context finished ({sw.ElapsedMilliseconds}ms)");
            sw.Stop();
        }

        /// <summary>
        /// Queues a loader to be executed.
        /// </summary>
        internal void AddToQueue(IDataLoader loader)
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Queueing loader");
            _queue.Enqueue(loader);
        }

//        internal void AddToQueue(IDataLoader loader)
//        {
//            OnNext(loader.ExecuteAsync);
//            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Adding loader to queue");
//            _signal.WaitAsync().ContinueWith(delegate
//            {
//                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Signal fired");
//                loader.ExecuteAsync().ContinueWith(delegate
//                {
//                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Firing signal");
//                    _signal.Set();
//                }, TaskContinuationOptions.ExecuteSynchronously);
//            }, TaskContinuationOptions.ExecuteSynchronously);
//        }

        /// <summary>
        /// Perform some action when the signal fires next.
        /// </summary>
//        internal async void OnNext(Func<Task> func)
//        {
//            await _signal.WaitAsync().ConfigureAwait(false);
//            await func().ConfigureAwait(false);
//            _signal.Set();
//        }
//
//        internal void OnNext(Action<Action> next)
//        {
//            _signal.WaitAsync().ContinueWith(_ => next(() => _signal.Set()));
//        }

        #region Ambient context

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

#endregion

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
        /// <see cref="DataLoader{TKey,TReturn}"/> requests.
        /// </summary>
        public static Task<T> Run<T>(Func<DataLoaderContext, Task<T>> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            return Task.Run(async () =>
            {
                using (var scope = new DataLoaderScope())
                {
                    var task = func(scope.Context);
                    if (task == null) throw new InvalidOperationException("No task provided.");
                    await scope.Context.ExecuteAsync().ConfigureAwait(false);
                    return await task;
                }
            });
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Represents a pending data load operation.
    /// </summary>
    public interface IDataLoader
    {
        Task ExecuteAsync();
    }

    /// <summary>
    /// Wraps an arbitrary query and integrates it into the loading chain.
    /// </summary>
    public interface IDataLoader<T> : IDataLoader
    {
        Task<T> LoadAsync();
    }


    /// <summary>
    /// Collects and loads keys in batches.
    /// </summary>
    public interface IDataLoader<TKey, TReturn> : IDataLoader
    {
        Task<IEnumerable<TReturn>> LoadAsync(TKey key);
    }

    /// <summary>
    /// Wraps an arbitrary query and integrates it into the loading chain.
    /// </summary>
    public class DataLoader<T> : IDataLoader<T>
    {
        private readonly DataLoaderContext _boundContext;
        private readonly Func<Task<T>> _fetchDelegate;
        private TaskCompletionSource<T> _tcs;

        /// <summary>
        /// Creates a new <see cref="DataLoader{T}"/>.
        /// </summary>
        public DataLoader(Func<Task<T>> fetchDelegate) : this(fetchDelegate, null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="DataLoader{T}"/> bound to the specified context.
        /// </summary>
        internal DataLoader(Func<Task<T>> fetchDelegate, DataLoaderContext boundContext)
        {
            _fetchDelegate = fetchDelegate;
            _boundContext = boundContext;
        }

        /// <summary>
        /// Gets the context visible to the loader which is either the loader is
        /// bound to if available, otherwise the current ambient context.
        /// </summary>
        /// <seealso cref="DataLoaderContext.Current"/>
        public DataLoaderContext Context => _boundContext ?? DataLoaderContext.Current;

        // /// <summary>
        // /// Gets or sets whether the loader will execute immediately or be added to the queue
        // /// when the <see cref="LoadAsync"/> method is called.
        // /// </summary>
        // /// <remarks>
        // /// If true, the corresponding Task will still be added to the load queue to guarantee
        // /// that subsequent loaders are executed in their proper order.
        // /// </remarks>
        // public bool ExecuteImmediately { get; set; } = true;

        /// <summary>
        /// Loads data using the configured fetch delegate.
        /// </summary>
        public Task<T> LoadAsync()
        {
            if (_tcs == null && Interlocked.CompareExchange(ref _tcs, new TaskCompletionSource<T>(), null) == null)
                Context?.Enqueue(this);

            return _tcs.Task;
        }

        /// <summary>
        /// Executes the fetch delegate and resolves the promise.
        /// </summary>
        public Task ExecuteAsync()
        {
            var tcs = Interlocked.Exchange(ref _tcs, new TaskCompletionSource<T>());
            return _fetchDelegate().ContinueWith(
                task => tcs.SetResult(task.Result),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                Context._taskScheduler);
        }

    }

    /// <summary>
    /// Collects and loads keys in batches.
    /// </summary>
    /// <remarks>
    /// When a call is made to a load method, each key is stored and a task is handed back that represents the future result.
    /// The request is deferred until the loader is invoked, which can occur in the following circumstances:
    /// <list type="bullet">
    /// <item>The delegate supplied to <see cref="o:DataLoaderContext.Run"/> returned.</item>
    /// <item><see cref="DataLoaderContext.Process"/> was explicitly called on the governing <see cref="DataLoaderContext"/>.</item>
    /// <item>The loader was invoked explicitly by calling <see cref="ExecuteAsync"/>.</item>
    /// </list>
    /// </remarks>
    public class DataLoader<TKey, TReturn> : IDataLoader<TKey, TReturn>
    {
        private readonly DataLoaderContext _boundContext;
        private readonly ConcurrentDictionary<TKey, Task<IEnumerable<TReturn>>> _cache = new ConcurrentDictionary<TKey, Task<IEnumerable<TReturn>>>();
        private readonly Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> _fetchDelegate;
        private List<TKey> _batch;
        private TaskCompletionSource<ILookup<TKey, TReturn>> _tcs = new TaskCompletionSource<ILookup<TKey, TReturn>>();

        /// <summary>
        /// Creates a new <see cref="DataLoader{TKey,TReturn}"/>.
        /// </summary>
        public DataLoader(Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetchDelegate) : this(fetchDelegate, null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="DataLoader{TKey,TReturn}"/> bound to the specified context.
        /// </summary>
        internal DataLoader(Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetchDelegate, DataLoaderContext context)
        {
            _boundContext = context;
            _fetchDelegate = fetchDelegate;
        }

        /// <summary>
        /// Gets the context the loader is bound to, otherwise the current ambient context.
        /// </summary>
        /// <seealso cref="DataLoaderContext.Current"/>
        public DataLoaderContext Context => _boundContext ?? DataLoaderContext.Current;

        /// <summary>
        /// Loads some data corresponding to the given key.
        /// </summary>
        /// <remarks>
        /// Each requested key is collected into a batch so that they can be fetched in a single call.
        /// When data for a key is loaded, it will be cached and used to fulfil any subsequent requests for the same key.
        /// </remarks>
        public Task<IEnumerable<TReturn>> LoadAsync(TKey key)
        {
            if (_cache.TryGetValue(key, out var task)) return task;

            if (_batch == null && Interlocked.CompareExchange(ref _batch, new List<TKey>(), null) == null)
                Context?.Enqueue(this);

            _batch.Add(key);
            return (_cache[key] = _tcs.Task.ContinueWith(
                t => t.Result[key],
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                Context._taskScheduler));
        }

        /// <summary>
        /// Fetches the current batch and resolves previously handed out promises.
        /// </summary>
        public Task ExecuteAsync()
        {
            var batch = Interlocked.Exchange(ref _batch, null);
            var tcs = Interlocked.Exchange(ref _tcs, new TaskCompletionSource<ILookup<TKey, TReturn>>());
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Fetching batch of {batch.Count} items");
            return _fetchDelegate(batch).ContinueWith(task =>
            {
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Completing {batch.Count} items");
                tcs.SetResult(task.Result);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, Context._taskScheduler);
        }
    }
}

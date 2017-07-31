using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Represents a pending data load operation.
    /// </summary>
    public interface IDataLoader
    {
        void Execute();
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
        Task<TReturn> LoadAsync(TKey key);
    }

    /// <summary>
    /// Wraps an arbitrary query and integrates it into the loading chain.
    /// </summary>
    public class BasicDataLoader<T> : IDataLoader<T> where T : class
    {
        private readonly object _lock = new object();
        private readonly DataLoaderContext _boundContext;
        private readonly Func<Task<T>> _fetchDelegate;
        private TaskCompletionSource<T> _source;

        /// <summary>
        /// Creates a new <see cref="BasicDataLoader{T}"/>.
        /// </summary>
        public BasicDataLoader(Func<Task<T>> fetchDelegate) : this(fetchDelegate, null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="BasicDataLoader{T}"/> bound to the specified context.
        /// </summary>
        internal BasicDataLoader(Func<Task<T>> fetchDelegate, DataLoaderContext boundContext)
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

        /// <summary>
        /// Loads data using the configured fetch delegate.
        /// </summary>
        public Task<T> LoadAsync()
        {
            Task<T> task = _source?.Task;

            lock (_lock)
            {
                if (task == null)
                {
                    _source = new TaskCompletionSource<T>();
                    task = _source.Task;
                    Context.EnqueueLoader(this);
                }
            }

            return task;
        }

        /// <summary>
        /// Executes the fetch delegate and resolves the promise.
        /// </summary>
        public async void Execute()
        {
            TaskCompletionSource<T> tcs;

            lock (_lock)
            {
                tcs = _source;
                if (tcs == null) return;
                _source = new TaskCompletionSource<T>();
            }

            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Fetching basic query");
            var result = await _fetchDelegate();

            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Completing basic query");
            tcs.SetResult(result);

            //  _fetchDelegate().ContinueWith(task =>
            // {
            //     Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Completing basic query");
            //     tcs.SetResult(task.Result);
            // }, CancellationToken.None, TaskContinuationOptions.None, Context.Scheduler);
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
    /// <item>The loader was invoked explicitly by calling <see cref="FetchAndCompleteAsync"/>.</item>
    /// </list>
    /// </remarks>
    public class BatchDataLoader<TKey, TReturn> : IDataLoader<TKey, TReturn> where TReturn : class
    {
        private readonly object _lock = new object();
        private readonly DataLoaderContext _boundContext;
        private readonly Func<IEnumerable<TKey>, Task<Dictionary<TKey, TReturn>>> _fetchDelegate;
        private readonly ConcurrentDictionary<TKey, Task<TReturn>> _cache = new ConcurrentDictionary<TKey, Task<TReturn>>();
        // private volatile Dictionary<TKey, TaskCompletionSource<TReturn>> _tcs;
        private volatile Tuple<TaskCompletionSource<Dictionary<TKey, TReturn>>, HashSet<TKey>> _batch;

        /// <summary>
        /// Creates a new <see cref="BatchDataLoader{TKey,TReturn}"/>.
        /// </summary>
        public BatchDataLoader(Func<IEnumerable<TKey>, Task<Dictionary<TKey, TReturn>>> fetchDelegate) : this(fetchDelegate, null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="BatchDataLoader{TKey,TReturn}"/> bound to the specified context.
        /// </summary>
        internal BatchDataLoader(Func<IEnumerable<TKey>, Task<Dictionary<TKey, TReturn>>> fetchDelegate, DataLoaderContext context)
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
        public Task<TReturn> LoadAsync(TKey key)
        {
            return _cache.GetOrAdd(key, async _ =>
            {
                var batch = _batch;

                lock (_lock)
                {
                    if (batch == null)
                    {
                        batch = _batch = Tuple.Create(new TaskCompletionSource<Dictionary<TKey, TReturn>>(), new HashSet<TKey>());
                        Context.EnqueueLoader(this);
                    }

                    batch.Item2.Add(key);
                }

                var dict = await batch.Item1.Task;
                return dict.ContainsKey(key) ? dict[key] : null;
            });
        }

        /// <summary>
        /// Fetches the current batch and resolves previously handed out promises.
        /// </summary>
        public async void Execute()
        {
            Tuple<TaskCompletionSource<Dictionary<TKey, TReturn>>, HashSet<TKey>> batch;

            lock (_lock)
            {
                batch = _batch;
                if (batch == null) return;
                _batch = null;
            }

            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Fetching batch ({batch.Item2.Count} keys)");
            var result = await _fetchDelegate(batch.Item2);

            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Completing batch ({batch.Item2.Count} keys)");
            batch.Item1.SetResult(result);

            // _fetchDelegate(batch.Item2).ContinueWith(task =>
            // {
            //     Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(3, ' ')} - Completing batch ({batch.Item2.Count} keys)");
            //     batch.Item1.SetResult(task.Result);
            // }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, Context.Scheduler);
        }
    }
}

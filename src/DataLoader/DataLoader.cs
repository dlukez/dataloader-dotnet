using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    public enum DataLoaderStatus
    {
        Idle,
        WaitingToExecute,
        Executing
    }

    public interface IDataLoader<in TKey, TValue, TReturn>
    {
        DataLoaderStatus Status { get; }
        Task<TReturn> LoadAsync(TKey key);
    }

    public interface IDataLoader<in TKey, TValue> : IDataLoader<TKey, TValue, IEnumerable<TValue>>
    {
    }

    public interface IDataLoader
    {
        Task<Task> ExecuteAsync();
    }
    
    /// <summary>
    /// Collects keys into a batch to load in one request.
    /// </summary>
    /// <remarks>
    /// When a call is made to a load method, each key is stored and a
    /// promise task is handed back that represents the future result of the deferred request. The request
    /// is deferred (and keys are collected) until the loader is invoked, which can occur in the following circumstances:
    /// <list type="bullet">
    /// <item>The delegate supplied to <see cref="o:DataLoaderContext.Run"/> returned (but hasn't necessarily completed).</item>
    /// <item><see cref="DataLoaderContext.CompleteAsync"/> was explicitly called on the governing <see cref="DataLoaderContext"/>.</item>
    /// <item>The loader was invoked explicitly by calling <see cref="ExecuteAsync"/>.</item>
    /// </list>
    /// </remarks>
    public class DataLoader<TKey, TValue, TReturn> : IDataLoader<TKey, TValue, TReturn>, IDataLoader
    {
        private readonly object _lock = new object();
        private readonly Dictionary<TKey, Task<TReturn>> _cache = new Dictionary<TKey, Task<TReturn>>();
        private readonly Func<IEnumerable<TKey>, Task<ILookup<TKey, TValue>>> _fetch;
        private readonly Func<IEnumerable<TValue>, TReturn> _transform;
        private readonly DataLoaderContext _boundContext;
        private List<(TKey, TaskCompletionSource<TReturn>)> _batch = new List<(TKey, TaskCompletionSource<TReturn>)>(); //= new List<(TKey, TaskCompletionSource<TReturn>)>();
        private bool _isExecuting;

        /// <summary>
        /// Creates a new <see cref="DataLoader{TKey,TValue,TReturn}"/>.
        /// </summary>
        public DataLoader(Func<IEnumerable<TKey>, Task<ILookup<TKey, TValue>>> fetch, Func<IEnumerable<TValue>, TReturn> transform)
        {
            _fetch = fetch;
            _transform = transform;
        }

        /// <summary>
        /// Creates a new <see cref="DataLoader{TKey,TValue,TReturn}"/> bound to the specified context.
        /// </summary>
        internal DataLoader(Func<IEnumerable<TKey>, Task<ILookup<TKey, TValue>>> fetch, Func<IEnumerable<TValue>, TReturn> transform, DataLoaderContext context) : this(fetch, transform)
        {
            _boundContext = context;
        }

#if FEATURE_ASYNCLOCAL
        /// <summary>
        /// Gets the context the loader is bound to, otherwise the current ambient context.
        /// </summary>
        /// <seealso cref="DataLoaderContext.Current"/>
        public DataLoaderContext Context => _boundContext ?? DataLoaderContext.Current;
#else
        /// <summary>
        /// Gets the context the loader is bound to.
        /// </summary>
        public DataLoaderContext Context => _boundContext;
#endif

        /// <summary>
        /// Gets the keys to retrieve in the next batch.
        /// </summary>
        public IEnumerable<TKey> Keys => GetKeys(_batch);

        /// <summary>
        /// Indicates the loader's current status.
        /// </summary>
        public DataLoaderStatus Status =>
            _isExecuting
                ? DataLoaderStatus.Executing
                : _batch.Count > 0
                    ? DataLoaderStatus.WaitingToExecute
                    : DataLoaderStatus.Idle;

        /// <summary>
        /// Loads an item.
        /// </summary>
        public Task<TReturn> LoadAsync(TKey key)
        {
            if (!_cache.TryGetValue(key, out var task))
            {
                var tcs = new TaskCompletionSource<TReturn>();
                task = tcs.Task;
                _cache.Add(key, task);

                lock (_lock)
                {
                    if (_batch.Count == 0) Context?.QueueLoader(this);
                    _batch.Add((key, tcs));
                }
            }
            return task;
        }

        /// <summary>
        /// Fetches the current batch and resolves previously handed out promises.
        /// </summary>
        public async Task<Task> ExecuteAsync()
        {
            _isExecuting = true;
            try
            {
                List<(TKey, TaskCompletionSource<TReturn>)> thisBatch;
                lock (_lock) thisBatch = Interlocked.Exchange(ref _batch, new List<(TKey, TaskCompletionSource<TReturn>)>());
                var lookup = await _fetch(GetKeys(thisBatch)).ConfigureAwait(false);
                return Task.Run(() =>
                {
                    foreach (var (key, tcs) in thisBatch)
                    {
                        tcs.SetResult(_transform(lookup[key]));
                    }
                });
            }
            finally { _isExecuting = false; }
        }

        /// <summary>
        /// Gets the keys in a batch.
        /// </summary>
        private static IEnumerable<TKey> GetKeys(IEnumerable<(TKey, TaskCompletionSource<TReturn>)> batch)
        {
            return batch.Select(item => item.Item1).Distinct().ToList();
        }
    }

    /// <summary>
    /// Collects keys into a batch to load in one request.
    /// </summary>
    /// <remarks>
    /// When a call is made to a load method, each key is stored and a
    /// promise task is handed back that represents the future result of the deferred request. The request
    /// is deferred (and keys are collected) until the loader is invoked, which can occur in the following circumstances:
    /// <list type="bullet">
    /// <item>The delegate supplied to <see cref="o:DataLoaderContext.Run"/> returned (but hasn't necessarily completed).</item>
    /// <item><see cref="DataLoaderContext.CompleteAsync"/> was explicitly called on the governing <see cref="DataLoaderContext"/>.</item>
    /// <item>The loader was invoked explicitly by calling <see cref="ExecuteAsync"/>.</item>
    /// </list>
    /// </remarks>
    public class DataLoader<TKey, TValue> : DataLoader<TKey, TValue, IEnumerable<TValue>>, IDataLoader<TKey, TValue>
    {
        /// <summary>
        /// Creates a new <see cref="DataLoader{TKey,TValue}"/>.
        /// </summary>
        public DataLoader(Func<IEnumerable<TKey>, Task<ILookup<TKey, TValue>>> fetch) : base(fetch, r => r)
        {
        }

        /// <summary>
        /// Creates a new <see cref="DataLoader{TKey,TValue}"/> bound to the specified context.
        /// </summary>
        internal DataLoader(Func<IEnumerable<TKey>, Task<ILookup<TKey, TValue>>> fetch, DataLoaderContext context) : base(fetch, r => r, context)
        {
        }
    }
}

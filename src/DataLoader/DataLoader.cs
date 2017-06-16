using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    public interface IDataLoader<in TKey, TReturn> : IDataLoader
    {
        Task<IEnumerable<TReturn>> LoadAsync(TKey key);
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
    /// <item><see cref="DataLoaderContext.Process"/> was explicitly called on the governing <see cref="DataLoaderContext"/>.</item>
    /// <item>The loader was invoked explicitly by calling <see cref="ExecuteAsync"/>.</item>
    /// </list>
    /// </remarks>
    public class DataLoader<TKey, TReturn> : IDataLoader<TKey, TReturn>
    {
        private readonly object _lock = new object();
        private readonly Dictionary<TKey, Task<IEnumerable<TReturn>>> _cache = new Dictionary<TKey, Task<IEnumerable<TReturn>>>();
        private readonly Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> _fetchDelegate;
        private readonly DataLoaderContext _boundContext;
        private List<(TKey, TaskCompletionSource<IEnumerable<TReturn>>)> _batch = new List<(TKey, TaskCompletionSource<IEnumerable<TReturn>>)>();
        private bool _isExecuting;

        /// <summary>
        /// Creates a new <see cref="DataLoader{TKey,TReturn}"/>.
        /// </summary>
        public DataLoader(Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetchDelegate)
        {
            _fetchDelegate = fetchDelegate;
        }

        /// <summary>
        /// Creates a new <see cref="DataLoader{TKey,TReturn}"/> bound to the specified context.
        /// </summary>
        internal DataLoader(Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetchDelegate, DataLoaderContext context) : this(fetchDelegate)
        {
            _boundContext = context;
        }

        /// <summary>
        /// Gets the context the loader is bound to, otherwise the current ambient context.
        /// </summary>
        /// <seealso cref="DataLoaderContext.Current"/>
        public DataLoaderContext Context => _boundContext ?? DataLoaderContext.Current;

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
        public Task<IEnumerable<TReturn>> LoadAsync(TKey key)
        {
            if (!_cache.TryGetValue(key, out var task))
            {
                var tcs = new TaskCompletionSource<IEnumerable<TReturn>>();
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
        public async Task ExecuteAsync()
        {
            _isExecuting = true;
            try
            {
                List<(TKey, TaskCompletionSource<IEnumerable<TReturn>>)> thisBatch;
                lock (_lock) thisBatch = Interlocked.Exchange(ref _batch, new List<(TKey, TaskCompletionSource<IEnumerable<TReturn>>)>());
                var lookup = await _fetchDelegate(GetKeys(thisBatch)).ConfigureAwait(false);
                foreach (var (key, tcs) in thisBatch)
                {
                    tcs.SetResult(lookup[key]);
                }
            }
            finally { _isExecuting = false; }
        }

        /// <summary>
        /// Gets the keys in a batch.
        /// </summary>
        private static IEnumerable<TKey> GetKeys(IEnumerable<(TKey, TaskCompletionSource<IEnumerable<TReturn>>)> batch)
        {
            return batch.Select(item => item.Item1).Distinct().ToList();
        }
    }
}

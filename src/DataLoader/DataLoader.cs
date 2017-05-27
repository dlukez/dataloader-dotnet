using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Collects keys into a batch to load in one request.
    /// </summary>
    /// <remarks>
    /// When a call is made to a load method, each key is stored and a
    /// promise task is handed back that represents the future result of the deferred request. The request
    /// is deferred (and keys are collected) until the loader is invoked, which can occur in the following circumstances:
    /// <list type="bullet">
    /// <item>The delegate supplied to <see cref="DataLoaderContext.Run"/> returned.</item>
    /// <item><see cref="DataLoaderContext.CompleteAsync"/> was explicitly called on the governing <see cref="DataLoaderContext"/>.</item>
    /// <item>The loader was invoked explicitly by calling <see cref="ExecuteAsync"/>.</item>
    /// </list>
    /// </remarks>
    public class DataLoader<TKey, TReturn> : IDataLoader<TKey, TReturn>
    {
        private readonly object _lock = new object();

        private readonly Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> _fetch;
        private readonly DataLoaderContext _boundContext;

        private bool _isExecuting;

        private List<(TKey, TaskCompletionSource<IEnumerable<TReturn>>)> _batch = new List<(TKey, TaskCompletionSource<IEnumerable<TReturn>>)>(); //= new List<(TKey, TaskCompletionSource<IEnumerable<TReturn>>)>();

        /// <summary>
        /// Creates a new <see cref="DataLoader{TKey,TReturn}"/>.
        /// </summary>
        public DataLoader(Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetch)
        {
            _fetch = fetch;
        }

        /// <summary>
        /// Creates a new <see cref="DataLoader{TKey,TReturn}"/> bound to the specified context.
        /// </summary>
        internal DataLoader(Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetch, DataLoaderContext context) : this(fetch)
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
        public Task<IEnumerable<TReturn>> LoadAsync(TKey key)
        {
            if (_batch.Count == 0) Context?.QueueLoader(this);

            var tcs = new TaskCompletionSource<IEnumerable<TReturn>>();
            _batch.Add((key, tcs));
            return tcs.Task;
        }

        /// <summary>
        /// Fetches the current batch and resolves previously handed out promises.
        /// </summary>
        public async Task ExecuteAsync()
        {
            _isExecuting = true;
            try
            {
                var thisBatch = Interlocked.Exchange(ref _batch, new List<(TKey, TaskCompletionSource<IEnumerable<TReturn>>)>());
                var lookup = await _fetch(GetKeys(thisBatch)).ConfigureAwait(false);
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
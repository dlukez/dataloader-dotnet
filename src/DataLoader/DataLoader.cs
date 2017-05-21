using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace DataLoader
{
    /// <summary>
    /// Collects keys into a batch to load in one request.
    /// </summary>
    /// <remarks>
    /// When a call is made to one of the <see cref="LoadAsync"/> methods, each key is stored and a
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
        private Queue<FetchCompletionPair> _queue = new Queue<FetchCompletionPair>();
        private DataLoaderContext _boundContext;
        private bool _isExecuting;

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
            SetContext(context);
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
        public IEnumerable<TKey> Keys => GetKeys(_queue);

        /// <summary>
        /// Indicates the loader's current status.
        /// </summary>
        public DataLoaderStatus Status
        {
            get
            {
                return _isExecuting
                    ? DataLoaderStatus.Executing
                    : _queue.Count > 0
                        ? DataLoaderStatus.WaitingToExecute
                        : DataLoaderStatus.Idle;
            }
        }

        /// <summary>
        /// Binds an instance to a particular loading context.
        /// </summary>
        internal void SetContext(DataLoaderContext context)
        {
            lock (_lock)
                if (_queue.Count > 0)
                    throw new InvalidOperationException("Cannot set context while a loader is awaiting execution or executing");

            _boundContext = context;
        }

        /// <summary>
        /// Loads an item.
        /// </summary>
        public Task<IEnumerable<TReturn>> LoadAsync(TKey key)
        {
            var fetchResult = new FetchCompletionPair(key);
            lock (_lock)
            {
                if (_queue.Count == 0) Context?.AddToQueue(this);
                _queue.Enqueue(fetchResult);
            }
            return fetchResult.CompletionSource.Task;
        }

        /// <summary>
        /// Triggers the fetch callback and fulfils any promises.
        /// </summary>
        public async Task ExecuteAsync()
        {
            _isExecuting = true;
            try
            {
                Queue<FetchCompletionPair> toFetch;
                lock (_lock)
                {
                    toFetch = _queue;
                    _queue = new Queue<FetchCompletionPair>();
                }

                var lookup = await _fetch(GetKeys(toFetch)).ConfigureAwait(false);
                while (toFetch.Count > 0)
                {
                    var item = toFetch.Dequeue();
                    item.CompletionSource.SetResult(lookup[item.Key]);
                    Debug.Assert(item.CompletionSource.Task.IsCompleted);
                }
            }
            finally { _isExecuting = false; }
        }

        private static IEnumerable<TKey> GetKeys(IEnumerable<FetchCompletionPair> pairs)
        {
            return pairs.Select(p => p.Key).Distinct().ToList();
        }

        /// <summary>
        /// A <see cref="TaskCompletionSource{T}"/> paired with a given key.
        /// </summary>
        private class FetchCompletionPair
        {
            public TKey Key { get; }
            public TaskCompletionSource<IEnumerable<TReturn>> CompletionSource { get; }

            public FetchCompletionPair(TKey key)
            {
                Key = key;
                CompletionSource = new TaskCompletionSource<IEnumerable<TReturn>>();
            }
        }
    }
}
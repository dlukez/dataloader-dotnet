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
    /// When a call is made to one of the <see cref="LoadAsync"/> methods, each key is stored and a
    /// promise task is handed back that represents the future result of the deferred request. The request
    /// is deferred (and keys are collected) until the loader is invoked, which can occur in the following circumstances:
    /// <list type="bullet">
    /// <item>The delegate supplied to <see cref="DataLoaderContext.Run{T}"/> returned.</item>
    /// <item><see cref="DataLoaderContext.ExecuteAsync">StartLoading</see> was explicitly called on the governing <see cref="DataLoaderContext"/>.</item>
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

#if NETSTANDARD1_1

        /// <summary>
        /// Gets the context the loader is bound to.
        /// </summary>
        public DataLoaderContext Context => _boundContext;

#else

        /// <summary>
        /// Gets the context the loader is bound to, otherwise the current ambient context.
        /// </summary>
        /// <seealso cref="DataLoaderContext.Current"/>
        public DataLoaderContext Context => _boundContext ?? DataLoaderContext.Current;

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
                return _isExecuting ? DataLoaderStatus.Executing :
                    _queue.Count > 0
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
                    throw new InvalidOperationException("Cannot set context while a load is pending or executing");

            _boundContext = context;
        }

        /// <summary>
        /// Loads an item.
        /// </summary>
        public Task<IEnumerable<TReturn>> LoadAsync(TKey key)
        {
            var fetchResult = new FetchCompletionPair(key);
            bool shouldSchedule;

            lock (_queue)
            {
                shouldSchedule = _queue.Count == 0;
                _queue.Enqueue(fetchResult);
            }

            if (shouldSchedule) Context?.AddToQueue(this);
            return fetchResult.CompletionSource.Task;
        }

        /// <summary>
        /// Loads many items.
        /// </summary>
        public async Task<IDictionary<TKey, IEnumerable<TReturn>>> LoadAsync(params TKey[] keys)
        {
            var tasks = keys.Select(async key =>
                new KeyValuePair<TKey, IEnumerable<TReturn>>(
                    key, await LoadAsync(key).ConfigureAwait(false)));

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Triggers the fetch callback and fulfils any promises.
        /// </summary>
        public async Task ExecuteAsync()
        {
            _isExecuting = true;
            try
            {
                var queue = Interlocked.Exchange(ref _queue, new Queue<FetchCompletionPair>());
                var lookup = await _fetch(GetKeys(queue)).ConfigureAwait(false);
                while (queue.Count > 0)
                {
                    var item = queue.Dequeue();
                    item.CompletionSource.SetResult(lookup[item.Key]);
                    var task = item.CompletionSource.Task;
                    if (!task.IsCompleted)
                    {
                        Console.WriteLine($"Not completed: task {task.Id} (thread {Thread.CurrentThread.ManagedThreadId})");
                        task.Wait();
                    }
                }
            }
            finally { _isExecuting = false; }
        }

        private static IEnumerable<TKey> GetKeys(IEnumerable<FetchCompletionPair> pairs)
        {
            return pairs.Select(p => p.Key).Distinct().ToList();
        }

        /// <summary>
        /// Creates a new <see cref="TaskCompletionSource{T}"/> paired with the given key.
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
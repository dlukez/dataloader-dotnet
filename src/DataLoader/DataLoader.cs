using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Collects keys into a batch to fetch in one request.
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
        private readonly FetchDelegate<TKey, TReturn> _fetch;
        private Queue<FetchCompletionPair> _queue = new Queue<FetchCompletionPair>();
        private DataLoaderContext _boundContext;

        /// <summary>
        /// Creates a new <see cref="DataLoader{TKey,TReturn}"/>.
        /// </summary>
        public DataLoader(FetchDelegate<TKey, TReturn> fetch)
        {
            _fetch = fetch;
        }

        /// <summary>
        /// Creates a new <see cref="DataLoader{TKey,TReturn}"/> bound to the specified context.
        /// </summary>
        public DataLoader(FetchDelegate<TKey, TReturn> fetch, DataLoaderContext context) : this(fetch)
        {
            SetContext(context);
        }

        /// <summary>
        /// Gets the bound context if set, otherwise the current ambient context.
        /// </summary>
        public DataLoaderContext Context => _boundContext ?? DataLoaderContext.Current;

        /// <summary>
        /// Gets the keys to retrieve in the next batch.
        /// </summary>
        public IEnumerable<TKey> Keys => GetKeys(_queue);

        /// <summary>
        /// Indicates the loader's current status.
        /// </summary>
        public DataLoaderStatus Status { get; private set; }

        /// <summary>
        /// Binds an instance to a particular loading context.
        /// </summary>
        public void SetContext(DataLoaderContext context)
        {
            if (Status != DataLoaderStatus.Idle)
                throw new InvalidOperationException("Cannot set context - loader must be not be queued or executing");

            _boundContext = context;
        }

        /// <summary>
        /// Loads an item.
        /// </summary>
        public Task<IEnumerable<TReturn>> LoadAsync(TKey key)
        {
            lock (_queue)
            {
                if (_queue.Count == 0) ScheduleToRun();
                var fetchResult = new FetchCompletionPair(key);
                _queue.Enqueue(fetchResult);
                return fetchResult.CompletionSource.Task;
            }
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
            Status = DataLoaderStatus.Executing;
            Queue<FetchCompletionPair> queue;
            lock (_queue) queue = Interlocked.Exchange(ref _queue, new Queue<FetchCompletionPair>());
            var lookup = await _fetch(GetKeys(queue)).ConfigureAwait(false);
            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                item.CompletionSource.SetResult(lookup[item.Key]);
            }
            Status = DataLoaderStatus.Idle;
        }

        private void ScheduleToRun()
        {
            Status = DataLoaderStatus.WaitingToExecute;
            Context?.AddToQueue(this);
        }

        private static IEnumerable<TKey> GetKeys(IEnumerable<FetchCompletionPair> pairs)
        {
            return pairs.Select(p => p.Key).Distinct().ToList();
        }

        /// <summary>
        /// Provides a new <see cref="TaskCompletionSource{T}"/> paired with the given key.
        /// </summary>
        private struct FetchCompletionPair
        {
            public readonly TKey Key;
            public readonly TaskCompletionSource<IEnumerable<TReturn>> CompletionSource;
            public FetchCompletionPair(TKey key)
            {
                Key = key;
                CompletionSource = new TaskCompletionSource<IEnumerable<TReturn>>();
            }
        }
    }
}
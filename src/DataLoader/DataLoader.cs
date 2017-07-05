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
    /// Provides functionality for loading data.
    /// </summary>
    public interface IDataLoader<in TKey, TReturn> : IDataLoader
    {
        Task<IEnumerable<TReturn>> LoadAsync(TKey key);
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
        private static readonly Task<Task> s_completedWrappedTask = Task.FromResult(Task.CompletedTask);
        private readonly object _lock = new object();
        private readonly ConcurrentDictionary<TKey, Task<IEnumerable<TReturn>>> _cache = new ConcurrentDictionary<TKey, Task<IEnumerable<TReturn>>>();
        private readonly DataLoaderContext _boundContext;
        private List<TKey> _batch = new List<TKey>();
        private TaskCompletionSource<ILookup<TKey, TReturn>> _completionSource = new TaskCompletionSource<ILookup<TKey, TReturn>>();
        private Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> _fetchDelegate;

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
            return _cache[key] = defer();

            async Task<IEnumerable<TReturn>> defer()
            {
                lock (_lock)
                {
                    if (_batch.Count == 0)
                        Context?.SetNext(ExecuteAsync);

                    _batch.Add(key);
                }

                var lookup = await _completionSource.Task.ConfigureAwait(false);
                return lookup[key];
            }
        }

        /// <summary>
        /// Fetches the current batch and resolves previously handed out promises.
        /// </summary>
        public async Task<Task> ExecuteAsync()
        {
            List<TKey> batch;
            TaskCompletionSource<ILookup<TKey, TReturn>> tcs;
            lock (_lock)
            {
                batch = Interlocked.Exchange(ref _batch, new List<TKey>());
                tcs = Interlocked.Exchange(ref _completionSource, new TaskCompletionSource<ILookup<TKey, TReturn>>());
            }

            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Fetching batch of {batch.Count} items");
            var lookup = await _fetchDelegate(batch).ConfigureAwait(false);
            return Task.Run(() => tcs.SetResult(lookup));
        }
    }
}

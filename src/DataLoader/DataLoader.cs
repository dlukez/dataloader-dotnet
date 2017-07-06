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
    public interface IDataLoader<TKey, TReturn> : IDataLoader
    {
        DataLoaderResult<TKey, TReturn> LoadAsync(TKey key);
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

        private List<DataLoaderResult<TKey, TReturn>> _batch;
        private readonly DataLoaderContext _boundContext;
        private readonly ConcurrentDictionary<TKey, DataLoaderResult<TKey, TReturn>> _cache = new ConcurrentDictionary<TKey, DataLoaderResult<TKey, TReturn>>();
        // private TaskCompletionSource<ILookup<TKey, TReturn>> _completionSource = new TaskCompletionSource<ILookup<TKey, TReturn>>();
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
        public DataLoaderResult<TKey, TReturn> LoadAsync(TKey key)
        {
            if (_cache.TryGetValue(key, out var task)) return task;

            var result = new DataLoaderResult<TKey, TReturn>(key);
            _cache[key] = result;

            lock (_lock)
            {
                if (_batch == null)
                {
                    _batch = new List<DataLoaderResult<TKey, TReturn>>();
                    Context?.SetNext(ExecuteAsync);
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Queued loader");
                }
            }

            _batch.Add(result);
            return result;
        }

        /// <summary>
        /// Fetches the current batch and resolves previously handed out promises.
        /// </summary>
        public async Task<Task> ExecuteAsync()
        {
            List<DataLoaderResult<TKey, TReturn>> batch;
            lock (_lock) batch = Interlocked.Exchange(ref _batch, null);

            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Fetching batch of {batch.Count} items");
            var lookup = await _fetchDelegate(batch.Select(x => x.Key)).ConfigureAwait(false);
            return Task.Run(() =>
            {
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(2, ' ')} - Completing {batch.Count} items");;
                foreach (var result in batch)
                    result.Complete(lookup);
            });
        }
    }
}

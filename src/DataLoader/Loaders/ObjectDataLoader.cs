using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Batches multiple loads into a single request, where each key is expected to return only one value.
    /// </summary>
    internal sealed class ObjectDataLoader<TKey, TReturn> : DataLoaderBase<Dictionary<TKey, TReturn>>, IDataLoader<TKey, TReturn>
    {
        private readonly object _lock = new object();
        private readonly Dictionary<TKey, Task<TReturn>> _cache = new Dictionary<TKey, Task<TReturn>>();
        private readonly Func<IEnumerable<TKey>, Task<Dictionary<TKey, TReturn>>> _fetchDelegate;
        private HashSet<TKey> _batch = new HashSet<TKey>();

        /// <summary>
        /// Creates a new <see cref="ObjectDataLoader{TKey,TReturn}"/>.
        /// </summary>
        public ObjectDataLoader(Func<IEnumerable<TKey>, Task<Dictionary<TKey, TReturn>>> fetchDelegate) : this(fetchDelegate, null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="ObjectDataLoader{TKey,TReturn}"/> bound to a specific context.
        /// </summary>
        internal ObjectDataLoader(Func<IEnumerable<TKey>, Task<Dictionary<TKey, TReturn>>> fetchDelegate, DataLoaderContext context) : base(context)
        {
            _fetchDelegate = fetchDelegate;
        }

        /// <summary>
        /// Loads some data corresponding to the given key.
        /// </summary>
        /// <remarks>
        /// Each requested key is collected into a batch so that they can be fetched in a single call.
        /// When data for a key is loaded, it will be cached and used to fulfil any subsequent requests for the same key.
        /// </remarks>
        public Task<TReturn> LoadAsync(TKey key)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var task)) return task;
                _batch.Add(key);
                return (_cache[key] = Completion.ContinueWith(
                    SelectKeyFromTaskResult
                    , key
                    , CancellationToken.None
                    , TaskContinuationOptions.None
                    , TaskScheduler.Default));
            }
        }

        static TReturn SelectKeyFromTaskResult(Task<Dictionary<TKey, TReturn>> task, object state)
        {
            return task.Result.TryGetValue((TKey)state, out var value) ? value : default(TReturn);
        }

        /// <summary>
        /// Invokes the user-specified fetch delegate configured in the constructor,
        /// passing it the current set of keys to be loaded.
        /// </summary>
        public override Task<Dictionary<TKey, TReturn>> Fetch()
        {
            HashSet<TKey> currentBatch;
            lock (_lock) currentBatch = Interlocked.Exchange(ref _batch, new HashSet<TKey>());
            return _fetchDelegate(currentBatch);
        }
    }
}
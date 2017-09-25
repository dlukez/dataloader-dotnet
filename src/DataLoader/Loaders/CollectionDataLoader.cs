using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace DataLoader
{
    /// <summary>
    /// Batches multiple loads into a single request, where each is expected to return multiple values for the given.
    /// </summary>
    internal sealed class CollectionDataLoader<TKey, TReturn> : DataLoaderBase<ILookup<TKey, TReturn>>, IDataLoader<TKey, IEnumerable<TReturn>>
    {
        private readonly object _lock = new object();
        private readonly Dictionary<TKey, Task<IEnumerable<TReturn>>> _cache = new Dictionary<TKey, Task<IEnumerable<TReturn>>>();
        private readonly Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> _fetchDelegate;
        private HashSet<TKey> _batch = new HashSet<TKey>();

        /// <summary>
        /// Creates a new <see cref="CollectionDataLoader{TKey,TReturn}"/>.
        /// </summary>
        public CollectionDataLoader(Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetchDelegate) : this(fetchDelegate, null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="CollectionDataLoader{TKey,TReturn}"/> bound to a specific context.
        /// </summary>
        internal CollectionDataLoader(Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetchDelegate, DataLoaderContext context) : base(context)
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
        /// <returns>The future result matching the given key.</returns>
        public Task<IEnumerable<TReturn>> LoadAsync(TKey key)
        {
            ThrowIfDisposed();
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var task)) return task;
                _batch.Add(key);
                return (_cache[key] = Completion.ContinueWith(
                    (t, state) => t.Result[(TKey)state]
                    , key
                    , CancellationToken.None
                    , TaskContinuationOptions.None
                    , TaskScheduler.Default));
            }
        }

        /// <summary>
        /// Invokes the user-specified fetch delegate specified in the constructor.
        /// </summary>
        /// <returns>The result of the fetch delegate.</returns>
        public override Task<ILookup<TKey, TReturn>> Fetch()
        {
            HashSet<TKey> currentBatch;
            lock (_lock) currentBatch = Interlocked.Exchange(ref _batch, new HashSet<TKey>());
            return _fetchDelegate(currentBatch);
        }
    }
}
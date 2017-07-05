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
    /// Batches multiple loads for individual items into a single request.
    /// </summary>
    public sealed class BatchDataLoader<TKey, TReturn> : DataLoaderBase<ILookup<TKey, TReturn>>, IDataLoader<TKey, TReturn>
    {
        private readonly Dictionary<TKey, Task<IEnumerable<TReturn>>> _cache = new Dictionary<TKey, Task<IEnumerable<TReturn>>>();
        private readonly Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> _fetchDelegate;
        private HashSet<TKey> _batch = new HashSet<TKey>();

        /// <summary>
        /// Creates a new <see cref="BatchDataLoader{TKey,TReturn}"/>.
        /// </summary>
        public BatchDataLoader(Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetchDelegate) : this(fetchDelegate, null)
        {
        }

        /// <summary>
        /// Creates a new <see cref="BatchDataLoader{TKey,TReturn}"/> bound to a specific context.
        /// </summary>
        internal BatchDataLoader(Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetchDelegate, DataLoaderContext context) : base(context)
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
        public Task<IEnumerable<TReturn>> LoadAsync(TKey key)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(key, out var task)) return task;
                _batch.Add(key);
                return (_cache[key] = Completion.ContinueWith(SelectResultItemForKey, key));
            }
        }

        /// <summary>
        /// Retrieves a value from the task's result, for the given key (passed via the state parameter).
        /// </summary>
        static IEnumerable<TReturn> SelectResultItemForKey(Task<ILookup<TKey, TReturn>> task, object state)
        {
            return task.Result[(TKey)state];
        }

        /// <summary>
        /// Invokes the user-specified fetch delegate configured in the constructor,
        /// passing it the current set of keys to be loaded.
        /// </summary>
        public override Task<ILookup<TKey, TReturn>> Fetch()
        {
            var batch = Interlocked.Exchange(ref _batch, new HashSet<TKey>());
            Logger.WriteLine($"Fetching batch of {typeof(TReturn).Name} ({batch.Count} keys)");
            return _fetchDelegate(batch);
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Collects keys into a batch to be executed in a single query.
    /// </summary>
    public class BatchLoader<TKey, TValue> : IDataLoader<TKey, TValue>, IDataLoader
    {
        private readonly Func<IEnumerable<TKey>, ILookup<TKey, TValue>> _fetchFunc;
        private HashSet<TKey> _keys = new HashSet<TKey>();
        private Task<ILookup<TKey, TValue>> _future;

        /// <summary>
        /// The context this loader is attached to.
        /// </summary>
        public DataLoaderContext Context { get; private set; }

        /// <summary>
        /// The keys to be sent in the next batch.
        /// </summary>
        public IEnumerable<TKey> Keys => _keys.Select(key => key);

        /// <summary>
        /// Initializes a new <see cref="DataLoader"/> attached to the specified context.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="fetchFunc"></param>
        public BatchLoader(DataLoaderContext context, Func<IEnumerable<TKey>, ILookup<TKey, TValue>> fetchFunc)
        {
            _fetchFunc = fetchFunc;
            SetContext(context);
        }

        /// <summary>
        /// Initializes a new <see cref="DataLoader"/> using the default ambient context.
        /// </summary>
        /// <param name="fetchFunc"></param>
        public BatchLoader(Func<IEnumerable<TKey>, ILookup<TKey, TValue>> fetchFunc) : this(DataLoaderContext.Current, fetchFunc)
        {
        }

        /// <summary>
        /// Attaches the DataLoader to a new context.
        /// </summary>
        /// <param name="context"></param>
        public void SetContext(DataLoaderContext context)
        {
            if (context == null)
                throw new InvalidOperationException($"No DataLoaderContext is set");

            Context = context;
        }

        /// <summary>
        /// Calls the configured fetch function, passing it the batch of keys.
        /// </summary>
        private ILookup<TKey, TValue> Fetch() => _fetchFunc(Interlocked.Exchange(ref _keys, new HashSet<TKey>()));

        /// <summary>
        /// Retrieves a <typeparamref name="TValue"/> for the given <typeparamref name="TKey"/>
        /// </summary>
        /// <param name="key"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<IEnumerable<TValue>> LoadAsync(TKey key)
        {
            if (_keys.Count == 0)
                _future = Context.Enqueue(Fetch);

            _keys.Add(key);

            var batchResult = await _future.ConfigureAwait(false);

            return batchResult[key];
        }

        async Task<IEnumerable> IDataLoader.LoadAsync(object key)
        {
            return await LoadAsync((TKey)key).ConfigureAwait(false);
        }
    }
}
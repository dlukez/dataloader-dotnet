using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Collects keys into a batch to be executed in a single query.
    /// </summary>
    public class FetchBasedDataLoader<TKey, TValue> : DataLoaderBase<TKey, TValue>
    {
        private readonly Fetch<TKey, TValue> _fetch;

        /// <summary>
        /// Creates a new <see cref="FetchBasedDataLoader"/>.
        /// </summary>
        public FetchBasedDataLoader(FetchDelegate<TKey, TValue> fetchDelegate)
        {
            _fetch = new Fetch<TKey, TValue>(fetchDelegate);
        }

        /// <summary>
        /// Creates a new <see cref="FetchBasedDataLoader"/> bound to the specified context.
        /// </summary>
        public FetchBasedDataLoader(FetchDelegate<TKey, TValue> fetchDelegate, DataLoaderContext context)
        {
            _fetch = new Fetch<TKey, TValue>(fetchDelegate);
            SetContext(context);
        }

        protected override async Task<IEnumerable<TValue>> GetTaskInternal(TKey key)
        {
            var lookup = await _fetch;
            return lookup[key];
        }

        protected override Task ExecuteAsyncInternal(IEnumerable<TKey> keys) => _fetch.ExecuteAsync(keys);
    }
}
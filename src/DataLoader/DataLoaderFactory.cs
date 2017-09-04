using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataLoader
{
    /// <summary>
    /// Provides methods for obtaining loader instances.
    /// </summary>
    public class DataLoaderFactory
    {
        private readonly ConcurrentDictionary<object, IDataLoader> _cache = new ConcurrentDictionary<object, IDataLoader>();
        private readonly DataLoaderContext _loaderContext;

        /// <summary>
        /// Creates a new <see cref="DataLoaderFactory"/> that produces loaders for the given <see cref="DataLoaderContext"/>.
        /// </summary>
        public DataLoaderFactory(DataLoaderContext loaderContext)
        {
            _loaderContext = loaderContext;
        }

        /// <summary>
        /// Retrieves a cached loader for the given key, creating one if none is found.
        /// </summary>
        public IDataLoader<TReturn> GetOrCreateLoader<TReturn>(
            object key, Func<Task<TReturn>> fetchDelegate)
        {
            return (IDataLoader<TReturn>)_cache.GetOrAdd(key,
                _ => new RootDataLoader<TReturn>(fetchDelegate, _loaderContext));
        }

        /// <summary>
        /// Retrieves a cached loader for the given key, creating one if none is found.
        /// </summary>
        public IDataLoader<TKey, TReturn> GetOrCreateLoader<TKey, TReturn>(
            object key, Func<IEnumerable<TKey>, Task<Dictionary<TKey, TReturn>>> fetchDelegate)
        {
            return (IDataLoader<TKey, TReturn>)_cache.GetOrAdd(key,
                _ => new ObjectDataLoader<TKey, TReturn>(fetchDelegate, _loaderContext));
        }

        /// <summary>
        /// Retrieves a cached loader for the given key, creating one if none is found.
        /// </summary>
        public IDataLoader<TKey, IEnumerable<TReturn>> GetOrCreateLoader<TKey, TReturn>(
            object key, Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetchDelegate)
        {
            return (IDataLoader<TKey, IEnumerable<TReturn>>)_cache.GetOrAdd(key,
                _ => new CollectionDataLoader<TKey, TReturn>(fetchDelegate, _loaderContext));
        }
    }
}
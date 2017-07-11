using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataLoader
{
    public class DataLoaderFactory
    {
        private readonly ConcurrentDictionary<object, IDataLoader> _cache;

        private readonly DataLoaderContext _loadContext;

        public DataLoaderFactory(DataLoaderContext loadContext)
        {
            _cache = new ConcurrentDictionary<object, IDataLoader>();
            _loadContext = loadContext;
        }

        /// <summary>
        /// Retrieves a cached loader for the given key, creating one if none is found.
        /// </summary>
        public IDataLoader<TKey, TReturn> GetOrCreateLoader<TKey, TReturn>(object key, Func<IEnumerable<TKey>, Task<ILookup<TKey, TReturn>>> fetchDelegate)
        {
            return (IDataLoader<TKey, TReturn>)_cache.GetOrAdd(key, _ => new DataLoader<TKey, TReturn>(fetchDelegate, _loadContext));
        }

        /// <summary>
        /// Retrieves a cached loader for the given key, creating one if none is found.
        /// </summary>
        public IDataLoader<TReturn> GetOrCreateLoader<TReturn>(object key, Func<Task<TReturn>> fetchDelegate)
        {
            return (IDataLoader<TReturn>)_cache.GetOrAdd(key, _ => new DataLoader<TReturn>(fetchDelegate, _loadContext));
        }
    }
}
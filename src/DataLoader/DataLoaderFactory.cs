using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataLoader
{
    public class DataLoaderFactory
    {
        private readonly ConcurrentDictionary<object, IDataLoader> _cache = new ConcurrentDictionary<object, IDataLoader>();
        private readonly DataLoaderContext _context;

        /// <summary>
        /// Creates a new <see cref="DataLoaderFactory"/> that manages loaders for a given <see cref="DataLoaderContext"/>.
        /// </summary>
        public DataLoaderFactory(DataLoaderContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves a cached loader for the given key, creating one if none is found.
        /// </summary>
        public IDataLoader<TReturn> GetBasicLoader<TReturn>(object key, Func<Task<TReturn>> fetchDelegate)
            where TReturn : class
        {
            return (IDataLoader<TReturn>)_cache.GetOrAdd(key, _ => new BasicDataLoader<TReturn>(fetchDelegate, _context));
        }

        /// <summary>
        /// Retrieves a cached loader for the given key, creating one if none is found.
        /// </summary>
        public IDataLoader<TKey, TReturn> GetBatchLoader<TKey, TReturn>(object key, Func<IEnumerable<TKey>, Task<Dictionary<TKey, TReturn>>> fetchDelegate)
            where TReturn : class
        {
            return (IDataLoader<TKey, TReturn>)_cache.GetOrAdd(key, _ => new BatchDataLoader<TKey, TReturn>(fetchDelegate, _context));
        }
    }
}
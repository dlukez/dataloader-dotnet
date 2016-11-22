using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Resolvers;
using GraphQL.Types;

namespace DataLoader.StarWars
{
    public class DataLoaderResolver<TSource, TValue> : DataLoaderResolver<TSource, int, TValue>
    {
        public DataLoaderResolver(Func<TSource, int> keySelector, IDataLoader<int, TValue> loader) : base(keySelector, loader)
        {
        }

        public DataLoaderResolver(Func<TSource, int> keySelector, FetchDelegate<int, TValue> fetch) : base(keySelector, fetch)
        {
        }
    }

    /// <summary>
    /// Collects the key for each source item so that they may be processed as a batch.
    /// </summary>
    public class DataLoaderResolver<TSource, TKey, TValue> : IFieldResolver<Task<IEnumerable<TValue>>>
    {
        private readonly Func<TSource, TKey> _keySelector;
        private readonly IDataLoader<TKey, TValue> _loader;

        public DataLoaderResolver(Func<TSource, TKey> keySelector, IDataLoader<TKey, TValue> loader)
        {
            _keySelector = keySelector;
            _loader = loader;
        }

        public DataLoaderResolver(Func<TSource, TKey> keySelector, FetchDelegate<TKey, TValue> fetchDelegate)
            : this(keySelector, new DataLoader<TKey, TValue>(fetchDelegate))
        {
        }

        public Task<IEnumerable<TValue>> Resolve(ResolveFieldContext context)
        {
            var source = (TSource)context.Source;
            var key = _keySelector(source);
            return _loader.LoadAsync(key);
        }

        object IFieldResolver.Resolve(ResolveFieldContext context)
        {
            return Resolve(context);
        }
    }
}

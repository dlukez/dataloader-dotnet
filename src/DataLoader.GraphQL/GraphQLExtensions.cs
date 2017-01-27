using System;
using System.Collections.Generic;
using GraphQL.Builders;
using GraphQL.Types;

namespace DataLoader.GraphQL
{
    public static class GraphQLExtensions
    {
        /// <summary>
        /// Configures a field to resolve batches using a <see cref="DataLoaderResolver{TSource,TKey,TReturn}">DataLoaderResolver</see>.
        /// </summary>
        public static FieldBuilder<TSource, IEnumerable<TReturn>> BatchResolve<TSource, TKey, TReturn>(this FieldBuilder<TSource, object> fieldBuilder, Func<TSource, TKey> keySelector, FetchDelegate<TKey, TReturn> fetchDelegate)
        {
            return fieldBuilder
                .Returns<IEnumerable<TReturn>>()
                .Resolve(new DataLoaderResolver<TSource, TKey, TReturn>(keySelector, fetchDelegate));
        }

        /// <summary>
        /// Configures a field to resolve batches using a <see cref="DataLoaderResolver{TSource,TKey,TReturn}">DataLoaderResolver</see>
        /// </summary>
        public static FieldBuilder<TSource, IEnumerable<TReturn>> BatchResolve<TSource, TKey, TReturn>(this FieldBuilder<TSource, object> fieldBuilder, Func<TSource, TKey> keySelector, DataLoaderResolverDelegate<TKey, TReturn> fetchDelegate)
        {
            return fieldBuilder
                .Returns<IEnumerable<TReturn>>()
                .Resolve(new DataLoaderResolver<TSource, TKey, TReturn>(keySelector, fetchDelegate));
        }

        /// <summary>
        /// Retrieves a batch loader from the current loader context for the field being resolved.
        /// </summary>
        public static IDataLoader<TKey, TReturn> GetDataLoader<TSource, TKey, TReturn>(this ResolveFieldContext<TSource> context, FetchDelegate<TKey, TReturn> fetchDelegate)
        {
            var loadCtx = DataLoaderContext.Current;
            
            if (loadCtx == null)
                throw new NullReferenceException($"No loader context set ({nameof(DataLoaderContext.Current)} is null)");
                
            return loadCtx.GetLoader<TKey, TReturn>(context.FieldDefinition, fetchDelegate);
        }
    }
}
using System;
using System.Collections.Generic;
using GraphQL.Builders;
using GraphQL.Types;

namespace DataLoader.StarWars
{
    public static class GraphQLExtensions
    {
        public static FieldBuilder<TSource, IEnumerable<TValue>> Resolve<TSource, TKey, TValue>(this FieldBuilder<TSource, object> fieldBuilder, Func<TSource, TKey> keySelector, FetchDelegate<TKey, TValue> fetchDelegate)
        {
            return fieldBuilder
                .Returns<IEnumerable<TValue>>()
                .Resolve(new DataLoaderResolver<TSource, TKey, TValue>(keySelector, fetchDelegate));
        }

        public static IDataLoader<int, TValue> GetDataLoader<TValue>(this ResolveFieldContext context, FetchDelegate<int, TValue> fetchDelegate)
        {
            return ((DataLoaderContext) context.RootValue).GetCachedLoader(context.FieldDefinition, fetchDelegate);
        }
    }
}
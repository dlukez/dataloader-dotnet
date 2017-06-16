using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Types;
using Microsoft.EntityFrameworkCore;

namespace DataLoader.StarWars
{
    public class GraphQLUserContext
    {
        public StarWarsContext DataContext { get; }
        public DataLoaderContext LoadContext { get; }

        public GraphQLUserContext(DataLoaderContext loadContext) : this(loadContext, new StarWarsContext().WithNoTracking())
        {
        }

        public GraphQLUserContext(DataLoaderContext loadContext, StarWarsContext dataContext)
        {
            DataContext = dataContext;
            LoadContext = loadContext;
        }
    }

    public static class StarWarsContextExtensions
    {
        public static StarWarsContext WithNoTracking(this StarWarsContext context)
        {
            context.ChangeTracker.AutoDetectChangesEnabled = false;
            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            return context;
        }
    }

    public static class GraphQLUserContextExtensions
    {
        public static StarWarsContext GetDataContext<T>(this ResolveFieldContext<T> context)
        {
            return ((GraphQLUserContext)context.UserContext).DataContext;
        }

        public static IDataLoader<int, TReturn> GetDataLoader<TSource, TReturn>(
            this ResolveFieldContext<TSource> context, Func<IEnumerable<int>, Task<ILookup<int, TReturn>>> fetchDelegate)
        {
            return ((GraphQLUserContext)context.UserContext).LoadContext.GetOrCreateLoader(context.FieldDefinition, fetchDelegate);
        }

        public static IDataLoader<TReturn> GetDataLoader<TSource, TReturn>(
            this ResolveFieldContext<TSource> context, Func<Task<IEnumerable<TReturn>>> fetchDelegate)
        {
            return ((GraphQLUserContext)context.UserContext).LoadContext.GetOrCreateLoader(context.FieldDefinition, fetchDelegate);
        }
    }
}
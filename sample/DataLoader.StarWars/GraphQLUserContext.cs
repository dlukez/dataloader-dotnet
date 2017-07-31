using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL.Types;
using Microsoft.EntityFrameworkCore;

namespace DataLoader.StarWars
{
    public class GraphQLUserContext
    {
        public StarWarsContext DataContext { get; }
        public DataLoaderContext LoadContext { get; }

        public GraphQLUserContext() : this(null, new StarWarsContext().WithNoTracking())
        {
        }

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
        public static GraphQLUserContext GetUserContext<T>(this ResolveFieldContext<T> context)
        {
            return (GraphQLUserContext)context.UserContext;
        }

        public static StarWarsContext GetDataContext<T>(this ResolveFieldContext<T> context)
        {
            return context.GetUserContext().DataContext;
        }

        public static DataLoaderContext GetLoadContext<T>(this ResolveFieldContext<T> context)
        {
            return DataLoaderContext.Current;
            // return context.GetUserContext().LoadContext;
        }

        public static IDataLoader<IEnumerable<TReturn>> GetBasicLoader<TSource, TReturn>(
            this ResolveFieldContext<TSource> context, Func<Task<IEnumerable<TReturn>>> fetchDelegate)
        {
            return context.GetLoadContext().Factory.GetBasicLoader(context.FieldDefinition, fetchDelegate);
        }

        public static IDataLoader<int, IEnumerable<TReturn>> GetBatchLoader<TSource, TReturn>(
            this ResolveFieldContext<TSource> context, Func<IEnumerable<int>, Task<Dictionary<int, IEnumerable<TReturn>>>> fetchDelegate)
        {
            return context.GetLoadContext().Factory.GetBatchLoader(context.FieldDefinition, fetchDelegate);
        }
    }
}
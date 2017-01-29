using GraphQL.Types;

namespace DataLoader.GraphQL.StarWars
{
    public class GraphQLUserContext
    {
        public StarWarsContext DataContext { get; set; }
        public DataLoaderContext LoadContext { get; set; }

        public GraphQLUserContext(DataLoaderContext loadContext) : this(loadContext, new StarWarsContext())
        {
        }

        public GraphQLUserContext(DataLoaderContext loadContext, StarWarsContext dataContext)
        {
            DataContext = dataContext;
            LoadContext = loadContext;
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

        public static IDataLoader<int, TReturn> GetDataLoader<TSource, TReturn>(this ResolveFieldContext<TSource> context, FetchDelegate<int, TReturn> fetchDelegate)
        {
            return context.GetUserContext().LoadContext.GetLoader(context.FieldDefinition, fetchDelegate);
        }
    }
}
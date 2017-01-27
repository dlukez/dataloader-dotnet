using System.Threading;
using System.Threading.Tasks;
using GraphQL.Execution;

namespace DataLoader.GraphQL
{
    /// <summary>
    /// Provides a listener that sets up a DataLoaderContext and triggers it at the
    /// necessary stage in the GraphQL document's execution.
    /// </summary>
    public class DataLoaderListener : DocumentExecutionListenerBase<object>
    {
        private readonly DataLoaderScope _scope;

        public DataLoaderListener()
        {
            _scope = new DataLoaderScope();
        }

        public DataLoaderListener(DataLoaderContext loadCtx)
        {
            _scope = new DataLoaderScope(loadCtx);
        }

        public DataLoaderContext Context => _scope.Context;
        
        public override Task BeforeExecutionAwaitedAsync(object userContext, CancellationToken token)
        {
            using (_scope) return _scope.Context.ExecuteAsync();
        }
    }
}
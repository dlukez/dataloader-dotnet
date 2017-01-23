using System.Threading;
using System.Threading.Tasks;
using DataLoader;
using GraphQL.Execution;

namespace GraphQL.DataLoader
{
    public class DataLoaderListener : DocumentExecutionListenerBase<object>
    {
        private DataLoaderScope _scope;

        public override Task BeforeExecutionAsync(object userContext, CancellationToken token)
        {
            _scope = new DataLoaderScope();
            return Task.CompletedTask;
        }

        public override Task BeforeExecutionAwaitedAsync(object userContext, CancellationToken token)
        {
            _scope.Dispose();
            return Task.CompletedTask;
        }
    }
}
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GraphQL.Execution;

namespace DataLoader.GraphQL
{
    public class DataLoaderListener : DocumentExecutionListenerBase<object>
    {
        private DataLoaderScope _scope;

        public DataLoaderContext Context => _scope.Context;

        public override Task BeforeExecutionAsync(object userContext, CancellationToken token)
        {
            _scope = new DataLoaderScope();
            return Task.CompletedTask;
        }

        public override Task BeforeExecutionAwaitedAsync(object userContext, CancellationToken token)
        {
            using (_scope) return _scope.Context.ExecuteAsync();
        }
    }
}
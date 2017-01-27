using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GraphQL.Execution;

namespace DataLoader.GraphQL
{
    public class DataLoaderListener : DocumentExecutionListenerBase<object>
    {
        private readonly ConditionalWeakTable<object, DataLoaderScope> _contextTable =
            new ConditionalWeakTable<object, DataLoaderScope>();

        public override Task BeforeExecutionAsync(object userContext, CancellationToken token)
        {
            var scope = new DataLoaderScope();
            _contextTable.Add(userContext, scope);
            return Task.CompletedTask;
        }

        public override async Task BeforeExecutionAwaitedAsync(object userContext, CancellationToken token)
        {
            DataLoaderScope scope;
            if (!_contextTable.TryGetValue(userContext, out scope))
                throw new InvalidOperationException("User context has already been garbage collected. Has execution already finished?");
            await scope.Context.ExecuteAsync().ConfigureAwait(false);
            scope.Dispose();
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace DataLoader.Tests
{
    public class DataLoaderTests
    {
        [Fact]
        public void DataLoader_ImplicitContextDefaultsToCurrentContext()
        {
            var loader = new DataLoader<object, object>(_ => null);
            loader.Context.ShouldBeNull();
            var task = DataLoaderContext.Run(ctx =>
            {
                loader.Context.ShouldBe(ctx);
                return Task.FromResult<object>(null);
            });
            task.IsCompleted.ShouldBeTrue();
        }

        [Fact]
        public void DataLoader_ConstructorSupportsExplicitContext()
        {
            var loadCtx = new DataLoaderContext();
            var loader = new DataLoader<object, object>(_ => null, loadCtx);
            loader.Context.ShouldBe(loadCtx);
        }

        [Fact]
        public void DataLoader_CanSetExplicitContext()
        {

            var loader = new DataLoader<object, object>(_ => null);
            loader.Context.ShouldBeNull();
            
            var loadCtx = new DataLoaderContext();
            loader.SetContext(loadCtx);
            loader.Context.ShouldBe(loadCtx);

            var loadCtx2 = new DataLoaderContext();
            loader.SetContext(loadCtx2);
            loader.Context.ShouldBe(loadCtx2);

            loader.SetContext(null);
            loader.Context.ShouldBeNull();
        }
    }
}
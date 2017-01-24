using System;
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

        [Fact]
        public async Task DataLoader_ConsecutiveLoadsWork()
        {
            var loader = new DataLoader<int, object>(async (ids) =>
            {
                await Task.Delay(150);
                return ids.ToLookup(id => id, id => new object());
            });

            var awaits = 0;
            var func = new Func<Task>(async () =>
            {
                awaits++;
                var a = await loader.LoadAsync(1);
                awaits++;
                var b = await loader.LoadAsync(2);
                awaits++;
                var c = await loader.LoadAsync(3);
            });

            var task = func();
            await loader.ExecuteAsync();
            Console.WriteLine($"Total: {awaits}");
            task.IsCompleted.ShouldBeTrue();
            // Should.CompleteIn(task, TimeSpan.FromSeconds(3));
        }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public async Task DataLoader_ShouldTriggerDescendentLoads()
        {
            var count = 0;

            var loader = new DataLoader<int, object>(async (ids) =>
            {
                count++;
                await Task.Delay(100);
                return ids.ToLookup(id => id, id => new object());
            });

            var func = new Func<Task>(async () =>
            {
                Log($"Request {count + 1}");
                await loader.LoadAsync(1);
                Log($"Request {count + 1}");
                await loader.LoadAsync(2);
                Log($"Request {count + 1}");
                await loader.LoadAsync(3);
                Log($"Done {count}");
            });

            var task = func();
            await loader.ExecuteAsync();

            Log($"Task: {task.Status:f}");
            Log($"Count: {count}");

            count.ShouldBe(3);
            Should.CompleteIn(task, TimeSpan.FromSeconds(3));
        }

        private void Log(string msg)
        {
            Debug.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - {msg}");
        }
    }
}
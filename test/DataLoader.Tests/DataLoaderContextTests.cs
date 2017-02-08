using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace DataLoader.Tests
{
    public class DataLoaderContextTests
    {
        [Fact]
        public async Task DataLoaderContext_Run_SetsCurrentContext()
        {
            DataLoaderContext.Current.ShouldBeNull();

            await DataLoaderContext.Run(ctx =>
            {
                DataLoaderContext.Current.ShouldBe(ctx);
                DataLoaderContext.Current.ShouldNotBeNull();
            });

            DataLoaderContext.Current.ShouldBeNull();
        }

        [Fact]
        public async Task DataLoaderContext_Run_CanBeNested()
        {
            await DataLoaderContext.Run(async outerCtx =>
            {
                DataLoaderContext.Current.ShouldBe(outerCtx);

                await DataLoaderContext.Run(innerCtx =>
                {
                    innerCtx.ShouldNotBe(outerCtx);
                    innerCtx.ShouldBe(DataLoaderContext.Current);
                });

                DataLoaderContext.Current.ShouldBe(outerCtx);
            });

            DataLoaderContext.Current.ShouldBeNull();
        }

        [Fact]
        public async Task DataLoaderContext_Run_FlowsCurrentContext()
        {
            await DataLoaderContext.Run(async () =>
            {
                var ctx = DataLoaderContext.Current;
                var threadId = Thread.CurrentThread.ManagedThreadId;

                // Test with `Task.Yield`.
                await Task.Yield();
                DataLoaderContext.Current.ShouldBe(ctx);

                // Test with `Task.Delay`.
                await Task.Delay(100);
                DataLoaderContext.Current.ShouldBe(ctx);

                // Test with `Task.Run`.
                await Task.Run(() => DataLoaderContext.Current.ShouldBe(ctx));

                // Test with `Thread`.
                var thread = new Thread(() => DataLoaderContext.Current.ShouldBe(ctx));
                thread.Start();
                thread.Join();
            });
        }

        [Fact]
        public void DataLoaderContext_Run_AllowsParallelContexts()
        {
            const int n = 2;
            var barrier = new Barrier(n);
            var contexts = new ConcurrentBag<DataLoaderContext>();

            Action<int> action = _ =>
            {
                DataLoaderContext.Run(ctx =>
                {
                    barrier.SignalAndWait();
                    ctx.ShouldBe(DataLoaderContext.Current);
                    contexts.Add(DataLoaderContext.Current);
                    return Task.FromResult(1);
                }).Wait();
            };

            var result = Parallel.For(0, n, action);
            result.IsCompleted.ShouldBeTrue();
            contexts.Count.ShouldBe(n);
            contexts.ShouldBeUnique();
        }

        [Fact]
        public void DataLoaderContext_Run_TriggersConsecutiveLoads()
        {
            var loadCount = 0;

            var loader = new DataLoader<int, int>(async ids =>
            {
                await Task.Delay(150);
                loadCount++;
                return ids.ToLookup(id => id);
            });

            var task = DataLoaderContext.Run(async () =>
            {
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - {loadCount}");
                await loader.LoadAsync(1);

                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - {loadCount}");
                await loader.LoadAsync(2);

                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - {loadCount}");
                await loader.LoadAsync(3);

                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - {loadCount}");
                await Task.WhenAll(loader.LoadAsync(4), loader.LoadAsync(5), loader.LoadAsync(6));

                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - {loadCount}");
                var t7 = loader.LoadAsync(7);
                var t8 = loader.LoadAsync(8);
                var t9 = loader.LoadAsync(9);
                Thread.Sleep(200);
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - {loadCount} (again)");
                await loader.LoadAsync(10);

                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - {loadCount}");
                t7.IsCompleted.ShouldBeTrue();
                t8.IsCompleted.ShouldBeTrue();
                t9.IsCompleted.ShouldBeTrue();
            });

            Should.CompleteIn(task, TimeSpan.FromSeconds(10));
            loadCount.ShouldBe(5);
        }
    }
}
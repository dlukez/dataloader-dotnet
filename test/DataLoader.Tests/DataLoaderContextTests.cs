using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
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
                return Task.FromResult(1);
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
                    return Task.FromResult(1);
                });
                DataLoaderContext.Current.ShouldBe(outerCtx);
                return 2;
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

                // Test with `await`.
                await Task.Yield();
                DataLoaderContext.Current.ShouldBe(ctx);

                // Test with `Task.Run`.
                await Task.Run(() =>
                {
                    threadId.ShouldNotBe(Thread.CurrentThread.ManagedThreadId);
                    DataLoaderContext.Current.ShouldBe(ctx);
                });

                // Test with `Thread`.
                var thread = new Thread(() =>
                {
                    threadId.ShouldNotBe(Thread.CurrentThread.ManagedThreadId);
                    DataLoaderContext.Current.ShouldBe(ctx);
                });
                thread.Start();
                thread.Join();

                return true;
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
        public void DataLoaderContext_TriggersConsecutiveLoads()
        {
            var loadCount = 0;

            var loader = new DataLoader<int, int>(async ids =>
            {
                await Task.Delay(150);
                loadCount++;
                return ids.ToLookup(id => id);
            });

            Should.CompleteIn(async () =>
            {
                await DataLoaderContext.Run(async () =>
                {
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Before 1");
                    await loader.LoadAsync(1);

                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Before 2");
                    await loader.LoadAsync(2);

                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Before 3");
                    await loader.LoadAsync(3);

                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Before 4,5,6");
                    await Task.WhenAll(loader.LoadAsync(4), loader.LoadAsync(5), loader.LoadAsync(6));

                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Before 7,8,9");
                    var t7 = loader.LoadAsync(7);
                    var t8 = loader.LoadAsync(8);
                    var t9 = loader.LoadAsync(9);

//                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Delay...");
//                    await Task.Delay(700);

                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Sleeping...");
                    Thread.Sleep(800);

                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Before 10");
                    await loader.LoadAsync(10);

                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Testing 7,8,9 are completed");
                    t7.IsCompleted.ShouldBeTrue();
                    t8.IsCompleted.ShouldBeTrue();
                    t9.IsCompleted.ShouldBeTrue();

                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - Returning");
                    return 0;
                });

                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} task {Task.CurrentId} - ✓ Done");
            }, TimeSpan.FromSeconds(10));

            loadCount.ShouldBe(5);
        }

        [Fact]
        public async Task DataLoaderContext_Completes()
        {
            var ctx = new DataLoaderContext();
            var loadCount = 0;

            FetchDelegate<int, int> fetch = async (ids) =>
            {
                await Task.Delay(new Random().Next(10, 700));
                loadCount++;
                return ids.ToLookup(id => id);
            };

            var loader1 = new DataLoader<int, int>(fetch, ctx);
            var loader2 = new DataLoader<int, int>(fetch, ctx);
            var tasks = new[]
            {
                loader1.LoadAsync(1),
                loader1.LoadAsync(2),
                loader2.LoadAsync(1),
                loader2.LoadAsync(2),
                ctx.Completion
            };

            await ctx.ExecuteAsync();
            loadCount.ShouldBe(2);
            Should.CompleteIn(Task.WhenAll(tasks), TimeSpan.FromSeconds(5));
        }
    }
}
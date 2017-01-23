using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
                return Task.FromResult(1);
            });

            DataLoaderContext.Current.ShouldBeNull();
        }

        [Fact]
        public async Task DataLoaderContext_Run_CanBeNested_Explicit()
        {
            await DataLoaderContext.Run(async (outerCtx) =>
            {
                DataLoaderContext.Current.ShouldBe(outerCtx);
                await DataLoaderContext.Run(innerCtx =>
                {
                    innerCtx.ShouldNotBe(outerCtx);
                    DataLoaderContext.Current.ShouldNotBe(outerCtx);
                    DataLoaderContext.Current.ShouldBe(innerCtx);
                    return Task.FromResult(1);
                });
                DataLoaderContext.Current.ShouldBe(outerCtx);
                return 2;
            });
            
            DataLoaderContext.Current.ShouldBeNull();
        }

        [Fact]
        public async Task DataLoaderContext_Run_CanBeNested_Implicit()
        {
            await DataLoaderContext.Run(async () =>
            {
                var outerCtx = DataLoaderContext.Current;
                outerCtx.ShouldNotBeNull();
                await DataLoaderContext.Run(ctx =>
                {
                    var innerCtx = DataLoaderContext.Current;
                    innerCtx.ShouldNotBeNull();
                    innerCtx.ShouldNotBe(outerCtx);
                    return Task.FromResult(1);
                });
                outerCtx.ShouldNotBeNull();
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
            var contexts = new ConcurrentBag<DataLoaderContext>();
            const int participants = 2;
            var barrier = new Barrier(participants);
            Action action = async () =>
            {
                await DataLoaderContext.Run(_ =>
                {
                    barrier.SignalAndWait();
                    contexts.Add(DataLoaderContext.Current);
                    return Task.FromResult(true);
                });
            };

            var result = Parallel.For(0, participants, _ => action());
            result.IsCompleted.ShouldBeTrue();
            contexts.Count.ShouldBe(participants);
            contexts.ShouldBeUnique();
        }

        private class Node
        {
            public int Id { get; set; }
        }

        [Fact]
        public void DataLoaderContext_PumpsDependentLoads()
        {
            var context = new DataLoaderContext();

            var loadCount = 0;

            var loader = new DataLoader<int, Node>(async (ids) =>
            {
                await Task.Delay(50);
                loadCount++;
                return ids.Select(x => new Node {Id = x}).ToLookup(x => x.Id);
            }, context);

            var task = new Func<Task<int>>(async () =>
            {
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - 1");
                await loader.LoadAsync(1);
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - 2");
                await loader.LoadAsync(2);
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - 3");
                await loader.LoadAsync(3);
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - 4,5");
                await Task.WhenAll(loader.LoadAsync(4), loader.LoadAsync(5));
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} - ️✓");
                return 0;
            })();

            context.StartLoading();
            Should.CompleteIn(task, TimeSpan.FromSeconds(2));
            loadCount.ShouldBe(4);
        }

        [Fact]
        public void DataLoaderContext_Completes()
        {
            Should.CompleteIn(async () =>
            {
                var ctx = new DataLoaderContext();
                var loadCount = 0;

                FetchDelegate<int, Node> fetch = async (ids) =>
                {
                    await Task.Delay(100);
                    loadCount++;
                    return ids.Select(x => new Node { Id = x }).ToLookup(x => x.Id);
                };

                var loader1 = new DataLoader<int, Node>(fetch, ctx);

                var loader2 = new DataLoader<int, Node>(fetch, ctx);

                var tasks = new[]
                {
                    loader1.LoadAsync(1),
                    loader1.LoadAsync(2),
                    loader2.LoadAsync(1),
                    loader2.LoadAsync(2)
                };

                ctx.StartLoading();

                await Task.WhenAll(tasks);
                await ctx.Completion;

                loadCount.ShouldBe(2);
            }, TimeSpan.FromSeconds(2));
        }
    }
}
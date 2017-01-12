using System;
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
        public async void DataLoaderContext_Run_SetsCurrentContext()
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
        public async void DataLoaderContext_Run_CanBeNested()
        {
            await DataLoaderContext.Run(outerCtx =>
                DataLoaderContext.Run(innerCtx =>
                {
                    innerCtx.ShouldNotBe(outerCtx);
                    DataLoaderContext.Current.ShouldNotBe(outerCtx);
                    return Task.FromResult(1);
                }));
        }

        [Fact]
        public async Task DataLoaderContext_Run_FlowsCurrentContext()
        {
            await DataLoaderContext.Run(async _ =>
            {
                var ctx = DataLoaderContext.Current;
                var threadId = Thread.CurrentThread.ManagedThreadId;

                // Test with `await`.
                await Task.Yield(); 
                threadId.ShouldNotBe(Thread.CurrentThread.ManagedThreadId);
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
            List<DataLoaderContext> contexts = new List<DataLoaderContext>();
            const int participants = 2;
            var barrier = new Barrier(participants);
            Action action = async () =>
            {
                await DataLoaderContext.Run(_ =>
                {
                    barrier.SignalAndWait();
                    lock (contexts) contexts.Add(DataLoaderContext.Current);
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
            public IEnumerable<Node> Children { get; set; }
        }

        [Fact]
        public async void DataLoaderContext_Run_PumpsContinuations()
        {
            var loadCount = 0;

            var loader = new DataLoader<int, Node>(async ids =>
            {
                await Task.Delay(50);
                loadCount++;
                return ids.Select(x => new Node {Id = x}).ToLookup(x => x.Id);
            });

            await DataLoaderContext.Run(async ctx =>
            {
                await loader.LoadAsync(1);
                await loader.LoadAsync(2);
                await loader.LoadAsync(3);
                return await loader.LoadAsync(4);
            });

            loadCount.ShouldBe(4);
        }
    }
}
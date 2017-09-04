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
        public void DataLoaderContext_Current_IsNullByDefault()
        {
            DataLoaderContext.Current.ShouldBeNull();
        }

        [Fact]
        public async Task DataLoaderContext_Run_SetsCurrent()
        {
            await DataLoaderContext.Run(() =>
            {
                DataLoaderContext.Current.ShouldNotBeNull();
                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task DataLoaderContext_Run_UnsetsCurrent()
        {
            await DataLoaderContext.Run(() => Task.CompletedTask);
            DataLoaderContext.Current.ShouldBeNull();
        }

        [Fact]
        public async Task DataLoaderContext_Run_CanBeNested()
        {
            await DataLoaderContext.Run(async outerCtx =>
            {
                await DataLoaderContext.Run(async innerCtx =>
                {
                    innerCtx.ShouldNotBe(outerCtx);
                    innerCtx.ShouldBe(DataLoaderContext.Current);
                    await Task.Yield();
                    innerCtx.ShouldBe(DataLoaderContext.Current);
                });

                DataLoaderContext.Current.ShouldBe(outerCtx);
            });
        }

        [Fact]
        public async Task DataLoaderContext_Run_FlowsCurrentContext()
        {
            var checkpoints = 0;

            await DataLoaderContext.Run(async _ =>
            {
                var ctx = DataLoaderContext.Current;

                // Test with `Task.Yield`.
                await Task.Yield();
                DataLoaderContext.Current.ShouldBe(ctx);
                checkpoints++; // 1

                // Test with `Task.Delay`.
                await Task.Delay(100);
                DataLoaderContext.Current.ShouldBe(ctx);
                checkpoints++; // 2

                // Test with `Task.Run`.
                await Task.Run(() => DataLoaderContext.Current.ShouldBe(ctx));
                checkpoints++; // 3

                // Test with `Thread`.
                var thread = new Thread(() =>
                {
                    DataLoaderContext.Current.ShouldBe(ctx);
                    checkpoints++; // 4
                });
                thread.Start();
                thread.Join();
            });

            checkpoints.ShouldBe(4);
        }

        [Fact]
        public async Task DataLoaderContext_Run_AllowsParallelContexts()
        {
            const int n = 2;
            var barrier = new Barrier(n);
            var contexts = new ConcurrentBag<DataLoaderContext>();

            Func<Task> action = async () =>
            {
                await DataLoaderContext.Run(ctx =>
                {
                    barrier.SignalAndWait();
                    ctx.ShouldBe(DataLoaderContext.Current);
                    contexts.Add(DataLoaderContext.Current);
                    return Task.FromResult(1);
                });
            };

            var t1 = Task.Run(action);
            var t2 = Task.Run(action);

            await Task.WhenAll(t1, t2);

            contexts.Count.ShouldBe(n);
            contexts.ShouldBeUnique();
        }

        [Fact]
        public async Task DataLoaderContext_Run_TriggersConsecutiveLoads()
        {
            var loadCount = 0;

            await DataLoaderContext.Run(async ctx =>
            {
                var loader = ctx.Factory.GetOrCreateLoader<int, int>(
                    "somekey",
                    async ids =>
                    {
                        await Task.Delay(100);
                        loadCount++;
                        return ids.ToDictionary(id => id);
                    });

                var one = await loader.LoadAsync(1); // 1
                var two = await loader.LoadAsync(2); // 2
                var three = await loader.LoadAsync(3); // 3

                var fourfivesix = await Task.WhenAll( // 4
                    loader.LoadAsync(4),
                    loader.LoadAsync(5),
                    loader.LoadAsync(6)
                );
            });

            loadCount.ShouldBe(4);
        }

        [Fact]
        public async Task DataLoaderContext_Run_HandlesUnrelatedAwaits()
        {
            var loadCount = 0;

            await DataLoaderContext.Run(async ctx =>
            {
                var loader = ctx.Factory.GetOrCreateLoader<int, int>(
                    "somekey",
                    async ids =>
                    {
                        await Task.Delay(50);
                        loadCount++;
                        return ids.ToDictionary(id => id);
                    });

                var one = await loader.LoadAsync(1);
                await Task.Delay(50);
                var two = await loader.LoadAsync(2);
            });

            loadCount.ShouldBe(2);
        }
    }
}
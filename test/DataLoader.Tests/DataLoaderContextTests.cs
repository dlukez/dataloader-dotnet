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
        public void Current_IsNullByDefault()
        {
            DataLoaderContext.Current.ShouldBeNull();
        }

        [Fact]
        public async Task SetsCurrent()
        {
            await DataLoaderContext.Run(() =>
            {
                DataLoaderContext.Current.ShouldNotBeNull();
                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task UnsetsCurrent()
        {
            await DataLoaderContext.Run(() => Task.CompletedTask);
            DataLoaderContext.Current.ShouldBeNull();
        }

        [Fact]
        public async Task CanBeNested()
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
        public async Task FlowsLikeSyncContext()
        {
            var checkpoints = 0;

            await DataLoaderContext.Run(async _ =>
            {
                var ctx = DataLoaderContext.Current;

                // Should flow over `Task.Yield`
                await Task.Yield();
                DataLoaderContext.Current.ShouldBe(ctx);
                checkpoints++; // 1

                // Should flow over `Task.Delay`
                await Task.Delay(100);
                DataLoaderContext.Current.ShouldBe(ctx);
                checkpoints++; // 2

                // Shouldn't flow into Task.Run
                await Task.Run(() => DataLoaderContext.Current.ShouldBe(null));
                DataLoaderContext.Current.ShouldBe(ctx);
                checkpoints++; // 3

                // Shouldn't flow into a new Thread
                var thread = new Thread(() => { DataLoaderContext.Current.ShouldBe(null); });
                thread.Start();
                thread.Join();
                DataLoaderContext.Current.ShouldBe(ctx);
                checkpoints++; // 4
            });

            checkpoints.ShouldBe(4);
        }

        [Fact]
        public async Task AllowsParallelContexts()
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
        public async Task TriggersConsecutiveLoads()
        {
            var loadCount = 0;

            await DataLoaderContext.Run(async ctx =>
            {
                var loader = ctx.GetOrCreateLoader<int, int>(
                    "somekey",
                    async ids =>
                    {
                        await Task.Delay(100);
                        loadCount++;
                        return ids.ToDictionary(id => id);
                    });

                var one = await loader.LoadAsync(1);
                var two = await loader.LoadAsync(2);
                var three = await loader.LoadAsync(3);
                var fourfivesix = await Task.WhenAll(
                    loader.LoadAsync(4),
                    loader.LoadAsync(5),
                    loader.LoadAsync(6)
                );
            });

            loadCount.ShouldBe(4);
        }

        [Fact]
        public async Task HandlesUnrelatedAwaits()
        {
            var loadCount = 0;

            await DataLoaderContext.Run(async ctx =>
            {
                var loader = ctx.GetOrCreateLoader<int, int>(
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

        [Fact]
        public void RespectsAlreadyCancelledToken()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var didRun = false;
            var task = DataLoaderContext.Run(token =>
            {
                didRun = true;
                return null;
            }, cts.Token);

            didRun.ShouldBe(false);
            task.IsCanceled.ShouldBe(true);
        }

        [Fact]
        public void CanBeCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(50);

            var didRun = false;
            var task = DataLoaderContext.Run(async token =>
            {
                didRun = true;
                await Task.Delay(300, token);
            }, cts.Token);

            didRun.ShouldBeTrue();
            task.ShouldThrow<TaskCanceledException>();
        }
    }
}
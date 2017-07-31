using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine.ClientProtocol;
using System.Collections.Generic;

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
        public async Task DataLoaderContext_Run_UnsetsCurrent()
        {
            var task = DataLoaderContext.Run(() => Task.Delay(100));
            DataLoaderContext.Current.ShouldBeNull();
            await task;
        }

        [Fact]
        public async Task DataLoaderContext_Run_CanBeNested()
        {
            await DataLoaderContext.Run(async outerCtx =>
            {
                var task = DataLoaderContext.Run(async innerCtx =>
                {
                    innerCtx.ShouldNotBe(outerCtx);
                    innerCtx.ShouldBe(DataLoaderContext.Current);
                    await Task.Yield();
                    innerCtx.ShouldBe(DataLoaderContext.Current);
                });

                DataLoaderContext.Current.ShouldBe(outerCtx);
                await task;
            });
        }

        [Fact]
        public async Task DataLoaderContext_Run_FlowsCurrentContext()
        {
            var checkpoints = 0;

            await DataLoaderContext.Run(async () =>
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

            var loader = new BatchDataLoader<int, IEnumerable<int>>(async ids =>
            {
                await Task.Delay(150);
                loadCount++;
                return ids.ToDictionary(id => id, id => Enumerable.Range(0, id));
            });

            var task = DataLoaderContext.Run(async () =>
            {
                var one = await loader.LoadAsync(1);
                var two = await loader.LoadAsync(2);
                var three = await loader.LoadAsync(3);

                var fourfivesix = await Task.WhenAll(
                    loader.LoadAsync(4),
                    loader.LoadAsync(5),
                    loader.LoadAsync(6)
                );

                var t7 = loader.LoadAsync(7);
                var t8 = loader.LoadAsync(8);
                var t9 = loader.LoadAsync(9);
                Thread.Sleep(200);

                var ten = await loader.LoadAsync(10);
                t7.IsCompleted.ShouldBeTrue();
                t8.IsCompleted.ShouldBeTrue();
                t9.IsCompleted.ShouldBeTrue();
            });

            Should.CompleteIn(task, TimeSpan.FromSeconds(5));
            loadCount.ShouldBeGreaterThanOrEqualTo(5);
        }

        [Fact]
        public void DataLoaderContext_Run_HandlesUnrelatedAwaits()
        {
            var loadCount = 0;

            var loader = new BatchDataLoader<int, IEnumerable<int>>(async ids =>
            {
                await Task.Delay(100);
                loadCount++;
                var test = ids.ToLookup(id => id);
                return test.ToDictionary(src => src.Key, src => src.AsEnumerable());
            });

            var task = DataLoaderContext.Run(async () =>
            {
                var one = await loader.LoadAsync(1);
                await Task.Delay(100);
                var two = await loader.LoadAsync(2);
            });

            Should.CompleteIn(task, TimeSpan.FromSeconds(5));
            loadCount.ShouldBe(2);
        }
    }
}

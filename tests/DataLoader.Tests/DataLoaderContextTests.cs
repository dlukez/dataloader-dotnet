using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace DataLoader.Tests
{
    public class DataLoaderContextTests
    {
        [Fact]
        public void DataLoaderContext_Run_SetsContext()
        {
            DataLoaderContext.Current.ShouldBeNull();

            DataLoaderContext.Run(_ =>
            {
                DataLoaderContext.Current.ShouldNotBeNull();
                return Task.FromResult(1);
            });

            DataLoaderContext.Current.ShouldBeNull();
        }

        [Fact]
        public void DataLoaderContext_Run_CanBeNested()
        {
            DataLoaderContext.Run(_ =>
            {
                var lastCtx = DataLoaderContext.Current;
                return DataLoaderContext.Run(__ =>
                {
                    DataLoaderContext.Current.ShouldNotBe(lastCtx);
                    return Task.FromResult(1);
                });
            });
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
                Debug.Assert(threadId != Thread.CurrentThread.ManagedThreadId);
                DataLoaderContext.Current.ShouldBe(ctx);

                // Test with `Task.Run`.
                await Task.Run(() =>
                {
                    Debug.Assert(threadId != Thread.CurrentThread.ManagedThreadId);
                    DataLoaderContext.Current.ShouldBe(ctx);
                });

                // Test with `Thread`.
                var thread = new Thread(() =>
                {
                    Debug.Assert(threadId != Thread.CurrentThread.ManagedThreadId);
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

            Action action = () =>
            {
                DataLoaderContext.Run(_ =>
                {
                    barrier.SignalAndWait();

                    lock (contexts)
                    {
                        contexts.Add(DataLoaderContext.Current);
                    }

                    return Task.FromResult(true);
                });
            };

            Parallel.For(0, participants, _ => action());

            contexts.Count.ShouldBe(participants);
            contexts.ShouldBeUnique();
        }

        [Fact]
        public void DataLoaderContext_Flush_CanHandleMultipleLevelsOfNestedFetches()
        {
            var limit = 4;
            var count = 1;

            Should.CompleteIn(async () =>
            {
                await DataLoaderContext.Run(ctx =>
                {
                    var loader = new TaskBasedDataLoader<int, int>(async ids =>
                    {
                        await Task.Delay(10);
                        count++;
                        return ids.SelectMany(x => new[]
                        {
                            new KeyValuePair<int, int>(x, x * 2),
                            new KeyValuePair<int, int>(x, x * 2 + 1)
                        }).ToLookup(x => x.Key, x => x.Value);
                    }, ctx);

                    Func<int, Task<object>> resolve = null;

                    resolve = async x =>
                    {
                        Debug.WriteLine($"x: {x}");
                        if (x >= (2 ^ limit)) return x;
                        var items = await loader.LoadAsync(x);
                        var tasks = items.Select(resolve);
                        var nested = await Task.WhenAll(tasks);
                        return nested;
                    };

                    return resolve(1);
                });
            }, TimeSpan.FromSeconds(5));

            count.ShouldBe(limit);
        }
    }
}
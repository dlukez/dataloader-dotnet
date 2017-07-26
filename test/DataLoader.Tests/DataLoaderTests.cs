using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace DataLoader.Tests
{
    public class DataLoaderTests
    {
        [Fact]
        public async Task DataLoader_WithoutBoundContext_ReflectsCurrentContext()
        {
            var loader = new BatchDataLoader<object, object>(_ => null);

            loader.Context.ShouldBeNull();

            var task = DataLoaderContext.Run(async ctx =>
            {
                loader.Context.ShouldBe(ctx);

                await Task.Delay(200);

                loader.Context.ShouldBe(ctx);
            });

            loader.Context.ShouldBeNull();

            await task;

            loader.Context.ShouldBeNull();
        }
    }
}
using DataLoader;
using Shouldly;
using Xunit;

namespace DataLoader.Tests
{
    public class DataLoaderTests
    {
        [Fact]
        public void DataLoader_ReflectsCurrentContext()
        {
            var loader = new DataLoader<object, object>(_ => null);
            loader.Context.ShouldBeNull();
            DataLoaderContext.Run(ctx => { loader.Context.ShouldBe(ctx); });
            loader.Context.ShouldBeNull();
        }
    }
}
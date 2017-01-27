using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace DataLoader.Tests
{
    public class DataLoaderTests
    {
        [Fact]
        public void DataLoader_ImplicitContextDefaultsToCurrentContext()
        {
            var loader = new DataLoader<object, object>(_ => null);
            loader.Context.ShouldBeNull();

            var ctx = new DataLoaderContext();
            DataLoaderContext.SetCurrentContext(ctx);
            loader.Context.ShouldBe(ctx);

            DataLoaderContext.SetCurrentContext(null);
            loader.Context.ShouldBeNull();
        }

        [Fact]
        public void DataLoader_ConstructorOverloaWithContext()
        {
            var loadCtx = new DataLoaderContext();
            var loader = new DataLoader<object, object>(_ => null, loadCtx);
            loader.Context.ShouldBe(loadCtx);
        }

        [Fact]
        public void DataLoader_CanChangeBoundContext()
        {
            var loader = new DataLoader<object, object>(_ => null);
            loader.Context.ShouldBeNull();
            
            var loadCtx = new DataLoaderContext();
            loader.SetContext(loadCtx);
            loader.Context.ShouldBe(loadCtx);

            var loadCtx2 = new DataLoaderContext();
            loader.SetContext(loadCtx2);
            loader.Context.ShouldBe(loadCtx2);

            loader.SetContext(null);
            loader.Context.ShouldBeNull();
        }
    }
}
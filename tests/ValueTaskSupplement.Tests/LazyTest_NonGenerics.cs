#pragma warning disable CS1998

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace ValueTaskSupplement.Tests
{
    public class LazyTest_NonGenerics
    {
        [Fact]
        public async Task Sync()
        {
            var calledCount = 0;
            var syncLazy = ValueTaskEx.Lazy(async () => { calledCount++; });

            calledCount.Should().Be(0);

            await syncLazy;
            calledCount.Should().Be(1);

            await syncLazy;
            calledCount.Should().Be(1);
        }

        [Fact]
        public async Task Async()
        {
            var calledCount = 0;
            var syncLazy = ValueTaskEx.Lazy(async () => { calledCount++; await Task.Delay(TimeSpan.FromSeconds(1)); });
            calledCount.Should().Be(0);

            await ValueTaskEx.WhenAll(syncLazy, syncLazy, syncLazy);

            calledCount.Should().Be(1);
        }
    }
}

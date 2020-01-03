#pragma warning disable CS1998

using FluentAssertions;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ValueTaskSupplement.Tests
{
    public class LazyTest
    {
        [Fact]
        public async Task Sync()
        {
            var calledCount = 0;
            var syncLazy = ValueTaskEx.Lazy(async () => { calledCount++; return 100; });

            calledCount.Should().Be(0);

            var value = await syncLazy;
            value.Should().Be(100);
            calledCount.Should().Be(1);

            var value2 = await syncLazy;
            value.Should().Be(100);

            calledCount.Should().Be(1);
        }

        [Fact]
        public async Task Async()
        {
            var calledCount = 0;
            var asyncLazyOriginal = ValueTaskEx.Lazy(async () => { calledCount++; await Task.Delay(TimeSpan.FromSeconds(1)); return new object(); });
            var asyncLazy = asyncLazyOriginal;
            calledCount.Should().Be(0);

            var (v1, v2, v3) = await ValueTaskEx.WhenAll(asyncLazy.AsValueTask(), asyncLazy.AsValueTask(), asyncLazyOriginal.AsValueTask());

            calledCount.Should().Be(1);

            object.ReferenceEquals(v1, v2).Should().BeTrue();
            object.ReferenceEquals(v2, v3).Should().BeTrue();
        }
    }
}

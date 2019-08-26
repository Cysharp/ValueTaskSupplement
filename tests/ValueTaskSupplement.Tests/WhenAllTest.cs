using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ValueTaskSupplement.Tests
{
    public class WhenAllTest
    {
        [Fact]
        public async Task AllSync()
        {
            var a = CreateSync(1);
            var b = CreateSync(2);
            var c = CreateSync(3);
            var result = await ValueTaskEx.WhenAll(a, b, c);
            result.Should().Be((1, 2, 3));
        }

        [Fact]
        public async Task WithAsync()
        {
            var a = CreateSync(1);
            var b = CreateAsync(2);
            var c = CreateAsync(3);
            var result = await ValueTaskEx.WhenAll(a, b, c);
            result.Should().Be((1, 2, 3));
        }

        [Fact]
        public async Task Array()
        {
            var a = CreateSync(1);
            var b = CreateAsync(2);
            var c = CreateAsync(3);
            var result = await ValueTaskEx.WhenAll(new[] { a, b, c });
            result.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        }

        [Fact]
        public async Task Extension()
        {
            var result = await new[] { CreateAsync(1), CreateAsync(2), CreateAsync(3) };
            result[0].Should().Be(1);
            result[1].Should().Be(2);
            result[2].Should().Be(3);

            var (x, y) = await (CreateAsync(10), CreateAsync(99));
            x.Should().Be(10);
            y.Should().Be(99);
        }

        ValueTask<int> CreateSync(int i)
        {
            return new ValueTask<int>(i);
        }

        async ValueTask<int> CreateAsync(int i)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10));
            return i;
        }
    }
}

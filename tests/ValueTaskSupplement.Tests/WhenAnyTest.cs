using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ValueTaskSupplement.Tests
{
    public class WhenAnyTest
    {
        [Fact]
        public async Task AnySync()
        {
            var a = CreateSync(1);
            var b = CreateSync(2);
            var c = CreateSync(3);
            var result = await ValueTaskEx.WhenAny(a, b, c);
            result.winArgumentIndex.Should().Be(0);
            result.Item2.hasResult.Should().BeTrue();
            result.Item2.result0.Should().Be(1);
            result.Item3.hasResult.Should().BeFalse();
            result.Item4.hasResult.Should().BeFalse();
        }

        [Fact]
        public async Task WithAsync()
        {
            var a = CreateAsync(1);
            var b = CreateSync(2);
            var c = CreateAsync(3);
            var result = await ValueTaskEx.WhenAny(a, b, c);
            result.winArgumentIndex.Should().Be(1);
            result.Item3.result1.Should().Be(2);
            result.Item2.hasResult.Should().BeFalse();
            result.Item3.hasResult.Should().BeTrue();
            result.Item4.hasResult.Should().BeFalse();
        }

        [Fact]
        public async Task Array()
        {
            var a = CreateSync(1);
            var b = CreateAsync(2);
            var c = CreateAsync(3);
            var result = await ValueTaskEx.WhenAny(new[] { a, b, c });
            result.winArgumentIndex.Should().Be(0);
            result.result.Should().Be(1);
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

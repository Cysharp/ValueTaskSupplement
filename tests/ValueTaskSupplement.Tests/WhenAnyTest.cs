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

            result.result0.Should().Be(1);
        }

        [Fact]
        public async Task WithAsync()
        {
            var a = CreateAsync(1);
            var b = CreateSync(2);
            var c = CreateAsync(3);
            var result = await ValueTaskEx.WhenAny(a, b, c);
            result.winArgumentIndex.Should().Be(1);

            result.result1.Should().Be(2);
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

        [Fact]
        public async Task Timeout()
        {
            {
                var delay = Task.Delay(TimeSpan.FromMilliseconds(100));
                var vtask = CreateAsync(999);
                var (hasValue, value) = await ValueTaskEx.WhenAny(vtask, delay);
                hasValue.Should().BeTrue();
                value.Should().Be(999);
            }
            {
                var delay = Task.Delay(TimeSpan.FromMilliseconds(100));
                var vtask = CreateAsyncSlow(999);
                var (hasValue, value) = await ValueTaskEx.WhenAny(vtask, delay);
                hasValue.Should().BeFalse();
            }
            {
                var delay = Task.Delay(TimeSpan.FromMilliseconds(100));
                var vtask = CreateSync(999);
                var (hasValue, value) = await ValueTaskEx.WhenAny(vtask, delay);
                hasValue.Should().BeTrue();
                value.Should().Be(999);
            }
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

        async ValueTask<int> CreateAsyncSlow(int i)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            return i;
        }
    }
}

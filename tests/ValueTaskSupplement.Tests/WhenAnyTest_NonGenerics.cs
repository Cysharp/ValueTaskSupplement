using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace ValueTaskSupplement.Tests
{
    public class WhenAnyTest_NonGenerics
    {
        [Fact]
        public async Task AnySync()
        {
            var a = CreateSync();
            var b = CreateSync();
            var c = CreateSync();
            var result = await ValueTaskEx.WhenAny(a, b, c);
            result.Should().Be(0);
        }

        [Fact]
        public async Task WithAsync()
        {
            var a = CreateAsync();
            var b = CreateSync();
            var c = CreateAsyncSlow();
            var result = await ValueTaskEx.WhenAny(a, b, c);
            result.Should().Be(1);
        }

        [Fact]
        public async Task Array()
        {
            var a = CreateSync();
            var b = CreateAsync();
            var c = CreateAsyncSlow();
            var result = await ValueTaskEx.WhenAny(new[] { a, b, c });
            result.Should().Be(0);
        }

        [Fact]
        public async Task Timeout()
        {
            {
                var delay = Task.Delay(TimeSpan.FromMilliseconds(100));
                var vtask = CreateAsync();
                var index = await ValueTaskEx.WhenAny(vtask, delay);
                index.Should().Be(0);
            }
            {
                var delay = Task.Delay(TimeSpan.FromMilliseconds(100));
                var vtask = CreateAsyncSlow();
                var index = await ValueTaskEx.WhenAny(vtask, delay);
                index.Should().Be(1);
            }
            {
                var delay = Task.Delay(TimeSpan.FromMilliseconds(100));
                var vtask = CreateSync();
                var index = await ValueTaskEx.WhenAny(vtask, delay);
                index.Should().Be(0);
            }
        }

        ValueTask CreateSync()
        {
            return new ValueTask();
        }

        async ValueTask CreateAsync()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }

        async ValueTask CreateAsyncSlow()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }
    }
}

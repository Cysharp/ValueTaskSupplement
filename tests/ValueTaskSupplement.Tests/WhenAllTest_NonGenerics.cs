using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace ValueTaskSupplement.Tests
{
    public class WhenAllTest_NonGenerics
    {
        [Fact]
        public async Task AllSync()
        {
            var a = CreateSync();
            var b = CreateSync();
            var c = CreateSync();

            a.IsCompletedSuccessfully.Should().BeTrue();
            b.IsCompletedSuccessfully.Should().BeTrue();
            c.IsCompletedSuccessfully.Should().BeTrue();

            await ValueTaskEx.WhenAll(a, b, c);

            a.IsCompletedSuccessfully.Should().BeTrue();
            b.IsCompletedSuccessfully.Should().BeTrue();
            c.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Fact]
        public async Task WithAsync()
        {
            var a = CreateSync();
            var b = CreateAsync();
            var c = CreateAsync();

            a.IsCompletedSuccessfully.Should().BeTrue();
            b.IsCompletedSuccessfully.Should().BeFalse();
            c.IsCompletedSuccessfully.Should().BeFalse();

            await ValueTaskEx.WhenAll(a, b, c);

            a.IsCompletedSuccessfully.Should().BeTrue();
            b.IsCompletedSuccessfully.Should().BeTrue();
            c.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Fact]
        public async Task Array()
        {
            var a = CreateSync();
            var b = CreateAsync();
            var c = CreateAsync();

            a.IsCompletedSuccessfully.Should().BeTrue();
            b.IsCompletedSuccessfully.Should().BeFalse();
            c.IsCompletedSuccessfully.Should().BeFalse();

            await ValueTaskEx.WhenAll(new[] { a, b, c });

            a.IsCompletedSuccessfully.Should().BeTrue();
            b.IsCompletedSuccessfully.Should().BeTrue();
            c.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Fact]
        public async Task Extension()
        {
            await new[] { CreateAsync(), CreateAsync(), CreateAsync() };
            await (CreateAsync(), CreateAsync());
        }

        ValueTask CreateSync()
        {
            return new ValueTask();
        }

        async ValueTask CreateAsync()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }
    }
}

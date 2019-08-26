using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ValueTaskSupplement.Tests
{
    public class TempListTest
    {
        [Fact]
        public void Foo()
        {
            var xs = new TempList<int>(6);
            xs.Add(10);
            xs.Add(20);
            xs.Add(30);
            xs.Add(40);
            xs.Add(50);

            xs.AsSpan().SequenceEqual(new[] { 10, 20, 30, 40, 50 }).Should().BeTrue();

            xs.Dispose();

            var ys = new TempList<int>(3);

            foreach (var item in Enumerable.Range(1, 100))
            {
                ys.Add(item);
            }

            ys.AsSpan().SequenceEqual(Enumerable.Range(1, 100).ToArray()).Should().BeTrue();

            ys.Dispose();
        }
    }
}

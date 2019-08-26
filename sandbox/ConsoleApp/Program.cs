using System;
using System.Threading.Tasks;
using ValueTaskSupplement;

namespace ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var ta = Foo();
            var tb = new ValueTask<int>(100);
            var tc = Foo();

            var tako = ValueTaskEx.WhenAll(ta, tb, tc);

            var (a, b, c) = await tako;

        }
        static async ValueTask<int> Foo()
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            return 10;
        }
    }

}

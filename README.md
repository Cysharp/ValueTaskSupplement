ValueTaskSupplement
===
[![CircleCI](https://circleci.com/gh/Cysharp/ValueTaskSupplement.svg?style=svg)](https://circleci.com/gh/Cysharp/ValueTaskSupplement)

Append supplemental methods(`WhenAny`, `WhenAll`, `Lazy`) to ValueTask.

> PM> Install-Package [ValueTaskSupplement](https://www.nuget.org/packages/ValueTaskSupplement)

How to Use
---
```csharp
using ValueTaskSupplement; // namespace

async ValueTask Demo()
{
    ValueTask<int> task1 = LoadAsyncA();
    ValueTask<string> task2 = LoadAsyncB();
    ValueTask<bool> task3 = LoadAsyncC();

    // await ValueTasks with tuple
    var (a, b, c) = await ValueTaskEx.WhenAll(task1, task2, task3);

    // WhenAny with int winIndex
    var (winIndex, a, b, c) = await ValueTaskEx.WhenAny(task1, task2, task2);

    // Lazy(called factory once and delayed) but you can use same type(ValueTask)
    ValueTask<int> asyncLazy = ValueTaskEx.Lazy(async () => 9999);
}
```

All method is based on `IValueTaskSource` so fast and less allocation.

License
---
This library is under the MIT License.

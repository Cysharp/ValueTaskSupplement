[![GitHub Actions](https://github.com/Cysharp/ValueTaskSupplement/workflows/Build-Debug/badge.svg)](https://github.com/Cysharp/ValueTaskSupplement/actions) [![Releases](https://img.shields.io/github/release/Cysharp/ValueTaskSupplement.svg)](https://github.com/Cysharp/ValueTaskSupplement/releases)

ValueTaskSupplement
===

`ValueTask<T>` is a new standard of define async methods especially after being introduced `IValueTaskSource`. But it lacks utility like WhenAny, etc. ValueTaskSupplement appends supplemental methods(`WhenAny`, `WhenAll`, `Lazy`) to ValueTask and it is implemented by `IValueTaskSource` so fast and less allocation.

> PM> Install-Package [ValueTaskSupplement](https://www.nuget.org/packages/ValueTaskSupplement)

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
## Table of Contents

- [How to Use](#how-to-use)
- [WhenAll](#whenall)
- [WhenAny](#whenany)
- [Lazy](#lazy)
- [Factory](#factory)
- [License](#license)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

How to Use
---
```csharp
using ValueTaskSupplement; // namespace

async ValueTask Demo()
{
    // `ValueTaskEx` is the only types from provided this library

    // like this individual types
    ValueTask<int> task1 = LoadAsyncA();
    ValueTask<string> task2 = LoadAsyncB();
    ValueTask<bool> task3 = LoadAsyncC();

    // await ValueTasks(has different type each other) with tuple
    var (a, b, c) = await ValueTaskEx.WhenAll(task1, task2, task3);

    // WhenAny with int winIndex
    var (winIndex, a, b, c) = await ValueTaskEx.WhenAny(task1, task2, task2);

    // like Timeout
    var (hasLeftResult, value) = await ValueTaskEx.WhenAny(task1, Task.Delay(TimeSpan.FromSeconds(1)));
    if (!hasLeftResult) throw new TimeoutException();

    // Lazy(called factory once and delayed)
    AsyncLazy<int> asyncLazy = ValueTaskEx.Lazy(async () => 9999);
}
```

WhenAll
---

```csharp
// Same type and return array(same as Task.WhenAll).
public static ValueTask<T[]> WhenAll<T>(IEnumerable<ValueTask<T>> tasks);

// T0, T1, to...
public static ValueTask<(T0, T1)> WhenAll<T0, T1>(ValueTask<T0> task0, ValueTask<T1> task1);
...
// T0 ~ T15
public static ValueTask<(T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15)> WhenAll<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13, ValueTask<T14> task14, ValueTask<T15> task15);
```

`IEnumerable<ValueTask<T>>` and `(ValueTask<T0>, ValueTask<T1>, ...)` can await directly.

```csharp
using ValueTaskSupplement;

// same as ValueTaskEx.WhenAll(new []{ }), ValueTaskEx.WhenAll(A, B, C)
var result = await new[] { LoadAsyncA(), LoadAsyncB(), LoadAsyncC() };
var (x, y, z) = await (LoadAsyncA(), LoadAsyncB(), LoadAsyncC());
```

WhenAny
---

```csharp
// binary api is useful to await with Delay(like for check Timeout).
public static ValueTask<(bool hasResultLeft, T result)> WhenAny<T>(ValueTask<T> left, Task right);
public static ValueTask<(bool hasResultLeft, T result)> WhenAny<T>(ValueTask<T> left, ValueTask right);

// receive sequence is like Task.WhenAny but returns `int winArgumentIndex`.
public static ValueTask<(int winArgumentIndex, T result)> WhenAny<T>(IEnumerable<ValueTask<T>> tasks);

// Return result of tuple methods is guaranteed only winArgumentIndex value
public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1)> WhenAny<T0, T1>(ValueTask<T0> task0, ValueTask<T1> task1);
...
// T0 ~ T15
public static ValueTask<(int winArgumentIndex, T0 result0, T1 result1, T2 result2, T3 result3, T4 result4, T5 result5, T6 result6, T7 result7, T8 result8, T9 result9, T10 result10, T11 result11, T12 result12, T13 result13, T14 result14, T15 result15)> WhenAny<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ValueTask<T0> task0, ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5, ValueTask<T6> task6, ValueTask<T7> task7, ValueTask<T8> task8, ValueTask<T9> task9, ValueTask<T10> task10, ValueTask<T11> task11, ValueTask<T12> task12, ValueTask<T13> task13, ValueTask<T14> task14, ValueTask<T15> task15);
```

Lazy
---

```csharp
// AsyncLazy<T> is similar to Lazy<T>, it can store in field
// it await directly or can convert to ValueTask easily to use WhenAll.
public static AsyncLazy<T> Lazy<T>(Func<ValueTask<T>> factory);

public class AsyncLazy<T>
{
    public ValueTask<T> AsValueTask();
    public ValueTaskAwaiter<T> GetAwaiter();   
    public static implicit operator ValueTask<T>(AsyncLazy<T> source);
}
```

Factory
---

```csharp
public static ValueTask<T> FromResult<T>(T result);
public static ValueTask FromTask(Task result);
public static ValueTask<T> FromTask<T>(Task<T> result);
public static ValueTask AsValueTask(this Task result);
public static ValueTask<T> AsValueTask<T>(this Task<T> result);
```

License
---
This library is under the MIT License.

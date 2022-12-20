using System;
using System.Threading;
using System.Threading.Tasks;

namespace Statsig.Lib;

internal abstract class Delay
{
    internal static Func<TimeSpan, CancellationToken, Task> Wait = Task.Delay;

    internal static void Reset()
    {
        Wait = Task.Delay;
    }
}
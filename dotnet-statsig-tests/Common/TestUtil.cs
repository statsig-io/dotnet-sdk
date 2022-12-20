using System;
using System.Threading.Tasks;
using Xunit;

namespace dotnet_statsig_tests;

public abstract class TestUtil
{
    public static async Task WaitFor(Action blockingAction, int timeoutMs = 1000)
    {
        var check = Task.Run(() =>
        {
            blockingAction();
            return Task.CompletedTask;
        });

        var result = await Task.WhenAny(check, Task.Delay(timeoutMs));

        // If they don't match, we timed out while waiting
        Assert.Equal(result, check);
    }
}
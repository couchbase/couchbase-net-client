#nullable enable
using System;
using System.Threading.Tasks;

namespace Couchbase.UnitTests.Helpers;

/// <summary>
/// Helper methods for async test assertions that need to wait for conditions
/// without using arbitrary Task.Delay values that can be flaky on CI.
/// </summary>
public static class AsyncTestHelper
{
    /// <summary>
    /// Polls a condition until it returns true or timeout is reached.
    /// Uses exponential backoff to avoid busy-waiting while remaining responsive.
    /// </summary>
    /// <param name="condition">The condition to check.</param>
    /// <param name="timeout">Maximum time to wait (default 5 seconds).</param>
    /// <param name="pollingInterval">Initial polling interval (default 10ms, doubles each iteration up to 200ms).</param>
    /// <returns>True if condition was met, false if timeout occurred.</returns>
    public static async Task<bool> WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan? timeout = null,
        TimeSpan? pollingInterval = null)
    {
        var timeoutValue = timeout ?? TimeSpan.FromSeconds(5);
        var interval = pollingInterval ?? TimeSpan.FromMilliseconds(10);
        var maxInterval = TimeSpan.FromMilliseconds(200);
        var deadline = DateTime.UtcNow + timeoutValue;

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(interval).ConfigureAwait(false);

            // Exponential backoff up to max interval
            interval = TimeSpan.FromMilliseconds(Math.Min(interval.TotalMilliseconds * 2, maxInterval.TotalMilliseconds));
        }

        // Final check
        return condition();
    }

    /// <summary>
    /// Waits for a condition to become true, throwing TimeoutException if it doesn't.
    /// </summary>
    /// <param name="condition">The condition to check.</param>
    /// <param name="timeout">Maximum time to wait (default 5 seconds).</param>
    /// <param name="message">Custom message for the timeout exception.</param>
    public static async Task WaitForConditionOrThrowAsync(
        Func<bool> condition,
        TimeSpan? timeout = null,
        string? message = null)
    {
        var result = await WaitForConditionAsync(condition, timeout).ConfigureAwait(false);
        if (!result)
        {
            throw new TimeoutException(message ?? "Condition was not met within the timeout period.");
        }
    }

    /// <summary>
    /// Waits for a value to stabilize (not change) over a specified period.
    /// Useful for verifying that something has stopped (e.g., call count after dispose).
    /// </summary>
    /// <typeparam name="T">Type of value to monitor.</typeparam>
    /// <param name="getValue">Function to get the current value.</param>
    /// <param name="stableDuration">How long the value must remain stable (default 200ms).</param>
    /// <param name="timeout">Maximum time to wait for stability (default 5 seconds).</param>
    /// <returns>The stable value.</returns>
    public static async Task<T> WaitForStableValueAsync<T>(
        Func<T> getValue,
        TimeSpan? stableDuration = null,
        TimeSpan? timeout = null) where T : IEquatable<T>
    {
        var stableDurationValue = stableDuration ?? TimeSpan.FromMilliseconds(200);
        var timeoutValue = timeout ?? TimeSpan.FromSeconds(5);
        var deadline = DateTime.UtcNow + timeoutValue;
        var checkInterval = TimeSpan.FromMilliseconds(50);

        var lastValue = getValue();
        var stableSince = DateTime.UtcNow;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(checkInterval).ConfigureAwait(false);

            var currentValue = getValue();
            if (!currentValue.Equals(lastValue))
            {
                lastValue = currentValue;
                stableSince = DateTime.UtcNow;
            }
            else if (DateTime.UtcNow - stableSince >= stableDurationValue)
            {
                return currentValue;
            }
        }

        return getValue();
    }
}

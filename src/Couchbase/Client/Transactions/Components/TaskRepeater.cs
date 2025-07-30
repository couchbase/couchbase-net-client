#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Couchbase.Client.Transactions.Components;

internal enum RepeatAction
{
    NoRepeat = 0,
    RepeatWithDelay = 1,
    RepeatNoDelay = 2,
    RepeatWithBackoff = 3
}

internal class TaskRepeater
{
    public static async Task<T> RepeatUntilSuccessOrThrow<T>(
        Func<Task<(RepeatAction retry, T finalVal)>> func, int retryLimit = 100_000,
        [CallerMemberName] string caller = nameof(RepeatUntilSuccessOrThrow))
    {
        var retryCount = -1;
        var opRetryBackoffMs = 1;
        while (retryCount < retryLimit)
        {
            retryCount++;
            var result = await func().CAF();
            switch (result.retry)
            {
                case RepeatAction.RepeatWithDelay:
                    await OpRetryDelay().CAF();
                    break;
                case RepeatAction.RepeatWithBackoff:
                    await Task.Delay(opRetryBackoffMs).CAF();
                    opRetryBackoffMs = Math.Min(opRetryBackoffMs * 10, 100);
                    break;
                case RepeatAction.RepeatNoDelay:
                    break;
                case RepeatAction.NoRepeat:
                    return result.finalVal;
            }
        }

        throw new InvalidOperationException(
            $"Retry Limit ({retryLimit}) exceeded in method {caller}");
    }

    public static Task RepeatUntilSuccessOrThrow(Func<Task<RepeatAction>> func, int retryLimit = 100_000, [CallerMemberName] string caller = nameof(RepeatUntilSuccessOrThrow)) =>
        RepeatUntilSuccessOrThrow<object>(async () =>
    {
        var retry = await func().CAF();
        return (retry, string.Empty);
    }, retryLimit, caller);

    private static Task OpRetryDelay() => Task.Delay(Transactions.OpRetryDelay);
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2025 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

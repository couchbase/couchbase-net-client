using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Couchbase.Transactions.LogUtil
{
    internal static class LoggingExtensions
    {
        internal static string SafeSubstring(this string str, int maxChars)
        {
            if (string.IsNullOrEmpty(str))
            {
                return string.Empty;
            }

            return str.Substring(0, Math.Min(maxChars, str.Length));
        }

        internal static IDisposable BeginMethodScope(this ILogger logger, [CallerMemberName] string method = "UnknownScope") => logger.BeginScope(method);
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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

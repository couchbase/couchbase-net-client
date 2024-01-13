using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    internal static class ThrowHelper
    {
        [DoesNotReturn]
#if NET6_0_OR_GREATER
        [StackTraceHidden]
#endif
        public static void ThrowArgumentException(string message, string paramName) =>
            throw new ArgumentException(message, paramName);

        [DoesNotReturn]
#if NET6_0_OR_GREATER
        [StackTraceHidden]
#endif
        public static void ThrowArgumentNullException(string paramName) =>
            throw new ArgumentNullException(paramName);

        [DoesNotReturn]
#if NET6_0_OR_GREATER
        [StackTraceHidden]
#endif
        public static void ThrowObjectDisposedException(string objectName) =>
            throw new ObjectDisposedException(objectName);
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

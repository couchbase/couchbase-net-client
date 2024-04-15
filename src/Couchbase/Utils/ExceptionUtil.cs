#nullable enable

namespace Couchbase.Utils
{
    internal static class ExceptionUtil
    {
        public const string ConnectException = "Could not connect to {0}. See inner exception for details.";

        public const string ConnectTimeoutExceptionMsg =
            "Could not connect to {0} after {1} seconds. The KvConnectTimeout is set to {2} seconds.";

        public const string OperationTimeout = "The operation has timed out.";

        public static string GetMessage(string msg, params object?[] args)
        {
            return string.Format(msg, args);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

#endregion

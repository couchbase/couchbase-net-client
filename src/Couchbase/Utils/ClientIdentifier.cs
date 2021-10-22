using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using Couchbase.Core.IO.Operations;

namespace Couchbase.Utils
{
    internal static class ClientIdentifier
    {
        // To avoid the User-Agent string being parsed multiple times, keep the parsed value and reuse
        private static readonly ProductInfoHeaderValue[] UserAgentSegments = new[]
            {
                $"couchbase-net-sdk/{CurrentAssembly.Version}",
                $"(clr/{RuntimeInformation.FrameworkDescription})",
                $"(os/{RuntimeInformation.OSDescription})"
            }
            .Select(ProductInfoHeaderValue.Parse)
            .ToArray();

        private static readonly string ClientDescription = string.Join(" ", UserAgentSegments.Select(p => p.ToString()));

        internal static ulong InstanceId = SequenceGenerator.GetRandomLong();

        public static string GetClientDescription() => ClientDescription;

        public static void SetUserAgent(HttpRequestHeaders headers)
        {
            for (var i = 0; i < UserAgentSegments.Length; i++)
            {
                headers.UserAgent.Add(UserAgentSegments[i]);
            }
        }

        public static string FormatConnectionString(ulong connectionId)
        {
            // format as hex padded to 16 spaces
            return $"{InstanceId:x16}/{connectionId:x16}";
        }
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

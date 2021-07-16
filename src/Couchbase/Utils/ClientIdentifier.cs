using System.Runtime.InteropServices;
using Couchbase.Core.IO.Operations;

namespace Couchbase.Utils
{
    internal static class ClientIdentifier
    {
        private const string DescriptionFormat = "couchbase-net-sdk/{0} (clr/{1}) (os/{2})";

        private static readonly string ClientDescription =
            string.Format(DescriptionFormat, CurrentAssembly.Version, RuntimeInformation.FrameworkDescription,
                RuntimeInformation.OSDescription);

        internal static ulong InstanceId = SequenceGenerator.GetRandomLong();

        public static string GetClientDescription() => ClientDescription;

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

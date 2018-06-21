using System;
using Couchbase.IO.Operations;

#if !NET45
using System.Runtime.InteropServices;
#endif

namespace Couchbase.Utils
{
    public static class ClientIdentifier
    {
        private const string DescriptionFormat = "couchbase-net-sdk/{0} (clr/{1}) (os/{2})";

        internal static ulong InstanceId = SequenceGenerator.GetRandomLong();

        public static string GetClientDescription()
        {
#if NET452
            return string.Format(DescriptionFormat, CurrentAssembly.Version, Environment.Version, Environment.OSVersion);
#else
            return string.Format(DescriptionFormat, CurrentAssembly.Version, RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription);
#endif
        }

        public static string FormatConnectionString(ulong connectionId)
        {
            // format as hex padded to 16 spaces
            return $"{InstanceId:x16}/{connectionId:x16}";
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

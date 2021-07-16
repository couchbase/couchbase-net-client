using System;

namespace Couchbase
{
    internal enum IpAddressMode
    {
        /// <summary>
        /// Default behavior, currently equivalent to <see cref="PreferIpv6"/>.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Force IPv4, ignoring any IPv6 records
        /// </summary>
        ForceIpv4 = 1,

        /// <summary>
        /// Prefer IPv4 over IPv6 records, but use IPv6 if that is the only option available.
        /// </summary>
        PreferIpv4 = 2,

        /// <summary>
        /// Force IPv6, ignoring any IPv4 records
        /// </summary>
        ForceIpv6 = 3,

        /// <summary>
        /// Prefer IPv6 over IPv4 records, but use IPv4 if that is the only option available.
        /// </summary>
        PreferIpv6 = 4
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

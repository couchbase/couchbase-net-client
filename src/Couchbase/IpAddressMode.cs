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

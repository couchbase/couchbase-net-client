#if NET45
using System;
#else
using System.Runtime.InteropServices;
#endif

namespace Couchbase.Utils
{
    public static class ClientIdentifier
    {
        private const string DescriptionFormat = "couchbase-net-sdk/{0} (clr/{1}) (os/{2})";

        public static string GetClientDescription()
        {
#if NET45
            return string.Format(DescriptionFormat, CurrentAssembly.Version, Environment.Version, Environment.OSVersion);
#else
            return string.Format(DescriptionFormat, CurrentAssembly.Version, RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription);
#endif
        }
    }
}
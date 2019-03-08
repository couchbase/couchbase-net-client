using System;
using System.Runtime.InteropServices;

namespace Couchbase.Utils
{
    public static class ClientIdentifier
    {
        private static readonly Random Random = new Random();
        private const string DescriptionFormat = "couchbase-net-sdk/{0} (clr/{1}) (os/{2})";

        internal static ulong InstanceId = GetRandomLong();

        public static string GetClientDescription()
        {
            return string.Format(DescriptionFormat, CurrentAssembly.Version, RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription);
        }

        public static string FormatConnectionString(ulong connectionId)
        {
            // format as hex padded to 16 spaces
            return $"{InstanceId:x16}/{connectionId:x16}";
        }

        public static ulong GetRandomLong()
        {
            var bytes = new byte[8];
            lock (Random)
            {
                Random.NextBytes(bytes);
            }

            return BitConverter.ToUInt64(bytes, 0);
        }
    }
}

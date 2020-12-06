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

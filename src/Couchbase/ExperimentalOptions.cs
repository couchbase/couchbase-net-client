using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase
{
    /// <summary>
    /// Settings to enable various experiments. These experiments may improve performance, but also
    /// may have stability issues. If successful, they will become the standard approach.
    /// </summary>
    public class ExperimentalOptions
    {
        /// <summary>
        /// Use System.Threading.Channels for connection pool distribution.
        /// </summary>
        [InterfaceStability(Level.Volatile)]
        public bool ChannelConnectionPools { get; set; } = false;
    }
}

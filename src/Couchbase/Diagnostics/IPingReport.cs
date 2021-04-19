using System.Collections.Generic;

namespace Couchbase.Diagnostics
{
    public interface IPingReport
    {
        /// <summary>
        /// Gets the report identifier.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the Ping Report version.
        /// </summary>
        short Version { get; }

        /// <summary>
        /// Gets the bucket configuration revision.
        /// </summary>
        ulong ConfigRev { get; }

        /// <summary>
        /// Gets the SDK identifier.
        /// </summary>
        string Sdk { get; }

        /// <summary>
        /// Gets the service endpoints.
        /// </summary>
        IDictionary<string, IEnumerable<IEndpointDiagnostics>> Services { get; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Management
{
    /// <summary>
    /// Number of replicas to be configured for this bucket. Required parameter when creating a Couchbase bucket. Default 1, minimum 0, maximum 3.
    /// </summary>
    public enum ReplicaNumber
    {
        Zero,
        One,

        /// <summary>
        /// Default.
        /// </summary>
        Two,
        Three
    }
}

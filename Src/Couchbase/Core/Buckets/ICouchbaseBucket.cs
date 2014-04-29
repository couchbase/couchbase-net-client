using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.N1QL;
using Couchbase.Views;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// Represents a persistent storage container for documents and other resources which is shared across the cluster.
    /// </summary>
    /// <remarks>Extends the IBocket interface by adding support for View and N1QL queries</remarks>
    /// <see cref="http://docs.couchbase.com/couchbase-manual-2.5/cb-admin/#data-storage"/>
    public interface ICouchbaseBucket 
        : IBucket, IViewSupportable, IQuerySupportable
    {
    }
}

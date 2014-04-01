using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.N1QL;
using Couchbase.Views;

namespace Couchbase.Core.Buckets
{
    public interface ICouchbaseBucket 
        : IBucket, IViewSupportable, IQuerySupportable
    {
    }
}

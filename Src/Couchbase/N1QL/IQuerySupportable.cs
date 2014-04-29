using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.N1QL
{
    /// <summary>
    /// Adds support for executing N1QL, a SQL-like language, queries against a Couchbase Cluster.
    /// </summary>
    /// <see cref="http://www.couchbase.com/communities/n1ql"/>
    public interface IQuerySupportable
    {
        IQueryResult<T> Query<T>(string query);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.N1QL
{
    public interface IQuerySupportable
    {
        IQueryResult<T> Query<T>(string query);
    }
}

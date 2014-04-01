using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.N1QL
{
    public interface IQueryResult<T>
    {
        List<T> Rows { get; set; } 


    }
}

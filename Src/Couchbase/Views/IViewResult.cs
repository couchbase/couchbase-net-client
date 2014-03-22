using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Views
{
    interface IViewResult<T>
    {
        //{"total_rows": 0, "rows": []}

        uint TotalRows { get; set; }

        List<T> Rows { get; set; } 
    }
}

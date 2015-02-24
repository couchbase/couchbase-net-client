using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.N1QL
{
    public interface IQueryPlan
    {
        /// <summary>
        /// Gives the N1QL string representation of the query plan.
        /// </summary>
        /// <returns>The N1QL string for the query plan.</returns>
        string ToN1ql();
    }
}

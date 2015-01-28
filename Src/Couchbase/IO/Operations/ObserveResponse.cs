using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.IO.Operations
{
    public enum ObserveResponse
    {
        DurabilitySatisfied,
        DurabilityNotSatisfied
    }

    public enum Durability
    {
        Satisfied,
        NotSatisfied,
        Unspecified
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Transactions.DataModel
{
    internal record QueryBeginWorkResponse(string? txid);
}

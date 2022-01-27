using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Transactions.DataModel
{
    internal record QueryErrorCause(object? cause, bool? rollback, bool? retry, string? raise);
}

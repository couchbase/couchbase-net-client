
using System.Collections.Generic;
using Couchbase.Core.IO.Operations;

namespace Couchbase.KeyValue;
internal interface ILookupInResultInternal : ILookupInResult
{
    IList<LookupInSpec> Specs{ get; }
    Flags Flags { get; }
}

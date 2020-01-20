using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core;
using Couchbase.Core.IO.Operations.SubDocument;

namespace Couchbase.KeyValue
{
    public class MutateInResult : IMutateInResult
    {
        public MutateInResult(ulong cas, MutationToken token, IList<OperationSpec> specs)
        {

        }
        public ulong Cas { get; }
        public MutationToken MutationToken { get; set; }
        public T ContentAs<T>(int index)
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;

namespace Couchbase.IO.Operations
{
    /// <summary>
    /// Removes a key from the database, failing if it doesn't exist.
    /// </summary>
    internal sealed class DeleteOperation : OperationBase<object>
    {
        public DeleteOperation(string key, IVBucket vBucket)
            : base(key, vBucket)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Delete; }
        }

        public override ArraySegment<byte> CreateExtras()
        {
            return new ArraySegment<byte>();
        }
    }
}

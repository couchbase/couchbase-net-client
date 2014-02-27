using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;

namespace Couchbase.IO.Operations
{
    internal class GetOperation<T> : OperationBase<T>
    {
        public GetOperation(string key, IVBucket vBucket)
            : base(key, vBucket)
        {
        }

        public override ArraySegment<byte> CreateBody()
        {
            return new ArraySegment<byte>(new byte[] { });
        }

        public override ArraySegment<byte> CreateExtras()
        {
            return new ArraySegment<byte>(new byte[] { });
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Get; }
        }
    }
}

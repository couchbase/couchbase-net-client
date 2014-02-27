using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;

namespace Couchbase.IO.Operations
{
    internal class SetOperation<T> : OperationBase<T>
    {
        public SetOperation(string key, T value, IVBucket vBucket)
            : base(key, value, vBucket)
        {
        }

        public override OperationCode OperationCode
        {
            get { return Operations.OperationCode.Set; }
        }
    }
}

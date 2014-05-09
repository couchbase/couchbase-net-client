using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;

namespace Couchbase.IO.Operations
{
    /// <summary>
    /// Replace a key in the database, failing if the key does not exist.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class ReplaceOperation<T> : OperationBase<T>
    {
        public ReplaceOperation(string key, T value, IVBucket vBucket) 
            : base(key, value, vBucket)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Replace; }
        }
    }
}

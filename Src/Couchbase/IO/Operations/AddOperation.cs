using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;

namespace Couchbase.IO.Operations
{
    /// <summary>
    /// Add a key to the database, failing if the key exists.
    /// </summary>
    /// <typeparam name="T">The value to add to the database.</typeparam>
    internal sealed class AddOperation<T> : OperationBase<T>
    {
        public AddOperation(string key, T value, IVBucket vBucket)
            : base(key, value, vBucket)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Add; }
        }
    }
}

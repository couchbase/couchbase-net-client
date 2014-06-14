using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations
{
    internal sealed class DecrementOperation : OperationBase<long>
    {
        private const int BodyOffset = 31;
        private readonly ulong _delta;
        private readonly uint _expiration;
        private readonly ulong _initial;

       public DecrementOperation(string key, ulong initial, ulong delta, uint expiration, IVBucket vBucket) 
           : base(key, vBucket)
        {
            _delta = delta;
            _initial = initial;
            _expiration = expiration;
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Decrement; }
        }

        public override ArraySegment<byte> CreateExtras()
        {
            var extras = new ArraySegment<byte>(new byte[20]);
            BinaryConverter.EncodeUInt64(_delta, extras.Array, 0);
            BinaryConverter.EncodeUInt64(_initial, extras.Array, 8);
            BinaryConverter.EncodeUInt32(_expiration, extras.Array, 16);
            return extras;
        }

        public override ArraySegment<byte> CreateBody()
        {
            return new ArraySegment<byte>();
        }

        public override int Offset
        {
            get { return BodyOffset; }
        }
    }
}

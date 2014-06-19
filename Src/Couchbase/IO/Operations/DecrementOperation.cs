using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Serializers;
using Couchbase.IO.Converters;
using Couchbase.IO.Utils;
using Couchbase.Utils;

namespace Couchbase.IO.Operations
{
    internal sealed class DecrementOperation : OperationBase<ulong>
    {
        private const int BodyOffset = 31;
        private readonly ulong _delta;
        private readonly uint _expiration;
        private readonly ulong _initial;

       public DecrementOperation(string key, 
           ulong initial, 
           ulong delta, 
           uint expiration, 
           IVBucket vBucket, 
           IByteConverter converter, 
           ITypeSerializer2 serializer) 
           : base(key, initial, serializer, vBucket, converter)
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
            extras.ConvertAndCopy(_delta, 0, 8);
            extras.ConvertAndCopy(_initial, 8, 8);
            extras.ConvertAndCopy(_expiration, 16, 4);
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.IO.Utils;
using Couchbase.Utils;

namespace Couchbase.IO.Operations
{
    internal sealed class IncrementOperation : OperationBase<long>
    {
        private const int BodyOffset = 31;
        private readonly ulong _delta;
        private readonly uint _expiration;
        private readonly ulong _initial;

        public IncrementOperation(string key, ulong initial, ulong delta, uint expiration, IVBucket vBucket, IByteConverter converter)
            : base(key, vBucket, converter)
        {
            _delta = delta;
            _initial = initial;
            _expiration = expiration;
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Increment; }
        }

        public override ArraySegment<byte> CreateExtras()
        {
            var extras = new ArraySegment<byte>(new byte[20]);
            extras.ConvertAndCopy(_delta, 0, 8);
            extras.ConvertAndCopy(_initial, 8, 8);
            extras.ConvertAndCopy(_expiration, 16, 4);
            return extras;
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

    /*Field        (offset) (value)
    Magic        (0)    : 0x80
    Opcode       (1)    : 0x05
    Key length   (2,3)  : 0x0007
    Extra length (4)    : 0x14
    Data type    (5)    : 0x00
    VBucket      (6,7)  : 0x0000
    Total body   (8-11) : 0x0000001b
    Opaque       (12-15): 0x00000000
    CAS          (16-23): 0x0000000000000000
    Extras              :
      delta      (24-31): 0x0000000000000001
      initial    (32-39): 0x0000000000000000
      exipration (40-43): 0x00000e10
    Key                 : Textual string "counter"
    Value               : None
     * */
}

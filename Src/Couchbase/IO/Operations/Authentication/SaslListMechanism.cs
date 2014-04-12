using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations.Authentication
{
    internal sealed class SaslListMechanism : OperationBase<string>
    {
        public override OperationCode OperationCode
        {
            get { return OperationCode.SaslList; }
        }

        public override ArraySegment<byte> CreateHeader(byte[] extras, byte[] body, byte[] key)
        {
            var header = new ArraySegment<byte>(new byte[24]);
            var buffer = header.Array;

            //0 magic and 1 opcode
            buffer[0x00] = (byte)Magic.Request;
            buffer[0x01] = (byte)OperationCode;

            return header;
        }
    }
}

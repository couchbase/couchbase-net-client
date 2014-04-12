using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations.Authentication
{
    internal class SaslAuthenticate : OperationBase<string>
    {
        private readonly string _userName;
        private readonly string _passWord;

        public SaslAuthenticate(string key, string userName, string passWord) 
            : base(key, GetAuthData(userName, passWord), null)
        {
            _userName = userName;
            _passWord = passWord;
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.SaslStart; }
        }

        static string GetAuthData(string userName, string passWord)
        {
            const string empty = "\0";
            var sb = new StringBuilder();
            sb.Append(userName);
            sb.Append(empty);
            sb.Append(userName);
            sb.Append(empty);
            sb.Append(passWord);
            return sb.ToString();
        }

        public override ArraySegment<byte> CreateHeader(byte[] extras, byte[] body, byte[] key)
        {
            var header = new ArraySegment<byte>(new byte[24]);
            var buffer = header.Array;
            var totalLength = key.GetLengthSafe() +
                body.GetLengthSafe();

            //0 magic and 1 opcode
            buffer[0x00] = (byte)Magic.Request;
            buffer[0x01] = (byte)OperationCode;

            //2 & 3 Key length
            buffer[0x02] = (byte)(key.Length >> 8);
            buffer[0x03] = (byte)(key.Length & 255); 

            //8-11 total body length
            buffer[0x08] = (byte)(totalLength >> 24);
            buffer[0x09] = (byte)(totalLength >> 16);
            buffer[0x0a] = (byte)(totalLength >> 8);
            buffer[0x0b] = (byte)(totalLength & 255);

            return header;
        }

        public override List<ArraySegment<byte>> CreateBuffer()
        {
            var body = CreateBody();
            var key = CreateKey();
            var header = CreateHeader(null, body.Array, key.Array);

            return new List<ArraySegment<byte>>(4)
                {
                    header,
                    key,
                    body
                };
        }

        public override byte[] GetBuffer()
        {
            var buffer = CreateBuffer();
            var bytes = new byte[
                buffer[0].Array.GetLengthSafe() +
                buffer[1].Array.GetLengthSafe() +
                buffer[2].Array.GetLengthSafe()];

            var count = 0;
            foreach (var segment in buffer)
            {
                foreach (var b in segment.ToArray())
                {
                    bytes[count++] = b;
                }
            }
            return bytes;
        }

        /*Field (offset) (value)
            Magic (0): 0x80 (PROTOCOL_BINARY_REQ)
            Opcode (1): 0x21 (sasl auth)
            Key length (2-3): 0x0005 (5)
            Extra length (4): 0x00
            Data type (5): 0x00
            vBucket (6-7): 0x0000 (0)
            Total body (8-11): 0x00000010 (16)
            Opaque (12-15): 0x00000000 (0)
            CAS (16-23): 0x0000000000000000 (0)
            Mechanisms (24-28): PLAIN
            Auth token (29-39): foo0x00foo0x00bar
         */
    }
}

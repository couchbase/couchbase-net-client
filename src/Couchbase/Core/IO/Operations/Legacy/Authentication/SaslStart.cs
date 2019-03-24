using System;

namespace Couchbase.Core.IO.Operations.Legacy.Authentication
{
    /// <summary>
    /// Starts the SASL authentication process using a specified SASL mechanism type as a key.
    /// </summary>
    internal class SaslStart : OperationBase<string>
    {
        public override OpCode OpCode => OpCode.SaslStart;

        public override byte[] CreateExtras()
        {
            Format = DataFormat.String;
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Format,
                TypeCode = TypeCode.String
            };
            return Array.Empty<byte>();
        }

        public override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            Format = DataFormat.String;
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Format,
                TypeCode = TypeCode.String
            };
        }

        public override byte[] CreateHeader(byte[] extras, byte[] body, byte[] key, byte[] framingExtras)
        {
            var header = new byte[OperationHeader.Length];
            var totalLength = key.GetLengthSafe() + body.GetLengthSafe();

            Converter.FromByte((byte)Magic.Request, header, HeaderOffsets.Magic);
            Converter.FromByte((byte)OpCode, header, HeaderOffsets.Opcode);
            Converter.FromInt16((short)key.Length, header, HeaderOffsets.KeyLength);
            Converter.FromInt32(totalLength, header, HeaderOffsets.BodyLength);
            Converter.FromUInt32(Opaque, header, HeaderOffsets.Opaque);

            return header;
        }

        public override byte[] Write()
        {
            var body = CreateBody();
            var key = CreateKey();
            var header = CreateHeader(null, body, key, null);

            var buffer = new byte[header.GetLengthSafe() +
                key.GetLengthSafe() +
                body.GetLengthSafe()];

            System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            System.Buffer.BlockCopy(key, 0, buffer, header.Length, key.Length);
            System.Buffer.BlockCopy(body, 0, buffer, header.Length + key.Length, body.Length);

            return buffer;
        }

        public override bool RequiresKey => false;

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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion [ License information          ]

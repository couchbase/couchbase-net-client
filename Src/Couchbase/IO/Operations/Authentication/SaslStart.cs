using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.IO.Converters;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations.Authentication
{
    /// <summary>
    /// Starts the SASL authentication process using a specified SASL mechanism type as a key.
    /// </summary>
    internal class SaslStart : OperationBase<string>
    {
        /// <summary>
        /// Creates an instance of the <see cref="SaslStart"/>" object for starting the SASL authentication process.
        /// </summary>
        /// <param name="key">The SASL Mechanism to use: PLAIN or CRAM-MD5.</param>
        /// <param name="value"></param>
        /// <param name="converter">The <see cref="IByteConverter"/> to use for encoding and decoding values.</param>
        public SaslStart(string key, string value, IByteConverter converter) 
            : base(key, value, null, converter)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.SaslStart; }
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

#endregion
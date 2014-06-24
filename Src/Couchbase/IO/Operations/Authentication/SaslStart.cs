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

        public override byte[] CreateHeader(byte[] extras, byte[] body, byte[] key)
        {
            var header = new byte[24];
            var buffer = header;
            var totalLength = key.GetLengthSafe() + body.GetLengthSafe();

            Converter.FromByte((byte) Magic.Request, buffer, HeaderIndexFor.Magic);
            Converter.FromByte((byte)OperationCode, buffer, HeaderIndexFor.Opcode);
            Converter.FromInt16((short)key.Length, buffer, HeaderIndexFor.KeyLength);
            Converter.FromInt32(totalLength, buffer, HeaderIndexFor.BodyLength);

            return header;
        }

        public override byte[] GetBuffer()
        {
            var body = CreateBody();
            var key = CreateKey();
            var header = CreateHeader(null, body, key);

            var buffer = new byte[header.GetLengthSafe() + 
                key.GetLengthSafe() +
                body.GetLengthSafe()];

            Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            Buffer.BlockCopy(key, 0, buffer, header.Length, key.Length);
            Buffer.BlockCopy(body, 0, buffer, header.Length + key.Length, body.Length);

            return buffer;
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
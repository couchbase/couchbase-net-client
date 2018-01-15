using System;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
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
        ///     Creates an instance of the <see cref="SaslStart" />" object for starting the SASL authentication process.
        /// </summary>
        /// <param name="key">The SASL Mechanism to use: PLAIN or CRAM-MD5.</param>
        /// <param name="value"></param>
        /// <param name="vBucket"></param>
        /// <param name="transcoder"></param>
        /// <param name="opaque"></param>
        /// <param name="timeout"></param>
        public SaslStart(string key, string value, IVBucket vBucket, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(key, value, vBucket, transcoder, opaque, timeout)
        {
        }

        public SaslStart(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
        }

        /// <summary>
        /// Creates an instance of the <see cref="SaslStart"/>" object for starting the SASL authentication process.
        /// </summary>
        /// <param name="key">The SASL Mechanism to use: PLAIN or CRAM-MD5.</param>
        /// <param name="value"></param>
        /// <param name="transcoder"></param>
        /// <param name="timeout"></param>
        public SaslStart(string key, string value, ITypeTranscoder transcoder, uint timeout)
            : base(key, value, null, transcoder, SequenceGenerator.GetNext(), timeout)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.SaslStart; }
        }

        public override byte[] CreateExtras()
        {
            Format = DataFormat.String;
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Format,
                TypeCode = TypeCode.String
            };
            return new byte[0];
        }

        public override void ReadExtras(byte[] buffer)
        {
            Format = DataFormat.String;
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Format,
                TypeCode = TypeCode.String
            };
        }

        public override byte[] CreateHeader(byte[] extras, byte[] body, byte[] key)
        {
            var header = new byte[24];
            var totalLength = key.GetLengthSafe() + body.GetLengthSafe();

            Converter.FromByte((byte)Magic.Request, header, HeaderIndexFor.Magic);
            Converter.FromByte((byte)OperationCode, header, HeaderIndexFor.Opcode);
            Converter.FromInt16((short)key.Length, header, HeaderIndexFor.KeyLength);
            Converter.FromInt32(totalLength, header, HeaderIndexFor.BodyLength);
            Converter.FromUInt32(Opaque, header, HeaderIndexFor.Opaque);

            return header;
        }

        public override byte[] Write()
        {
            var body = CreateBody();
            var key = CreateKey();
            var header = CreateHeader(null, body, key);

            var buffer = new byte[header.GetLengthSafe() +
                key.GetLengthSafe() +
                body.GetLengthSafe()];

            System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            System.Buffer.BlockCopy(key, 0, buffer, header.Length, key.Length);
            System.Buffer.BlockCopy(body, 0, buffer, header.Length + key.Length, body.Length);

            return buffer;
        }

        public override bool RequiresKey
        {
            get { return false; }
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

#endregion [ License information          ]

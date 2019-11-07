using System;

namespace Couchbase.Core.IO.Operations.Authentication
{
    /// <summary>
    /// Starts the SASL authentication process using a specified SASL mechanism type as a key.
    /// </summary>
    internal class SaslStart : OperationBase<string>
    {
        public override OpCode OpCode => OpCode.SaslStart;

        public override void WriteExtras(OperationBuilder builder)
        {
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

        protected override void BeginSend()
        {
            Format = DataFormat.String;
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Format,
                TypeCode = TypeCode.String
            };
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

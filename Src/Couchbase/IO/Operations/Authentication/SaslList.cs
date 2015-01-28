﻿using System;
using Couchbase.IO.Converters;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations.Authentication
{
    /// <summary>
    /// Gets the supported SASL Mechanisms supported by the Couchbase Server.
    /// </summary>
    internal sealed class SaslList : OperationBase<string>
    {
        public SaslList(IByteConverter converter)
            : base(converter)
        {
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

        public override OperationCode OperationCode
        {
            get { return OperationCode.SaslList; }
        }

        public override byte[] CreateHeader(byte[] extras, byte[] body, byte[] key)
        {
            var header = new byte[24];

            Converter.FromByte((byte)Magic.Request, header, HeaderIndexFor.Magic);
            Converter.FromByte((byte)OperationCode, header, HeaderIndexFor.Opcode);

            return header;
        }
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
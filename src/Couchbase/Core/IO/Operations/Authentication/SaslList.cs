using System;

namespace Couchbase.Core.IO.Operations.Authentication
{
    /// <summary>
    /// Gets the supported SASL Mechanisms supported by the Couchbase Server.
    /// </summary>
    internal sealed class SaslList : OperationBase<string>
    {
        protected override void WriteExtras(OperationBuilder builder)
        {
        }

        protected override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Flags.DataFormat,
                TypeCode = TypeCode.String
            };

            TryReadServerDuration(buffer);
        }

        public override OpCode OpCode => OpCode.SaslList;

        protected override void BeginSend()
        {
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Flags.DataFormat,
                TypeCode = TypeCode.String
            };
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

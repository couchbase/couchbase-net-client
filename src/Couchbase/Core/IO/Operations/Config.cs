using System;
using System.Net;
using Couchbase.Core.Configuration.Server;
using Newtonsoft.Json;

namespace Couchbase.Core.IO.Operations
{
    internal sealed class Config : OperationBase<BucketConfig>
    {
        internal IPEndPoint EndPoint { get; set; }

        protected override void BeginSend()
        {
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Format,
                TypeCode = TypeCode.Object
            };
        }

        public override void WriteExtras(OperationBuilder builder)
        {
        }

        public override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            Flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = Format,
                TypeCode = TypeCode.Object
            };
        }

        public override OpCode OpCode => OpCode.GetClusterConfig;

        public override bool Idempotent { get; } = true;

        public override BucketConfig GetValue()
        {
            BucketConfig bucketConfig = null;
            if (Success && Data.Length > 0)
            {
                try
                {
                    var buffer = Data;
                    ReadExtras(buffer.Span);
                    var offset = Header.BodyOffset;
                    var length = TotalLength - Header.BodyOffset;
                    bucketConfig = Transcoder.Decode<BucketConfig>(buffer.Slice(offset, length), Flags, OpCode);
                }
                catch (Exception e)
                {
                    Exception = e;
                    HandleClientError(e.Message, ResponseStatus.ClientFailure);
                }
            }

            return bucketConfig;
        }

        public override bool RequiresKey => false;
    }
}

#region [ License information ]

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

#endregion [ License information ]

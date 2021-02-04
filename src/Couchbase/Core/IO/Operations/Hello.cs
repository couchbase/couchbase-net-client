using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Couchbase.Core.IO.Converters;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Core.IO.Operations
{
    internal class Hello : OperationBase<ServerFeatures[]>
    {
        public override OpCode OpCode => OpCode.Helo;

        protected override void WriteBody(OperationBuilder builder)
        {
            var contentLength = Content.Length;

            using (var bufferOwner = MemoryPool<byte>.Shared.Rent(contentLength * 2))
            {
                var body = bufferOwner.Memory.Span;

                for (var i = 0; i < contentLength; i++)
                {
                    ByteConverter.FromInt16((short) Content[i], body);
                    body = body.Slice(2);
                }

                builder.Write(bufferOwner.Memory.Slice(0, contentLength * 2));
            }
        }

        protected override void WriteExtras(OperationBuilder builder)
        {
        }

        public override ServerFeatures[] GetValue()
        {
            var result = default(ServerFeatures[]);
            if (GetSuccess() && Data.Length > 0)
            {
                try
                {
                    var buffer = Data.Span.Slice(Header.BodyOffset);
                    result = new ServerFeatures[Header.BodyLength/2];

                    // Other than some range checking, this is basically a straight memcpy, very fast
                    MemoryMarshal.Cast<byte, ServerFeatures>(buffer).CopyTo(result);

                    if (BitConverter.IsLittleEndian) // If statement is optimized out during JIT compilation
                    {
                        // The ServerFeature values are sent to us big endian, we need to reverse
                        for (var i = 0; i < result.Length; i++)
                        {
                            result[i] = (ServerFeatures) BinaryPrimitives.ReverseEndianness((ushort) result[i]);
                        }
                    }
                }
                catch (Exception e)
                {
                    Exception = e;
                    HandleClientError(e.Message, ResponseStatus.ClientFailure);
                }
            }
            return result;
        }
        internal static string BuildHelloKey(ulong connectionId)
        {
            var agent = ClientIdentifier.GetClientDescription();
            if (agent.Length > 200)
            {
                agent = agent.Substring(0, 200);
            }

            return JsonConvert.SerializeObject(new
            {
                i = ClientIdentifier.FormatConnectionString(connectionId),
                a = agent
            }, Formatting.None);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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

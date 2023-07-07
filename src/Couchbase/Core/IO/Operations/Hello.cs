using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Core.IO.Converters;
using Couchbase.Utils;

namespace Couchbase.Core.IO.Operations
{
    internal class Hello : OperationBase<ServerFeatures[]>
    {
        public override OpCode OpCode => OpCode.Helo;

        protected override void WriteBody(OperationBuilder builder)
        {
            var contentLength = Content.Length;
            var bufferLength = contentLength * 2;

            var buffer = ArrayPool<byte>.Shared.Rent(bufferLength);
            try
            {
                var body = buffer.AsSpan();

                for (var i = 0; i < contentLength; i++)
                {
                    ByteConverter.FromInt16((short) Content[i], body);
                    body = body.Slice(2);
                }

                builder.Write(buffer, 0, bufferLength);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
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
                    var serverFeaturesLength = (Header.BodyLength - Header.FramingExtrasLength) / 2;
                    result = new ServerFeatures[serverFeaturesLength];

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

            return JsonSerializer.Serialize(new HelloKey
            {
                Identifier = ClientIdentifier.FormatConnectionString(connectionId),
                Agent = agent
            }, InternalSerializationContext.Default.HelloKey);
        }

        public class HelloKey
        {
            [JsonPropertyName("i")] public string Identifier { get; set; }

            [JsonPropertyName("a")] public string Agent { get; set; }
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

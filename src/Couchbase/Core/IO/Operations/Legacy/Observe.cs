using System;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.Utils;

namespace Couchbase.Core.IO.Operations.Legacy
{
    internal sealed class Observe : OperationBase<ObserveState>
    {
        public override void WriteExtras(OperationBuilder builder)
        {
        }

        public override void WriteFramingExtras(OperationBuilder builder)
        {
        }

        public override byte[] CreateKey()
        {
            return Array.Empty<byte>();
        }

        public override byte[] CreateBody()
        {
            var keyLength = Converter.GetStringByteCount(Key);

            //for collections add the leb128 cid
            if (Cid.HasValue)
            {
                keyLength = keyLength + 2;
            }

            var buffer = new byte[4 + keyLength];
            // ReSharper disable once PossibleInvalidOperationException
            Converter.FromInt16(VBucketId.Value, buffer);
            Converter.FromInt16((short)keyLength, buffer.AsSpan(2));

            var keySpan = buffer.AsSpan(4);
            if (Cid.HasValue)
            {
                var leb128Length = Leb128.Write(keySpan, Cid.Value);
                Converter.FromString(Key, keySpan.Slice(leb128Length));
            }
            else
            {
                Converter.FromString(Key, keySpan);
            }

            return buffer;
        }

        public override ObserveState GetValue()
        {
            if (Success && Data.Length > 0)
            {
                try
                {
                    var buffer = Data.Span;
                    var keylength = Converter.ToInt16(buffer.Slice(26));

                    return new ObserveState
                    {
                        PersistStat = Converter.ToUInt32(buffer.Slice(16)),
                        ReplState = Converter.ToUInt32(buffer.Slice(20)),
                        VBucket = Converter.ToInt16(buffer.Slice(24)),
                        KeyLength = keylength,
                        Key = Converter.ToString(buffer.Slice(28, keylength)),
                        KeyState = (KeyState) Converter.ToByte(buffer.Slice(28 + keylength)),
                        Cas = Converter.ToUInt64(buffer.Slice(28 + keylength + 1))
                    };
                }
                catch (Exception e)
                {
                    Exception = e;
                    HandleClientError(e.Message, ResponseStatus.ClientFailure);
                }
            }
            return new ObserveState();
        }

        public override OpCode OpCode => OpCode.Observe;

        public override IOperation Clone()
        {
            var cloned = new Observe
            {
                Key = Key,
                Content = Content,
                Transcoder = Transcoder,
                VBucketId = VBucketId,
                Opaque = Opaque,
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                LastConfigRevisionTried = LastConfigRevisionTried,
                BucketName = BucketName,
                ErrorCode = ErrorCode
            };
            return cloned;
        }

        public override bool RequiresKey => true;
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

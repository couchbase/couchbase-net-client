using System;

namespace Couchbase.Core.IO.Operations.Legacy
{
    internal sealed class Observe : OperationBase<ObserveState>
    {
        public override byte[] Write()
        {
            var key = CreateKey();

            var body = new byte[4 + key.Length];
            // ReSharper disable once PossibleInvalidOperationException
            Converter.FromInt16(VBucketId.Value, body, 0);
            Converter.FromInt16((short)key.Length, body, 2);
            Buffer.BlockCopy(key, 0, body, 4, key.Length);

            var header = new byte[OperationHeader.Length];
            Converter.FromByte((byte)Magic.Request, header, HeaderOffsets.Magic);
            Converter.FromByte((byte)OpCode, header, HeaderOffsets.Opcode);
            Converter.FromInt32(body.Length, header, HeaderOffsets.BodyLength);
            Converter.FromUInt32(Opaque, header, HeaderOffsets.Opaque);

            var buffer = new byte[body.Length + header.Length];
            System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            System.Buffer.BlockCopy(body, 0, buffer, header.Length, body.Length);
            return buffer;
        }

        public override ObserveState GetValue()
        {
            if (Success && Data != null && Data.Length > 0)
            {
                try
                {
                    var buffer = Data.ToArray();
                    var keylength = Converter.ToInt16(buffer, 26);

                    return new ObserveState
                    {
                        PersistStat = Converter.ToUInt32(buffer, 16),
                        ReplState = Converter.ToUInt32(buffer, 20),
                        VBucket = Converter.ToInt16(buffer, 24),
                        KeyLength = keylength,
                        Key = Converter.ToString(buffer, 28, keylength),
                        KeyState = (KeyState) Converter.ToByte(buffer, 28 + keylength),
                        Cas = Converter.ToUInt64(buffer, 28 + keylength + 1)
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

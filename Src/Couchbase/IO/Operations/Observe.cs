using Couchbase.Core;
using Couchbase.Core.Serializers;
using Couchbase.IO.Converters;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations
{
    internal sealed class Observe : OperationBase<ObserveState>
    {
        public Observe(IByteConverter converter)
            : base(converter)
        {
        }

        public Observe(string key, IVBucket vBucket, IByteConverter converter)
            : base(key, vBucket, converter)
        {
        }

        public Observe(string key, IVBucket vBucket, IByteConverter converter, ITypeSerializer serializer)
            : base(key, vBucket, converter, serializer)
        {
        }

        public override byte[] Write()
        {
            var body = new byte[2 + 2 + Key.Length];
            Converter.FromInt16((short)VBucket.Index, body, 0);
            Converter.FromInt16((short)Key.Length, body, 2);
            Converter.FromString(Key, body, 4);

            var header = new byte[24];
            Converter.FromByte((byte)Magic.Request, header, HeaderIndexFor.Magic);
            Converter.FromByte((byte)OperationCode, header, HeaderIndexFor.Opcode);
            Converter.FromInt32(body.Length, header, HeaderIndexFor.BodyLength);
            Converter.FromInt32(Opaque, header, HeaderIndexFor.Opaque);

            var buffer = new byte[body.Length + header.Length];
            System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            System.Buffer.BlockCopy(body, 0, buffer, header.Length, body.Length);
            return buffer;
        }

        public override ObserveState GetValue()
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
                KeyState = (KeyState)Converter.ToByte(buffer, 28 + keylength),
                Cas = Converter.ToUInt64(buffer, 28 + keylength + 1)
            };
        }

        public override IOperationResult<ObserveState> GetResult()
        {
            return new OperationResult<ObserveState>(this);
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Observe; }
        }
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
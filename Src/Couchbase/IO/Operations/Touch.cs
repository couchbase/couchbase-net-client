using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations
{
    internal class Touch : OperationBase
    {
        public Touch(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
        }

        public override byte[] CreateExtras()
        {
            var extras = new byte[4];
            Converter.FromUInt32(Expires, extras, 0);
            return extras;
        }

        public override byte[] Write()
        {
            var key = CreateKey();
            var extras = CreateExtras();
            var body = new byte[0];
            var header = CreateHeader(extras, body, key);

            var buffer = new byte[header.GetLengthSafe()+key.GetLengthSafe()+extras.GetLengthSafe()];

            System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            System.Buffer.BlockCopy(extras, 0, buffer, header.Length, extras.Length);
            System.Buffer.BlockCopy(key, 0, buffer, header.Length + extras.Length, key.Length);

            return buffer;
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Touch; }
        }

        public override IOperation Clone()
        {
            var cloned = new Touch(Key, VBucket, Transcoder, Timeout)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                Expires = Expires,
                LastConfigRevisionTried = LastConfigRevisionTried,
                BucketName = BucketName,
                ErrorCode = ErrorCode
            };
            return cloned;
        }

        public override bool RequiresKey
        {
            get { return true; }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

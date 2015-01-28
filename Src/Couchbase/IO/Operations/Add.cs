﻿using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;

namespace Couchbase.IO.Operations
{
    internal sealed class Add<T> : OperationBase<T>
    {
        public Add(string key, T value, IVBucket vBucket, IByteConverter converter, ITypeTranscoder transcoder)
            : base(key, value, transcoder, vBucket, converter, SequenceGenerator.GetNext(), DefaultTimeout)
        {
        }

        private Add(string key, T value, IVBucket vBucket, IByteConverter converter, ITypeTranscoder transcoder, uint opaque)
            : base(key, value, transcoder, vBucket, converter, opaque)
        {
        }

        public Add(string key, T value, ulong cas, IVBucket vBucket, IByteConverter converter, ITypeTranscoder transcoder)
            : base(key, value, transcoder, vBucket, converter, SequenceGenerator.GetNext(), DefaultTimeout)
        {
            Cas = cas;
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Add; }
        }

        public override int BodyOffset
        {
            get { return 24; }
        }

        public override IOperation<T> Clone()
        {
            var cloned = new Add<T>(Key, RawValue, VBucket, Converter, Transcoder, Opaque)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime
            };
            return cloned;
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
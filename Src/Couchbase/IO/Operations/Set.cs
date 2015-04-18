using System.Net.NetworkInformation;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;

namespace Couchbase.IO.Operations
{
    /// <summary>
    /// Add a key to the database, replacing the key if it already exists.
    /// </summary>
    /// <typeparam name="T">The value to insert.</typeparam>
    internal sealed class Set<T> : OperationBase<T>
    {
        public Set(string key, T value, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, value, vBucket, transcoder, SequenceGenerator.GetNext(), timeout)
        {
        }

        private Set(string key, T value, ITypeTranscoder transcoder, IVBucket vBucket, uint opaque, uint timeout)
            : base(key, value, vBucket, transcoder, opaque, timeout)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Set; }
        }

        public override IOperation Clone()
        {
            var cloned = new Set<T>(Key, RawValue, Transcoder, VBucket, Opaque, Timeout)
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

#endregion
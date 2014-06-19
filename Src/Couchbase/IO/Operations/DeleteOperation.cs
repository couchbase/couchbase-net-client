using System;
using Couchbase.Core;
using Couchbase.Core.Serializers;
using Couchbase.IO.Converters;

namespace Couchbase.IO.Operations
{
    /// <summary>
    /// Removes a key from the database, failing if it doesn't exist.
    /// </summary>
    internal sealed class DeleteOperation : OperationBase<object>
    {
        public DeleteOperation(string key, IVBucket vBucket, IByteConverter converter, ITypeSerializer2 serializer)
            : base(key, vBucket, converter, serializer)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Delete; }
        }

        public override ArraySegment<byte> CreateExtras()
        {
            return new ArraySegment<byte>();
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

#endregion
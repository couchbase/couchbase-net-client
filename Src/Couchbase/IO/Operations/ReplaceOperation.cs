using Couchbase.Core;

namespace Couchbase.IO.Operations
{
    /// <summary>
    /// Replace a key in the database, failing if the key does not exist.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class ReplaceOperation<T> : OperationBase<T>
    {
        public ReplaceOperation(string key, T value, IVBucket vBucket, IByteConverter converter) 
            : base(key, value, vBucket, converter)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Replace; }
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
namespace Couchbase.Core.IO.Operations.Legacy.SubDocument
{
    /// <summary>
    /// This command removes an entry from the document. If the entry points to a dictionary key-value,
    /// the key and the value are removed from the document. If the entry points to an array element, the
    /// array element is removed, and all following elements will implicitly shift back by one. If the
    /// array element is specified as [-1] then the last element is removed.
    /// </summary>
    /// <typeparam name="T">The CLR Type representing the document.</typeparam>
    /// <seealso cref="SubDocSingularMutationBase{T}" />
    internal class SubDocDelete<T> : SubDocSingularMutationBase<T>
    {
        /// <summary>
        /// Gets the operation code for this specific operation.
        /// </summary>
        /// <value>
        /// The operation code.
        /// </value>
        public override OpCode OpCode => OpCode.SubDelete;

        /// <summary>
        /// Creates an array representing the operations body.
        /// </summary>
        /// <remarks>Sub-Document delete is always empty.</remarks>
        /// <returns></returns>
        public override byte[] CreateBody()
        {
            return new byte[0];
        }

        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns></returns>
        public override IOperation Clone()
        {
            return new SubDocDelete<T>
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                LastConfigRevisionTried = LastConfigRevisionTried,
                BucketName = BucketName,
                ErrorCode = ErrorCode
            };
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

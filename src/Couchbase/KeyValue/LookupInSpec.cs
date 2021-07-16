using System;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;

#nullable enable

namespace Couchbase.KeyValue
{
    public class LookupInSpec : OperationSpec
    {
        private static LookupInSpec CreateSpec(OpCode opCode, string path, bool isXattr = false)
        {
            var pathFlags = SubdocPathFlags.None;
            if (isXattr)
            {
                pathFlags |= SubdocPathFlags.Xattr;
            }

            return new LookupInSpec
            {
                Path = path,
                OpCode = opCode,
                PathFlags = pathFlags
            };
        }

        public static LookupInSpec Get(string path, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubGet, path, isXattr);
        }

        public static LookupInSpec Exists(string path, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubExist, path, isXattr);
        }

        public static LookupInSpec Count(string path, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubGetCount, path, isXattr);
        }

        public static LookupInSpec GetFull()
        {
            return CreateSpec(OpCode.Get, "");
        }

        /// <summary>
        /// Creates a new object that is a copy of the current instance excluding the Byte and Status fields.
        /// </summary>
        /// <returns>
        /// A new object that is a copy of this instance.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        internal OperationSpec Clone()
        {
            return new LookupInSpec
            {
                Bytes = ReadOnlyMemory<byte>.Empty,
                PathFlags = PathFlags,
                DocFlags = DocFlags,
                OpCode = OpCode,
                Path = Path,
                RemoveBrackets = RemoveBrackets,
                Status = ResponseStatus.None,
                Value = Value,
            };
        }

    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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

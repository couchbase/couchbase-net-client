using System;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;

#nullable enable

namespace Couchbase.KeyValue
{
    public class MutateInSpec : OperationSpec
    {
        private static MutateInSpec CreateSpec(OpCode opCode, string path, object? value, bool createPath, bool isXattr, bool removeBrackets)
        {
            var pathFlags = SubdocPathFlags.None;
            if (value is IMutationMacro)
            {
                pathFlags |= SubdocPathFlags.ExpandMacroValues;
                pathFlags |= SubdocPathFlags.Xattr;
                value = value.ToString();
            }
            if (createPath)
            {
                pathFlags |= SubdocPathFlags.CreatePath;
            }
            if (isXattr)
            {
                pathFlags |= SubdocPathFlags.Xattr;
            }

            return new MutateInSpec
            {
                Path = path,
                Value = value,
                OpCode = opCode,
                PathFlags = pathFlags,
                RemoveBrackets = removeBrackets
            };
        }

        public static MutateInSpec Insert<T>(string path, T value, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubDictAdd, path, value, createPath, isXattr, removeBrackets);
        }

        public static MutateInSpec Upsert<T>(string path, T value, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubDictUpsert, path, value, createPath, isXattr, removeBrackets);
        }

        public static MutateInSpec Replace<T>(string path, T value, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubReplace, path, value, false, isXattr, removeBrackets);
        }

        public static MutateInSpec SetDoc<T>(T value)
        {
            return CreateSpec(OpCode.Set, "", value, false, false, false);
        }

        public static MutateInSpec Remove(string path, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubDelete, path, null, false, isXattr, removeBrackets);
        }

        public static MutateInSpec ArrayAppend<T>(string path, T[] values, bool createPath = false, bool isXattr = false, bool removeBrackets = true)
        {
            return CreateSpec(OpCode.SubArrayPushLast, path, values, createPath, isXattr, removeBrackets);
        }

        public static MutateInSpec ArrayAppend<T>(string path, T value, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubArrayPushLast, path, value, createPath, isXattr, removeBrackets);
        }

        public static MutateInSpec ArrayPrepend<T>(string path, T value, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubArrayPushFirst, path, value, createPath, isXattr, removeBrackets);
        }

        public static MutateInSpec ArrayPrepend<T>(string path, T[] values, bool createPath = false, bool isXattr = false, bool removeBrackets = true)
        {
            return CreateSpec(OpCode.SubArrayPushFirst, path, values, createPath, isXattr, removeBrackets);
        }

        public static MutateInSpec ArrayInsert<T>(string path, T value, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubArrayInsert, path, value, createPath, isXattr, removeBrackets);
        }

        public static MutateInSpec ArrayInsert<T>(string path, T[] values, bool createPath = false, bool isXattr = false, bool removeBrackets = true)
        {
            return CreateSpec(OpCode.SubArrayInsert, path, values, createPath, isXattr, removeBrackets);
        }

        public static MutateInSpec ArrayAddUnique<T>(string path, T value, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubArrayAddUnique, path, value, createPath, isXattr, removeBrackets);
        }

        [Obsolete("Use the Increment overload which accepts an unsigned long.")]
        public static MutateInSpec Increment(string path, long delta, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubCounter, path, delta, createPath, isXattr, removeBrackets);
        }

        public static MutateInSpec Increment(string path, ulong delta, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubCounter, path, delta, createPath, isXattr, removeBrackets);
        }

        [Obsolete("Use the Decrement overload which accepts an unsigned long. Negative signed long deltas may produce unexpected results.")]
        public static MutateInSpec Decrement(string path, long delta, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubCounter, path, delta, createPath, isXattr, removeBrackets);
        }

        public static MutateInSpec Decrement(string path, ulong delta, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubCounter, path, -(long)delta, createPath, isXattr, removeBrackets);
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
            return new MutateInSpec
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

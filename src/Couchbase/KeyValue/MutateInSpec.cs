using System;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue
{
    public class MutateInSpec : OperationSpec
    {
        [Obsolete("Use MutateInSpec static factory methods.")]
        public MutateInSpec()
        {
        }

        internal MutateInSpec(OpCode opCode, string path)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (path is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }

            OpCode = opCode;
            Path = path;
        }

        private static MutateInSpec CreateSpec(OpCode opCode, string path, bool createPath, bool isXattr, bool removeBrackets)
        {
            var pathFlags = SubdocPathFlags.None;
            if (createPath)
            {
                pathFlags |= SubdocPathFlags.CreatePath;
            }
            if (isXattr)
            {
                pathFlags |= SubdocPathFlags.Xattr;
            }

            return new MutateInSpec(opCode, path)
            {
                PathFlags = pathFlags,
                RemoveBrackets = removeBrackets
            };
        }

        private static MutateInSpec CreateSpec<T>(OpCode opCode, string path, T value, bool createPath, bool isXattr, bool removeBrackets)
        {
            var pathFlags = SubdocPathFlags.None;
            if (createPath)
            {
                pathFlags |= SubdocPathFlags.CreatePath;
            }
            if (isXattr)
            {
                pathFlags |= SubdocPathFlags.Xattr;
            }

            if (value is IMutationMacro)
            {
                return new MutateInSpec<string>(opCode, path, value.ToString()!)
                {
                    PathFlags = pathFlags | SubdocPathFlags.ExpandMacroValues | SubdocPathFlags.Xattr,
                    RemoveBrackets = removeBrackets
                };
            }

            return new MutateInSpec<T>(opCode, path, value)
            {
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
            return path == "" ? SetDoc(value) : CreateSpec(OpCode.SubReplace, path, value, false, isXattr, removeBrackets);
        }

        public static MutateInSpec SetDoc<T>(T value)
        {
            return CreateSpec(OpCode.Set, "", value, false, false, false);
        }

        public static MutateInSpec Remove(string path, bool isXattr = false, bool removeBrackets = false)
        {
            return path == "" ? CreateSpec(OpCode.Delete, path, false, isXattr, false) : CreateSpec(OpCode.SubDelete, path, false, isXattr, removeBrackets);
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

        public static MutateInSpec ReplaceBodyWithXattr(string path)
        {
            return CreateSpec(OpCode.SubReplaceBodyWithXattr, path, false, true, false);
        }

        /// <summary>
        /// Serializes the <see cref="OperationSpec.Value" /> to the <see cref="OperationBuilder"/> using the <see cref="ITypeTranscoder"/>.
        /// </summary>
        /// <param name="builder">Builder to serialize to.</param>
        /// <param name="transcoder">Transcoder to use.</param>
        internal virtual void WriteSpecValue(OperationBuilder builder, ITypeTranscoder transcoder)
        {
            // This is legacy backward-compatibility behavior. If the MutateInSpec is constructed using the static
            // methods above a MutateInSpec<T> will be created, or it is a SubDelete which doesn't have a body,
            // in both cases this method is will not be called. However, it is possible that consumers
            // are creating MutateInSpec objects directly and passing them to MutateInAsync, in which case this method
            // will be called, so we retain the legacy behavior.

            if (!RemoveBrackets)
            {
                // We can serialize directly
                transcoder.Serializer!.Serialize(builder, Value!);
            }
            else
            {
                using var stream = MemoryStreamFactory.GetMemoryStream();
                transcoder.Serializer!.Serialize(stream, Value!);

                ReadOnlyMemory<byte> bytes = stream.GetBuffer().AsMemory(0, (int) stream.Length);
                bytes = bytes.StripBrackets();

                builder.Write(bytes);
            }
        }

        /// <summary>
        /// Creates a new object that is a copy of the current instance excluding the Byte and Status fields.
        /// </summary>
        /// <returns>
        /// A new object that is a copy of this instance.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        internal virtual OperationSpec Clone()
        {
            return new MutateInSpec(OpCode, Path)
            {
                Bytes = ReadOnlyMemory<byte>.Empty,
                PathFlags = PathFlags,
                DocFlags = DocFlags,
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

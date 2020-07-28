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

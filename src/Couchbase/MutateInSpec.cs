using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;

namespace Couchbase
{
    public static class MutateInSpec
    {
        private static OperationSpec CreateSpec(OpCode opCode, string path, object value, bool createPath, bool isXattr)
        {
            var pathFlags = SubdocPathFlags.None;
            if (createPath)
            {
                pathFlags ^= SubdocPathFlags.CreatePath;
            }
            if (isXattr)
            {
                pathFlags ^= SubdocPathFlags.Xattr;
            }

            return new OperationSpec
            {
                Path = path,
                Value = value,
                OpCode = opCode,
                PathFlags = pathFlags
            };
        }

        public static OperationSpec Insert<T>(string path, T value, bool createPath = false, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubGet, path, value, createPath, isXattr);
        }

        public static OperationSpec Upsert<T>(string path, T value, bool createPath = false, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubDictUpsert, path, value, createPath, isXattr);
        }

        public static OperationSpec Replace<T>(string path, T value, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubReplace, path, value, false, isXattr);
        }

        public static OperationSpec Remove(string path, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubDelete, path, null, false, isXattr);
        }

        public static OperationSpec ArrayAppend<T>(string path, T[] values, bool createPath = false, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubArrayPushLast, path, values, createPath, isXattr);
        }

        public static OperationSpec ArrayPrepend<T>(string path, T[] values, bool createPath = false, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubArrayPushFirst, path, values, createPath, isXattr);
        }

        public static OperationSpec ArrayInsert<T>(string path, T[] values, bool createPath = false, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubArrayInsert, path, values, createPath, isXattr);
        }

        public static OperationSpec ArrayAddUnique<T>(string path, T value, bool createPath = false, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubArrayAddUnique, path, value, createPath, isXattr);
        }

        public static OperationSpec Increment(string path, long delta, bool createPath = false, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubCounter, path, delta, createPath, isXattr);
        }

        public static OperationSpec Decrement(string path, long delta, bool createPath = false, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubCounter, path, delta, createPath, isXattr);
        }
    }
}

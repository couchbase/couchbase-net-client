using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;

namespace Couchbase.KeyValue
{
    public static class MutateInSpec
    {
        private static OperationSpec CreateSpec(OpCode opCode, string path, object value, bool createPath, bool isXattr, bool removeBrackets)
        {
            var pathFlags = SubdocPathFlags.None;
            if (value is IMutationMacro)
            {
                pathFlags ^= SubdocPathFlags.ExpandMacroValues;
                pathFlags ^=SubdocPathFlags.Xattr;
                value = value.ToString();
            }
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
                PathFlags = pathFlags,
                RemoveBrackets = removeBrackets
            };
        }

        public static OperationSpec Insert<T>(string path, T value, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubDictAdd, path, value, createPath, isXattr, removeBrackets);
        }

        public static OperationSpec Upsert<T>(string path, T value, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubDictUpsert, path, value, createPath, isXattr, removeBrackets);
        }

        public static OperationSpec Replace<T>(string path, T value, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubReplace, path, value, false, isXattr, removeBrackets);
        }

        public static OperationSpec Remove(string path, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubDelete, path, null, false, isXattr, removeBrackets);
        }

        public static OperationSpec ArrayAppend<T>(string path, T[] values, bool createPath = false, bool isXattr = false, bool removeBrackets = true)
        {
            return CreateSpec(OpCode.SubArrayPushLast, path, values, createPath, isXattr, removeBrackets);
        }

        public static OperationSpec ArrayAppend<T>(string path, T value, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubArrayPushLast, path, value, createPath, isXattr, removeBrackets);
        }

        public static OperationSpec ArrayPrepend<T>(string path, T value, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubArrayPushFirst, path, value, createPath, isXattr, removeBrackets);
        }

        public static OperationSpec ArrayPrepend<T>(string path, T[] values, bool createPath = false, bool isXattr = false, bool removeBrackets = true)
        {
            return CreateSpec(OpCode.SubArrayPushFirst, path, values, createPath, isXattr, removeBrackets);
        }

        public static OperationSpec ArrayInsert<T>(string path, T value, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubArrayInsert, path, value, createPath, isXattr, removeBrackets);
        }

        public static OperationSpec ArrayInsert<T>(string path, T[] values, bool createPath = false, bool isXattr = false, bool removeBrackets = true)
        {
            return CreateSpec(OpCode.SubArrayInsert, path, values, createPath, isXattr, removeBrackets);
        }

        public static OperationSpec ArrayAddUnique<T>(string path, T value, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubArrayAddUnique, path, value, createPath, isXattr, removeBrackets);
        }

        public static OperationSpec Increment(string path, long delta, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubCounter, path, delta, createPath, isXattr, removeBrackets);
        }

        public static OperationSpec Decrement(string path, long delta, bool createPath = false, bool isXattr = false, bool removeBrackets = false)
        {
            return CreateSpec(OpCode.SubCounter, path, delta, createPath, isXattr, removeBrackets);
        }
    }
}

using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;

namespace Couchbase
{
    public class LookupInSpec
    {
        private static OperationSpec CreateSpec(OpCode opCode, string path, bool isXattr = false)
        {
            var pathFlags = SubdocPathFlags.None;
            if (isXattr)
            {
                pathFlags ^= SubdocPathFlags.Xattr;
            }

            return new OperationSpec
            {
                Path = path,
                OpCode = opCode,
                PathFlags = pathFlags
            };
        }

        public static OperationSpec Get(string path, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubGet, path, isXattr);
        }

        public static OperationSpec Exists(string path, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubExist, path, isXattr);
        }

        public static OperationSpec Count(string path, bool isXattr = false)
        {
            return CreateSpec(OpCode.SubGetCount, path, isXattr);
        }
    }
}

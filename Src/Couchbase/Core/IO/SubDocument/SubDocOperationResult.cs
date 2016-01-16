using Couchbase.IO;
using Couchbase.IO.Operations;

namespace Couchbase.Core.IO.SubDocument
{
    internal class SubDocOperationResult
    {
        public SubDocOperationResult()
        {
            Status = ResponseStatus.None;
        }

        public string Path { get; set; }

        public OperationCode OpCode { get; set; }

        public object Value { get; set; }

        public bool CreateParents { get; set; }

        public ResponseStatus Status { get; set; }
    }
}

using Couchbase.Core.IO.Operations;

namespace Couchbase.Core.Exceptions.KeyValue
{
    public class SubdocExceptionException : CouchbaseException
    {
        public int? SubDocumentErrorIndex { get; internal set; }
        public virtual ResponseStatus SubDocumentStatus { get; internal set; } = ResponseStatus.SubDocMultiPathFailure;
    }
}

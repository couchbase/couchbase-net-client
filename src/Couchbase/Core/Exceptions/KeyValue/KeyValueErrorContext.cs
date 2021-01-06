using Couchbase.Core.IO.Operations;

namespace Couchbase.Core.Exceptions.KeyValue
{
    /// <remarks>Uncommitted</remarks>
    public class KeyValueErrorContext : IErrorContext
    {
        public string DispatchedFrom { get; internal set; } //state.localendpoint

        public string DispatchedTo { get; internal set; } //state.endpoint

        public string DocumentKey { get; internal set; } //op.Id

        public string ClientContextId { get; internal set; } //state.opaque||op.opaque

        public ulong Cas { get; internal set; } //op.Cas

        public ResponseStatus Status { get; internal set; } //state.Status

        public string BucketName { get; internal set; } //collection.Bucket.BucketName

        public string CollectionName { get; internal set; }//collecton.Name

        public string ScopeName { get; internal set; }//scope.name

        public string Message { get; internal set; } //errorcode

        internal OpCode OpCode { get; set; }
    }
}

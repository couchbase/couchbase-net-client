using Couchbase.Core;
using Couchbase.Core.Transcoders;

namespace Couchbase.IO.Operations.SubDocument
{
    /// <summary>
    /// This command removes an entry from the document. If the entry points to a dictionary key-value,
    /// the key and the value are removed from the document. If the entry points to an array element, the
    /// array element is removed, and all following elements will implicitly shift back by one. If the
    /// array element is specified as [-1] then the last element is removed.
    /// </summary>
    /// <typeparam name="T">The CLR Type representing the document.</typeparam>
    /// <seealso cref="Couchbase.IO.Operations.SubDocument.SubDocSingularMutationBase{T}" />
    internal class SubDocDelete<T> : SubDocSingularMutationBase<T>
    {
        public SubDocDelete(MutateInBuilder<T> builder, string key, T value, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(builder, key, value, vBucket, transcoder, SequenceGenerator.GetNext(), timeout)
        {
            CurrentSpec = builder.FirstSpec();
            Path = CurrentSpec.Path;
        }

        /// <summary>
        /// Gets the operation code for this specific operation.
        /// </summary>
        /// <value>
        /// The operation code.
        /// </value>
        public override OperationCode OperationCode
        {
            get { return OperationCode.SubDelete; }
        }

        /// <summary>
        /// Creates an array representing the operations body.
        /// </summary>
        /// <remarks>Sub-Document delete is always empty.</remarks>
        /// <returns></returns>
        public override byte[] CreateBody()
        {
            return new byte[0];
        }
    }
}

using Couchbase.IO;
using Couchbase.IO.Operations;

namespace Couchbase.Core.IO.SubDocument
{
    /// <summary>
    /// Represents a single operation within a mult-operation against a document using the SubDocument API.
    /// </summary>
    internal class OperationSpec
    {
        public OperationSpec()
        {
            Status = ResponseStatus.None;
        }

        /// <summary>
        /// Gets or sets the N1QL path within the document.
        /// </summary>
        /// <value>
        /// The path.
        /// </value>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="OperationCode"/> for the SubDocument operation.
        /// </summary>
        /// <value>
        /// The op code.
        /// </value>
        public OperationCode OpCode { get; set; }

        /// <summary>
        /// Gets or sets the value that will be written or recieved. This can be a JSON fragment or a scalar.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        public object Value { get; set; }

        /// <summary>
        /// Gets or sets the bytes.
        /// </summary>
        /// <value>
        /// The bytes.
        /// </value>
        public byte[] Bytes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to create the parent element if it doesn't exist.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [create parents]; otherwise, <c>false</c>.
        /// </value>
        public bool CreateParents { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ResponseStatus"/> returned by the server indicating the status of the operation - i.e. failed, succeeded, etc.
        /// </summary>
        /// <value>
        /// The status.
        /// </value>
        public ResponseStatus Status { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not to remove array brackets.
        /// </summary>
        /// <value>
        ///   <c>true</c> if array brackets will be removed; otherwise, <c>false</c>.
        /// </value>
        public bool RemoveBrackets { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the value is valid JSON and not just an element's value.
        /// </summary>
        /// <value>
        ///   <c>true</c> if value is JSON; otherwise, <c>false</c>.
        /// </value>
        public bool ValueIsJson { get; set; }
    }
}

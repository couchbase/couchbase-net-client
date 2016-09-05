using System;
using System.Collections;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Newtonsoft.Json;

namespace Couchbase.Core.IO.SubDocument
{
    /// <summary>
    /// Represents a single operation within a mult-operation against a document using the SubDocument API.
    /// </summary>
    internal class OperationSpec : IEqualityComparer
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

        /// <summary>
        /// Creates a new object that is a copy of the current instance excluding the Byte and Status fields.
        /// </summary>
        /// <returns>
        /// A new object that is a copy of this instance.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public OperationSpec Clone()
        {
            return new OperationSpec
            {
                Bytes = null,
                CreateParents = CreateParents,
                OpCode = OpCode,
                Path = Path,
                RemoveBrackets = RemoveBrackets,
                Status = ResponseStatus.None,
                Value = Value,
            };
        }

        /// <summary>
        /// Determines whether the specified <see cref="OperationSpec" />, is equal to this instance. Only compares Path and OpCode!
        /// </summary>
        /// <param name="obj">The <see cref="OperationSpec" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="OperationSpec" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            var spec = obj as OperationSpec;
            if (spec == null) return false;
            return Path == spec.Path && OpCode == spec.OpCode;
        }

        /// <summary>
        /// Determines whether the specified <see cref="OperationSpec" />, is equal to this instance. Only compares Path and OpCode!
        /// </summary>
        /// <param name="x">The <see cref="OperationSpec" /> to compare with this instance.</param>
        /// <param name="y">The y.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="OperationSpec" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public new bool Equals(object x, object y)
        {
            var spec1 = x as OperationSpec;
            var spec2 = y as OperationSpec;
            if (spec1 == null || spec2 == null) return false;
            return spec1.Path == spec2.Path && spec1.OpCode == spec2.OpCode;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
        /// </returns>
        public int GetHashCode(object obj)
        {
            var spec = obj as OperationSpec;
            if (spec == null) return 0;
            var hash = 17;
            hash = hash*23 + (spec.Path == null ? 0 : spec.Path.GetHashCode());
            hash = hash*23 + spec.OpCode.GetHashCode();
            return hash;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
        /// </returns>
        public override int GetHashCode()
        {
            return GetHashCode(this);
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}

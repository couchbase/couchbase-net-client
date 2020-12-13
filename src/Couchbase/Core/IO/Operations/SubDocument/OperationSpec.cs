using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Couchbase.KeyValue;
using Couchbase.Utils;
using Newtonsoft.Json;

#nullable enable

namespace Couchbase.Core.IO.Operations.SubDocument
{
    /// <summary>
    /// Represents a single operation within a multi-operation against a document using the SubDocument API.
    /// </summary>
    public abstract class OperationSpec : IEqualityComparer
    {
        private string _path = "";

        /// <summary>
        /// Maximum length of the path, in bytes.
        /// </summary>
        internal const int MaxPathLength = 1024;

        /// <summary>
        /// Gets or sets the N1QL path within the document.
        /// </summary>
        /// <value>
        /// The path.
        /// </value>
        internal string Path
        {
            get => _path;
            set
            {
                if (value == null)
                {
                    ThrowHelper.ThrowArgumentNullException(nameof(value));
                }

                _path = value;
            }
        }

        /// <summary>
        /// Gets the original index in the spec list.
        /// </summary>
        internal int? OriginalIndex { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="OpCode"/> for the SubDocument operation.
        /// </summary>
        /// <value>
        /// The op code.
        /// </value>
        internal OpCode OpCode { get; set; }

        /// <summary>
        /// Gets or sets the value that will be written or received. This can be a JSON fragment or a scalar.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        internal object? Value { get; set; }

        /// <summary>
        /// Gets or sets the bytes.
        /// </summary>
        /// <value>
        /// The bytes.
        /// </value>
        internal ReadOnlyMemory<byte> Bytes { get; set; }

        /// <summary>
        /// Gets or sets the path flags for the operation.
        /// </summary>
        /// <value>
        /// The flags.
        /// </value>
        internal SubdocPathFlags PathFlags { get; set; }

        /// <summary>
        /// Gets or sets the document flags for the operation.
        /// </summary>
        /// <value>
        /// The flags.
        /// </value>
        internal SubdocDocFlags DocFlags { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ResponseStatus"/> returned by the server indicating the status of the operation - i.e. failed, succeeded, etc.
        /// </summary>
        /// <value>
        /// The status.
        /// </value>
        internal ResponseStatus Status { get; set; } = ResponseStatus.None;

        /// <summary>
        /// Gets or sets a value indicating whether or not to remove array brackets.
        /// </summary>
        /// <value>
        ///   <c>true</c> if array brackets will be removed; otherwise, <c>false</c>.
        /// </value>
        internal bool RemoveBrackets { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the value is valid JSON and not just an element's value.
        /// </summary>
        /// <value>
        ///   <c>true</c> if value is JSON; otherwise, <c>false</c>.
        /// </value>
        internal bool ValueIsJson { get; set; }

        /// <summary>
        /// Determines whether the specified <see cref="OperationSpec" />, is equal to this instance. Only compares Path and OpCode!
        /// </summary>
        /// <param name="obj">The <see cref="OperationSpec" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="OperationSpec" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object? obj)
        {
            if (!(obj is OperationSpec spec)) return false;
            return Path == spec.Path &&
                   OpCode == spec.OpCode &&
                   PathFlags == spec.PathFlags &&
                   DocFlags == spec.DocFlags;
        }

        /// <summary>
        /// Determines whether the specified <see cref="OperationSpec" />, is equal to this instance. Only compares Path and OpCode!
        /// </summary>
        /// <param name="x">The <see cref="OperationSpec" /> to compare with this instance.</param>
        /// <param name="y">The y.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="OperationSpec" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public new bool Equals(object? x, object? y)
        {
            if (!(x is OperationSpec spec1) || !(y is OperationSpec spec2)) return false;
            return spec1.Path == spec2.Path &&
                   spec1.OpCode == spec2.OpCode &&
                   spec1.PathFlags == spec2.PathFlags &&
                   spec1.DocFlags == spec2.DocFlags;
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
            if (!(obj is OperationSpec spec)) return 0;
            var hash = 17;
            hash = hash*23 + (spec.Path == null ? 0 : spec.Path.GetHashCode());
            hash = hash*23 + spec.OpCode.GetHashCode();
            hash = hash*23 + PathFlags.GetHashCode();
            hash = hash*23 + DocFlags.GetHashCode();
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

        internal static XattrFirstComparer ByXattr { get; } = new XattrFirstComparer();

        internal class XattrFirstComparer : IComparer<OperationSpec>
        {
            public int Compare([AllowNull] OperationSpec a, [AllowNull] OperationSpec b)
            {
                if (a == null && b == null)
                {
                    return 0;
                }

                if (a == null)
                {
                    return 1;
                }

                if (b == null)
                {
                    return -1;
                }

                var aIsXattr = a.PathFlags.HasFlag(SubdocPathFlags.Xattr);
                var bIsXattr = b.PathFlags.HasFlag(SubdocPathFlags.Xattr);
                if (aIsXattr && bIsXattr)
                {
                    return 0;
                }

                if (aIsXattr)
                {
                    return -1;
                }

                if (bIsXattr)
                {
                    return 1;
                }

                return 0;
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion

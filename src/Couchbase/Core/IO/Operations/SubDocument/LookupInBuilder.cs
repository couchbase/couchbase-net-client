using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
using Couchbase.KeyValue;
using Couchbase.Utils;

namespace Couchbase.Core.IO.Operations.SubDocument
{
    /// <summary>
    /// An implementation of <see cref="ILookupInBuilder{TDocument}"/> that exposes a "builder" API for constructing a chain of read commands on a document within Couchbase.
    /// </summary>
    /// <typeparam name="TDocument">The type of the document.</typeparam>
    /// <seealso cref="ILookupInBuilder{TDocument}" />
    /// <seealso cref="ITypeSerializerProvider" />
    public class LookupInBuilder<TDocument> : ILookupInBuilder<TDocument>, IEnumerable<OperationSpec>, IEquatable<LookupInBuilder<TDocument>>
    {
        private readonly ISubdocInvoker _invoker;
        private readonly ConcurrentQueue<OperationSpec> _commands = new ConcurrentQueue<OperationSpec>();
        private readonly Func<ITypeSerializer> _serializer;
        private ITypeSerializer _cachedSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="LookupInBuilder{TDocument}"/> class.
        /// </summary>
        /// <param name="invoker">The invoker.</param>
        /// <param name="serializer">The serializer.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException"> invoker or serializer or key.
        /// </exception>
        internal LookupInBuilder(ISubdocInvoker invoker, Func<ITypeSerializer> serializer, string key)
        {
            if (key == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(key));
            }

            _invoker = invoker;
            _serializer = serializer;
            Key = key;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LookupInBuilder{TDocument}"/> class.
        /// </summary>
        /// <param name="invoker">The invoker.</param>
        /// <param name="serializer">The serializer.</param>
        /// <param name="key">The key.</param>
        /// <param name="specs">The specs.</param>
        internal LookupInBuilder(ISubdocInvoker invoker, Func<ITypeSerializer> serializer, string key, IEnumerable<OperationSpec> specs)
            : this(invoker, serializer, key)
        {
            _commands = new ConcurrentQueue<OperationSpec>(specs);
        }

        /// <summary>
        /// Gets the <see cref="ITypeSerializer" /> related to the object.
        /// </summary>
        public ITypeSerializer Serializer => _cachedSerializer ??= _serializer.Invoke();

        /// <summary>
        /// Gets or sets the unique identifier for the document.
        /// </summary>
        /// <value>
        /// The key.
        /// </value>
        public string Key { get; internal set; }

        /// <summary>
        /// Returns a count of the currently chained operations.
        /// </summary>
        /// <returns>A <see cref="int"/> representing the number of chained commands.</returns>
        public int Count => _commands.Count;

        /// <summary>
        /// The maximum time allowed for an operation to live before timing out.
        /// </summary>
        public TimeSpan? Timeout { get; private set; }

        /// <summary>
        /// Gets a value indicating whether any of the pending commands target an XATTR.
        /// </summary>
        /// <value>
        /// <c>true</c> if any pending command targers an XATTR; otherwise, <c>false</c>.
        /// </value>
        internal bool ContainsXattrOperations
        {
            get { return _commands.Any(command => command.PathFlags.HasFlag(SubdocPathFlags.Xattr)); }
        }

        /// <summary>
        /// Gets the value at a specified N1QL path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.Core.ILookupInBuilder`1" /> implementation reference for chaining operations.
        /// </returns>
        public ILookupInBuilder<TDocument> Get(string path)
        {
            return Get(path, SubdocPathFlags.None);
        }

        /// <summary>
        /// Gets the value at a specified path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="pathFlags">The Subdoc pathFlags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.Core.ILookupInBuilder`1" /> implementation reference for chaining operations.
        /// </returns>
        public ILookupInBuilder<TDocument> Get(string path, SubdocPathFlags pathFlags, SubdocDocFlags docFlags = SubdocDocFlags.None)
        {
            _commands.Enqueue(new LookupInSpec
            {
                Path = path,
                OpCode = OpCode.SubGet,
                PathFlags = pathFlags,
                DocFlags = docFlags
            });
            return this;
        }

        /// <summary>
        /// Checks for the existence of a given N1QL path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.Core.ILookupInBuilder`1" /> implementation reference for chaining operations.
        /// </returns>
        public ILookupInBuilder<TDocument> Exists(string path)
        {
            return Exists(path, SubdocPathFlags.None);
        }

        /// <summary>
        /// Checks for the existence of a given N1QL path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="pathFlags">The Subdoc pathFlags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.Core.ILookupInBuilder`1" /> implementation reference for chaining operations.
        /// </returns>
        public ILookupInBuilder<TDocument> Exists(string path, SubdocPathFlags pathFlags, SubdocDocFlags docFlags = SubdocDocFlags.None)
        {
            _commands.Enqueue(new LookupInSpec
            {
                Path = path,
                OpCode = OpCode.SubExist,
                PathFlags = pathFlags,
                DocFlags = docFlags
            });
            return this;
        }

        /// <summary>
        /// Gets the number of items in a collection or dictionary at a specified N1QL path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>A <see cref="ILookupInBuilder{TDocument}"/> implementation reference for chaining operations.</returns>
        /// <remarks>Requires Couchbase Server 5.0 or higher</remarks>
        public ILookupInBuilder<TDocument> GetCount(string path)
        {
            return GetCount(path, SubdocPathFlags.None);
        }

        /// <summary>
        /// Gets the number of items in a collection or dictionary at a specified N1QL path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="pathFlags">The subdocument lookup pathFlags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <returns>A <see cref="ILookupInBuilder{TDocument}"/> implementation reference for chaining operations.</returns>
        /// <remarks>Requires Couchbase Server 5.0 or higher</remarks>
        public ILookupInBuilder<TDocument> GetCount(string path, SubdocPathFlags pathFlags, SubdocDocFlags docFlags = SubdocDocFlags.None)
        {
            _commands.Enqueue(new LookupInSpec
            {
                Path = path,
                OpCode = OpCode.SubGetCount,
                PathFlags = pathFlags,
                DocFlags = docFlags
            });
            return this;
        }

        /// <summary>
        /// Executes the chained operations.
        /// </summary>
        /// <returns>
        /// A <see cref="T:Couchbase.IDocumentFragment`1" /> representing the results of the chained operations.
        /// </returns>
        public IDocumentFragment<TDocument> Execute()
        {
            return _invoker.Invoke(this);
        }

        /// <summary>
        /// Executes the chained operations.
        /// </summary>
        /// <returns>
        /// A <see cref="T:Couchbase.IDocumentFragment`1" /> representing the results of the chained operations.
        /// </returns>
        public Task<IDocumentFragment<TDocument>> ExecuteAsync()
        {
            return _invoker.InvokeAsync(this);
        }

        /// <summary>
        /// Gets the enumerator for enumerating the list of <see cref="OperationSpec"/>s.
        /// </summary>
        /// <returns></returns>
        IEnumerator<OperationSpec> IEnumerable<OperationSpec>.GetEnumerator()
        {
            return _commands.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<OperationSpec>) this).GetEnumerator();
        }

        /// <summary>
        /// Gets the <see cref="OperationSpec"/> in the first position.
        /// </summary>
        /// <returns></returns>
        internal OperationSpec FirstSpec()
        {
            if (_commands.TryPeek(out var command))
            {
                return command;
            }
            return command;
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        public bool Equals(LookupInBuilder<TDocument> other)
        {
            if (other == null) return false;
            if (_commands.ToArray().AreEqual<OperationSpec>(other._commands.ToArray())
                && Key == other.Key)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// The maximum time allowed for an operation to live before timing out.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns>An <see cref="ILookupInBuilder{TDocument}"/> reference for chaining operations.</returns>
        public ILookupInBuilder<TDocument> WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
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

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Serialization;
using Couchbase.IO.Operations;
using Couchbase.Utils;

namespace Couchbase.Core
{
    /// <summary>
    /// An implementation of <see cref="ILookupInBuilder{TDocument}"/> that exposes a "builder" API for constructing a chain of read commands on a document within Couchbase.
    /// </summary>
    /// <typeparam name="TDocument">The type of the document.</typeparam>
    /// <seealso cref="Couchbase.Core.ILookupInBuilder{TDocument}" />
    /// <seealso cref="Couchbase.Core.Serialization.ITypeSerializerProvider" />
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
            if (invoker == null)
            {
                throw new ArgumentNullException("invoker");
            }
            if (serializer == null)
            {
                throw new ArgumentNullException("serializer");
            }
            if (key == null)
            {
                throw new ArgumentNullException("key");
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
        public ITypeSerializer Serializer
        {
            get
            {
                if (_cachedSerializer == null)
                {
                    _cachedSerializer = _serializer.Invoke();
                }

                return _cachedSerializer;
            }
        }

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
        public int Count
        {
            get { return _commands.Count; }
        }

        /// <summary>
        /// Gets a value indicating whether any of the pending commands target an XATTR.
        /// </summary>
        /// <value>
        /// <c>true</c> if any pending command targers an XATTR; otherwise, <c>false</c>.
        /// </value>
        internal bool ContainsXattrOperations
        {
            get { return _commands.Any(x => (x.Flags & (byte) SubdocLookupFlags.XattrPath) != 0); }
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
            return Get(path, SubdocLookupFlags.None);
        }

        private static byte GetFlagsValue(SubdocLookupFlags flags)
        {
            if (flags.HasFlag(SubdocLookupFlags.AccessDeleted) && !flags.HasFlag(SubdocLookupFlags.XattrPath))
            {
                flags |= SubdocLookupFlags.XattrPath;
            }

            return (byte) flags;
        }

        /// <summary>
        /// Gets the value at a specified path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="flags">The Subdoc flags.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.Core.ILookupInBuilder`1" /> implementation reference for chaining operations.
        /// </returns>
        public ILookupInBuilder<TDocument> Get(string path, SubdocLookupFlags flags)
        {
            _commands.Enqueue(new OperationSpec
            {
                Path = path,
                OpCode = OperationCode.SubGet,
                Flags = GetFlagsValue(flags)
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
            return Exists(path, SubdocLookupFlags.None);
        }

        /// <summary>
        /// Checks for the existence of a given N1QL path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="flags">The Subdoc flags.</param>
        /// <returns>
        /// A <see cref="T:Couchbase.Core.ILookupInBuilder`1" /> implementation reference for chaining operations.
        /// </returns>
        public ILookupInBuilder<TDocument> Exists(string path, SubdocLookupFlags flags)
        {
            _commands.Enqueue(new OperationSpec
            {
                Path = path,
                OpCode = OperationCode.SubExist,
                Flags = GetFlagsValue(flags)
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
            OperationSpec command;
            if (_commands.TryPeek(out command))
            {
                return command;
            }
            return command;
        }

        /// <summary>
        /// Creates a new object that is a copy of the current instance.
        /// </summary>
        /// <returns>
        /// A new object that is a copy of this instance.
        /// </returns>
        public object Clone()
        {
            var clonedSpecs = _commands.Select(spec => spec.Clone()).ToList();
            return new LookupInBuilder<TDocument>(_invoker, _serializer, Key, clonedSpecs);
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
    }
}

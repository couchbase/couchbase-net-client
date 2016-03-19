using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Serialization;
using Couchbase.IO.Operations;

namespace Couchbase.Core
{
    /// <summary>
    /// An implementation of <see cref="ILookupInBuilder{TDocument}"/> that exposes a "builder" API for constructing a chain of read commands on a document within Couchbase.
    /// </summary>
    /// <typeparam name="TDocument">The type of the document.</typeparam>
    /// <seealso cref="Couchbase.Core.ILookupInBuilder{TDocument}" />
    /// <seealso cref="Couchbase.Core.Serialization.ITypeSerializerProvider" />
    public class LookupInBuilder<TDocument> : ILookupInBuilder<TDocument>, IEnumerable<OperationSpec>
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
        /// Gets the value at a specified N1QL path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public ILookupInBuilder<TDocument> Get(string path)
        {
            _commands.Enqueue(new OperationSpec
            {
                Path = path,
                OpCode = OperationCode.SubGet
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
            _commands.Enqueue(new OperationSpec
            {
                Path = path,
                OpCode = OperationCode.SubExist
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
        /// Gets the enumerator for enumerating the list of <see cref="OperationSpec"/>s.
        /// </summary>
        /// <returns></returns>
        IEnumerator<OperationSpec> IEnumerable<OperationSpec>.GetEnumerator()
        {
            while (!_commands.IsEmpty)
            {
                OperationSpec command;
                if (_commands.TryDequeue(out command))
                {
                    yield return command;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<OperationSpec>) this).GetEnumerator();
        }

        internal OperationSpec FirstSpec()
        {
            OperationSpec command;
            if (_commands.TryPeek(out command))
            {
                return command;
            }
            return command;
        }
    }
}

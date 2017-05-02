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
    /// Exposes the creation of a set of mutation operations to be performed.
    /// </summary>
    /// <typeparam name="TDocument">The type of the document.</typeparam>
    /// <seealso cref="Couchbase.Core.IMutateInBuilder{TDocument}" />
    /// <seealso cref="Couchbase.Core.Serialization.ITypeSerializerProvider" />
    public class MutateInBuilder<TDocument> : IMutateInBuilder<TDocument>,  IEnumerable<OperationSpec>, IEquatable<MutateInBuilder<TDocument>>
    {
        private readonly ISubdocInvoker _invoker;
        private readonly ConcurrentQueue<OperationSpec> _commands = new ConcurrentQueue<OperationSpec>();
        private readonly Func<ITypeSerializer> _serializer;
        private ITypeSerializer _cachedSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="MutateInBuilder{TDocument}"/> class.
        /// </summary>
        /// <param name="invoker">The invoker.</param>
        /// <param name="serializer">The serializer.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException">invoker or serializer or key
        /// </exception>
        internal MutateInBuilder(ISubdocInvoker invoker, Func<ITypeSerializer> serializer, string key)
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

            Cas = 0L;
            Expiry = new TimeSpan();
            PersistTo = PersistTo.Zero;
            ReplicateTo = ReplicateTo.Zero;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LookupInBuilder{TDocument}"/> class.
        /// </summary>
        /// <param name="invoker">The invoker.</param>
        /// <param name="serializer">The serializer.</param>
        /// <param name="key">The key.</param>
        /// <param name="specs">The specs.</param>
        internal MutateInBuilder(ISubdocInvoker invoker, Func<ITypeSerializer> serializer, string key, IEnumerable<OperationSpec> specs)
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
        /// The unique identifier for the document.
        /// </summary>
        public string Key { get; internal set; }

        /// <summary>
        /// A "check-and-set" value for ensuring that a document has not been modified by another thread.
        /// </summary>
        public ulong Cas { get; private set; }

        /// <summary>
        /// The "time-to-live" or "TTL" that specifies the document's lifetime.
        /// </summary>
        public TimeSpan Expiry { get; private set; }

        /// <summary>
        /// A durability constraint ensuring that a document has been persisted to the n^th node.
        /// </summary>
        public PersistTo PersistTo { get; private set; }

        /// <summary>
        /// A durability constraint for ensuring that the document has been replicated to the n^th node.
        /// </summary>
        public ReplicateTo ReplicateTo { get; private set; }

        /// <summary>
        /// Returns a count of the currently chained operations.
        /// </summary>
        /// <value>A
        ///   <see cref="int"/> representing the number of chained commands.</value>
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
            get { return _commands.Any(x => (x.Flags & (byte) SubdocMutateFlags.XattrPath) != 0); }
        }

        private static byte GetFlagsValue(SubdocMutateFlags flags)
        {
            if (flags.HasFlag(SubdocMutateFlags.ExpandMacro) && !flags.HasFlag(SubdocMutateFlags.XattrPath))
            {
                flags |= SubdocMutateFlags.XattrPath;
            }

            return (byte)flags;
        }

        /// <summary>
        /// Inserts an element into a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        /// <exception cref="System.ArgumentException">Path cannot be empty for an Insert.</exception>
        public IMutateInBuilder<TDocument> Insert(string path, object value, bool createParents = true)
        {
            return Insert(path, value, createParents ? SubdocMutateFlags.CreatePath : SubdocMutateFlags.None);
        }

        /// <summary>
        /// Inserts an element into a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <param name="flags">The subdocument flags. Defaults to <see cref="F:SubdocLookupFlags.CreatePath"/>.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        /// <exception cref="System.ArgumentException">Path cannot be empty for an Insert.</exception>
        public IMutateInBuilder<TDocument> Insert(string path, object value, SubdocMutateFlags flags = SubdocMutateFlags.CreatePath)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty for an Insert.");
            }

            _commands.Enqueue(new OperationSpec
            {
                OpCode = OperationCode.SubDictAdd,
                Path = path,
                Value = value,
                Flags = GetFlagsValue(flags)
            });

            return this;
        }

        /// <summary>
        /// Inserts or updates an element within or into a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        /// <exception cref="System.ArgumentException">Path cannot be empty for an Upsert.</exception>
        public IMutateInBuilder<TDocument> Upsert(string path, object value, bool createParents = true)
        {
            return Upsert(path, value, createParents ? SubdocMutateFlags.CreatePath : SubdocMutateFlags.None);
        }

        /// <summary>
        /// Inserts or updates an element within or into a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <param name="flags">The subdocument flags. Defaults to <see cref="F:SubdocLookupFlags.CreatePath"/>.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        /// <exception cref="System.ArgumentException">Path cannot be empty for an Upsert.</exception>
        public IMutateInBuilder<TDocument> Upsert(string path, object value, SubdocMutateFlags flags = SubdocMutateFlags.CreatePath)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty for an Upsert.");
            }

            _commands.Enqueue(new OperationSpec
            {
                OpCode = OperationCode.SubDictUpsert,
                Path = path,
                Value = value,
                Flags = GetFlagsValue(flags)
            });

            return this;
        }

        /// <summary>
        /// Replaces an element or value within a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        /// <exception cref="System.ArgumentException">Path cannot be empty for an Upsert.</exception>
        public IMutateInBuilder<TDocument> Replace(string path, object value)
        {
            return Replace(path, value, SubdocMutateFlags.None);
        }

        /// <summary>
        /// Replaces an element or value within a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <param name="flags">The subdocument flags. Defaults to <see cref="F:SubdocLookupFlags.None"/>.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        /// <exception cref="System.ArgumentException">Path cannot be empty for an Upsert.</exception>
        public IMutateInBuilder<TDocument> Replace(string path, object value, SubdocMutateFlags flags = SubdocMutateFlags.None)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty for an Upsert.");
            }

            _commands.Enqueue(new OperationSpec
            {
                OpCode = OperationCode.SubReplace,
                Path = path,
                Value = value,
                Flags = GetFlagsValue(flags)
            });

            return this;
        }

        /// <summary>
        /// Removes an element or value from a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        /// <exception cref="System.ArgumentException">Path cannot be empty for an Remove.</exception>
        public IMutateInBuilder<TDocument> Remove(string path)
        {
            return Remove(path, SubdocMutateFlags.None);
        }

        /// <summary>
        /// Removes an element or value from a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="flags">The subdocument flags. Defaults to <see cref="F:SubdocLookupFlags.None"/>.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        /// <exception cref="System.ArgumentException">Path cannot be empty for an Remove.</exception>
        public IMutateInBuilder<TDocument> Remove(string path, SubdocMutateFlags flags = SubdocMutateFlags.None)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty for an Remove.");
            }

            _commands.Enqueue(new OperationSpec
            {
                OpCode = OperationCode.SubDelete,
                Path = path,
                Flags = GetFlagsValue(flags)
            });

            return this;
        }

        /// <summary>
        /// Inserts an array value into a JSON document at a given path.
        /// </summary>
        /// <param name="value">An array value.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> ArrayAppend(object value, bool createParents = true)
        {
            return ArrayAppend(string.Empty, value, createParents ? SubdocMutateFlags.CreatePath : SubdocMutateFlags.None);
        }

        /// <summary>
        /// Inserts a value to the end of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An aray value.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> ArrayAppend(string path, object value, bool createParents = true)
        {
            return ArrayAppend(path, value, createParents ? SubdocMutateFlags.CreatePath : SubdocMutateFlags.None);
        }

        /// <summary>
        /// Inserts a value to the end of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An aray value.</param>
        /// <param name="flags">The subdocument flags. Defaults to <see cref="F:SubdocLookupFlags.CreatePath"/>.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> ArrayAppend(string path, object value, SubdocMutateFlags flags)
        {
            _commands.Enqueue(new OperationSpec
            {
                OpCode = OperationCode.SubArrayPushLast,
                Path = path,
                Value = value,
                Flags = GetFlagsValue(flags)
            });

            return this;
        }

        /// <summary>
        /// Inserts one or more values at the end of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> ArrayAppend(bool createParents = false, params object[] values)
        {
            return ArrayAppend(string.Empty, createParents ? SubdocMutateFlags.CreatePath : SubdocMutateFlags.None, values);
        }

        /// <summary>
        /// Inserts one or more values to the end of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> ArrayAppend(string path, bool createParents = false, params object[] values)
        {
            return ArrayAppend(path, createParents ? SubdocMutateFlags.CreatePath : SubdocMutateFlags.None, values);
        }

        /// <summary>
        /// Inserts one or more values to the end of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="flags">The subdocument flags. Defaults to <see cref="F:SubdocLookupFlags.None"/>.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> ArrayAppend(string path, SubdocMutateFlags flags = SubdocMutateFlags.None, params object[] values)
        {
            _commands.Enqueue(new OperationSpec
            {
                OpCode = OperationCode.SubArrayPushLast,
                Path = path,
                Value = values,
                Flags = GetFlagsValue(flags),
                RemoveBrackets = true
            });

            return this;
        }

        /// <summary>
        /// Inserts a value to the beginning of an array that is the root of a JSON document.
        /// </summary>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        public IMutateInBuilder<TDocument> ArrayPrepend(object value, bool createParents = true)
        {
            return ArrayPrepend(string.Empty, value, createParents ? SubdocMutateFlags.CreatePath : SubdocMutateFlags.None);
        }

        /// <summary>
        /// Inserts a value to the beginning of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> ArrayPrepend(string path, object value, bool createParents = true)
        {
            return ArrayPrepend(path, value, createParents ? SubdocMutateFlags.CreatePath : SubdocMutateFlags.None);
        }

        /// <summary>
        /// Inserts a value to the beginning of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value.</param>
        /// <param name="flags">The subdocument flags. Defaults to <see cref="F:SubdocLookupFlags.CreatePath"/>.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> ArrayPrepend(string path, object value, SubdocMutateFlags flags)
        {
            _commands.Enqueue(new OperationSpec
            {
                OpCode = OperationCode.SubArrayPushFirst,
                Path = path,
                Value = value,
                Flags = GetFlagsValue(flags)
            });

            return this;
        }

        /// <summary>
        /// Inserts one or more values to the beginning of an array that is the root of a JSON document.
        /// </summary>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        public IMutateInBuilder<TDocument> ArrayPrepend(bool createParents = false, params object[] values)
        {
            return ArrayPrepend(string.Empty, createParents ? SubdocMutateFlags.CreatePath : SubdocMutateFlags.None, values);
        }

        /// <summary>
        /// Inserts one or more values to the beginning of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> ArrayPrepend(string path, bool createParents = false, params object[] values)
        {
            return ArrayPrepend(path, createParents ? SubdocMutateFlags.CreatePath : SubdocMutateFlags.None, values);
        }

        /// <summary>
        /// Inserts one or more values to the beginning of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="flags">The subdocument flags. Defaults to <see cref="F:SubdocLookupFlags.None"/>.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> ArrayPrepend(string path, SubdocMutateFlags flags = SubdocMutateFlags.None, params object[] values)
        {
            _commands.Enqueue(new OperationSpec
            {
                OpCode = OperationCode.SubArrayPushFirst,
                Path = path,
                Value = values,
                Flags = GetFlagsValue(flags),
                RemoveBrackets = true
            });

            return this;
        }

        /// <summary>
        /// Inserts a value at a given position within an array. The position is indicated as part of the path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">A value.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        /// <exception cref="System.ArgumentException">Path cannot be empty for an ArrayInsert.</exception>
        public IMutateInBuilder<TDocument> ArrayInsert(string path, object value)
        {
            return ArrayInsert(path, value, SubdocMutateFlags.None);
        }

        /// <summary>
        /// Inserts a value at a given position within an array. The position is indicated as part of the path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">A value.</param>
        /// <param name="flags">The subdocument flags. Defaults to <see cref="F:SubdocLookupFlags.None"/>.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        /// <exception cref="System.ArgumentException">Path cannot be empty for an ArrayInsert.</exception>
        public IMutateInBuilder<TDocument> ArrayInsert(string path, object value, SubdocMutateFlags flags = SubdocMutateFlags.None)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty for an ArrayInsert.");
            }

            _commands.Enqueue(new OperationSpec
            {
                OpCode = OperationCode.SubArrayInsert,
                Path = path,
                Value = value,
                Flags = GetFlagsValue(flags)
            });

            return this;
        }

        /// <summary>
        /// Inserts one or more values at a given position within an array. The position is indicated as part of the path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        /// <exception cref="System.ArgumentException">Path cannot be empty for an ArrayInsert.</exception>
        public IMutateInBuilder<TDocument> ArrayInsert(string path, params object[] values)
        {
            return ArrayInsert(path, SubdocMutateFlags.None, values);
        }

        /// <summary>
        /// Inserts one or more values at a given position within an array. The position is indicated as part of the path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="values">One or more values.</param>
        /// <param name="flags">The subdocument flags. Defaults to <see cref="F:SubdocLookupFlags.None"/>.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        /// <exception cref="System.ArgumentException">Path cannot be empty for an ArrayInsert.</exception>
        public IMutateInBuilder<TDocument> ArrayInsert(string path, SubdocMutateFlags flags = SubdocMutateFlags.None, params object[] values)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty for an ArrayInsert.");
            }

            _commands.Enqueue(new OperationSpec
            {
                OpCode = OperationCode.SubArrayInsert,
                Path = path,
                Value = values,
                Flags = GetFlagsValue(flags),
                RemoveBrackets = true
            });

            return this;
        }

        /// <summary>
        /// Adds a value to an array if the value does not already exist in the array.
        /// </summary>
        /// <param name="value">A unique value.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> ArrayAddUnique(object value, bool createParents = true)
        {
            return ArrayAddUnique(string.Empty, value, createParents ? SubdocMutateFlags.CreatePath : SubdocMutateFlags.None);
        }

        /// <summary>
        /// Adds a value to an array if the value does not already exist in the array.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">A unique value.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> ArrayAddUnique(string path, object value, bool createParents = true)
        {
            return ArrayAddUnique(path, value, createParents ? SubdocMutateFlags.CreatePath : SubdocMutateFlags.None);
        }

        /// <summary>
        /// Adds a value to an array if the value does not already exist in the array.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">A unique value.</param>
        /// <param name="flags">The subdocument flags. Defaults to <see cref="F:SubdocLookupFlags.CreatePath"/>.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> ArrayAddUnique(string path, object value, SubdocMutateFlags flags)
        {
            _commands.Enqueue(new OperationSpec
            {
                OpCode = OperationCode.SubArrayAddUnique,
                Path = path,
                Value = value,
                Flags = GetFlagsValue(flags)
            });

            return this;
        }

        /// <summary>
        /// Performs an arithmetic operation on a numeric value in a document.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="delta">The value to increment or decrement the original value by.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        /// <exception cref="System.ArgumentException">Path cannot be empty for a Counter.</exception>
        public IMutateInBuilder<TDocument> Counter(string path, long delta, bool createParents = true)
        {
            return Counter(path, delta, createParents ? SubdocMutateFlags.CreatePath : SubdocMutateFlags.None);
        }

        /// <summary>
        /// Performs an arithmetic operation on a numeric value in a document.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="delta">The value to increment or decrement the original value by.</param>
        /// <param name="flags">The subdocument flags. Defaults to <see cref="F:SubdocLookupFlags.CreatePath"/>.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        /// <exception cref="System.ArgumentException">Path cannot be empty for a Counter.</exception>
        public IMutateInBuilder<TDocument> Counter(string path, long delta, SubdocMutateFlags flags = SubdocMutateFlags.CreatePath)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty for a Counter.");
            }

            _commands.Enqueue(new OperationSpec
            {
                OpCode = OperationCode.SubCounter,
                Path = path,
                Value = delta,
                Flags = GetFlagsValue(flags)
            });

            return this;
        }

        /// <summary>
        /// Applies an expiration to a document.
        /// </summary>
        /// <param name="expiry">The "time-to-live" or TTL of the document.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> WithExpiry(TimeSpan expiry)
        {
            Expiry = expiry;
            return this;
        }

        /// <summary>
        /// A "check-and-set" value for ensuring that a document has not been modified by another thread.
        /// </summary>
        /// <param name="cas">The CAS value.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        /// <remarks>Be aware that <see cref="long"/> will be internally cast to a <see cref="ulong"/>.</remarks>
        public IMutateInBuilder<TDocument> WithCas(long cas)
        {
            Cas = (ulong)cas;
            return this;
        }

        /// <summary>
        /// A "check-and-set" value for ensuring that a document has not been modified by another thread.
        /// </summary>
        /// <param name="cas">The CAS value.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> WithCas(ulong cas)
        {
            Cas = cas;
            return this;
        }

        /// <summary>
        /// A durability constraint ensuring that a document has been persisted to the n^th node.
        /// </summary>
        /// <param name="persistTo">The <see cref="P:Couchbase.Core.IMutateInBuilder`1.PersistTo" /> value.</param>
        /// <returns></returns>
        public IMutateInBuilder<TDocument> WithDurability(PersistTo persistTo)
        {
            PersistTo = persistTo;
            return this;
        }

        /// <summary>
        /// A durability constraint ensuring that a document has been persisted to the n^th node.
        /// </summary>
        /// <param name="replicateTo">The <see cref="P:Couchbase.Core.IMutateInBuilder`1.ReplicateTo" /> value.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> WithDurability(ReplicateTo replicateTo)
        {
            ReplicateTo = replicateTo;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="P:Couchbase.Core.IMutateInBuilder`1.ReplicateTo" /> and <see cref="P:Couchbase.Core.IMutateInBuilder`1.PersistTo" /> values for a document.
        /// </summary>
        /// <param name="persistTo">The <see cref="P:Couchbase.Core.IMutateInBuilder`1.PersistTo" /> value.</param>
        /// <param name="replicateTo">The <see cref="P:Couchbase.Core.IMutateInBuilder`1.ReplicateTo" /> value.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.Core.IMutateInBuilder`1" /> reference for chaining operations.
        /// </returns>
        public IMutateInBuilder<TDocument> WithDurability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            WithDurability(replicateTo);
            WithDurability(persistTo);
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
        /// Gets the enumerator.
        /// </summary>
        /// <returns></returns>
        IEnumerator<OperationSpec> IEnumerable<OperationSpec>.GetEnumerator()
        {
            return _commands.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<OperationSpec>)this).GetEnumerator();
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

        /// <summary>
        /// Creates a new object that is a copy of the current instance.
        /// </summary>
        /// <returns>
        /// A new object that is a copy of this instance.
        /// </returns>
        public object Clone()
        {
            var clonedSpecs = _commands.Select(spec => spec.Clone()).ToList();
            return new MutateInBuilder<TDocument>(_invoker, _serializer, Key, clonedSpecs);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        public bool Equals(MutateInBuilder<TDocument> other)
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

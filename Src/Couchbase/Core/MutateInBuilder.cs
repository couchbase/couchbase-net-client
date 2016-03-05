using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Serialization;
using Couchbase.IO.Operations;

namespace Couchbase.Core
{
    public class MutateInBuilder<TDocument> : IMutateInBuilder<TDocument>, ITypeSerializerProvider
    {
        private readonly ISubdocInvoker _invoker;
        private readonly ConcurrentQueue<SubDocOperationResult> _commands = new ConcurrentQueue<SubDocOperationResult>();
        private readonly Func<ITypeSerializer> _serializer;
        private ITypeSerializer _cachedSerializer;

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

        public string Key { get; set; }

        public long Cas { get; private set; }

        public TimeSpan Expiry { get; private set; }

        public PersistTo PersistTo { get; private set; }

        public ReplicateTo ReplicateTo { get; private set; }

        public IMutateInBuilder<TDocument> Insert(string path, object value, bool createParents = true)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty for an Insert.");
            }

            _commands.Enqueue(new SubDocOperationResult
            {
                OpCode = OperationCode.SubDictAdd,
                Path = path,
                Value = value,
                CreateParents = createParents
            });

            return this;
        }

        public IMutateInBuilder<TDocument> Upsert(string path, object value, bool createParents = true)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty for an Upsert.");
            }

            _commands.Enqueue(new SubDocOperationResult
            {
                OpCode = OperationCode.SubDictUpsert,
                Path = path,
                Value = value,
                CreateParents = createParents
            });

            return this;
        }

        public IMutateInBuilder<TDocument> Replace(string path, object value)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty for an Upsert.");
            }

            _commands.Enqueue(new SubDocOperationResult
            {
                OpCode = OperationCode.SubReplace,
                Path = path,
                Value = value
            });

            return this;
        }

        public IMutateInBuilder<TDocument> Remove(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty for an Remove.");
            }

            _commands.Enqueue(new SubDocOperationResult
            {
                OpCode = OperationCode.SubDelete,
                Path = path
            });

            return this;
        }

        public IMutateInBuilder<TDocument> PushBack(string path, object value, bool createParents = true)
        {
            _commands.Enqueue(new SubDocOperationResult
            {
                OpCode = OperationCode.SubArrayPushLast,
                Path = path,
                Value = value,
                CreateParents = createParents
            });

            return this;
        }

        public IMutateInBuilder<TDocument> PushFront(string path, object value, bool createParents = true)
        {
            _commands.Enqueue(new SubDocOperationResult
            {
                OpCode = OperationCode.SubArrayPushFirst,
                Path = path,
                Value = value,
                CreateParents = createParents
            });

            return this;
        }

        public IMutateInBuilder<TDocument> ArrayInsert(string path, object value)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty for an ArrayInsert.");
            }

            _commands.Enqueue(new SubDocOperationResult
            {
                OpCode = OperationCode.SubArrayInsert,
                Path = path,
                Value = value
            });

            return this;
        }

        public IMutateInBuilder<TDocument> AddUnique(string path, object value, bool createParents = true)
        {
            _commands.Enqueue(new SubDocOperationResult
            {
                OpCode = OperationCode.SubArrayAddUnique,
                Path = path,
                Value = value,
                CreateParents = createParents
            });

            return this;
        }

        public IMutateInBuilder<TDocument> Counter(string path, long delta, bool createParents = true)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be empty for a Counter.");
            }

            _commands.Enqueue(new SubDocOperationResult
            {
                OpCode = OperationCode.SubCounter,
                Path = path,
                Value = delta,
                CreateParents = createParents
            });

            return this;
        }

        public IMutateInBuilder<TDocument> WithExpiry(TimeSpan expiry)
        {
            Expiry = expiry;
            return this;
        }

        public IMutateInBuilder<TDocument> WithCas(long cas)
        {
            Cas = cas;
            return this;
        }

        public IMutateInBuilder<TDocument> WithDurability(PersistTo persistTo)
        {
            PersistTo = persistTo;
            return this;
        }

        public IMutateInBuilder<TDocument> WithDurability(ReplicateTo replicateTo)
        {
            ReplicateTo = replicateTo;
            return this;
        }

        public IMutateInBuilder<TDocument> WithDurability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            WithDurability(replicateTo);
            WithDurability(persistTo);
            return this;
        }

        public IDocumentFragment<TDocument> Execute()
        {
            return _invoker.Invoke(this);
        }

        internal IEnumerable<SubDocOperationResult> GetEnumerator()
        {
            while (!_commands.IsEmpty)
            {
                SubDocOperationResult command;
                if (_commands.TryDequeue(out command))
                {
                    yield return command;
                }
            }
        }
    }
}

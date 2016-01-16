using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Couchbase.Core.IO.SubDocument;
using Couchbase.IO.Operations;

namespace Couchbase.Core
{
    public class MutateInBuilder : IMutateInBuilder
    {
        private readonly ISubdocInvoker _invoker;
        private readonly ConcurrentQueue<SubDocOperationResult> _commands = new ConcurrentQueue<SubDocOperationResult>();

        internal MutateInBuilder(ISubdocInvoker invoker, string key)
        {
            _invoker = invoker;
            Key = key;

            Cas = 0L;
            Expiry = new TimeSpan();
            PersistTo = PersistTo.Zero;
            ReplicateTo = ReplicateTo.Zero;
        }

        public string Key { get; set; }

        public long Cas { get; private set; }

        public TimeSpan Expiry { get; private set; }

        public PersistTo PersistTo { get; private set; }

        public ReplicateTo ReplicateTo { get; private set; }

        public IMutateInBuilder Insert(string path, object value, bool createParents = true)
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

        public IMutateInBuilder Upsert(string path, object value, bool createParents = true)
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

        public IMutateInBuilder Replace(string path, object value)
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

        public IMutateInBuilder Remove(string path)
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

        public IMutateInBuilder PushBack(string path, object value, bool createParents = true)
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

        public IMutateInBuilder PushFront(string path, object value, bool createParents = true)
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


        public IMutateInBuilder ArrayInsert(string path, object value)
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

        public IMutateInBuilder AddUnique(string path, object value, bool createParents = true)
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

        public IMutateInBuilder Counter(string path, long delta, bool createParents = true)
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

        public IMutateInBuilder WithExpiry(TimeSpan expiry)
        {
            Expiry = expiry;
            return this;
        }

        public IMutateInBuilder WithCas(long cas)
        {
            Cas = cas;
            return this;
        }

        public IMutateInBuilder WithDurability(PersistTo persistTo)
        {
            PersistTo = persistTo;
            return this;
        }

        public IMutateInBuilder WithDurability(ReplicateTo replicateTo)
        {
            ReplicateTo = replicateTo;
            return this;
        }

        public IMutateInBuilder WithDurability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            WithDurability(replicateTo);
            WithDurability(persistTo);
            return this;
        }

        public IDocumentFragment<TContent> Execute<TContent>()
        {
            return _invoker.Invoke<TContent>(this);
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

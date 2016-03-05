using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Serialization;
using Couchbase.IO.Operations;

namespace Couchbase.Core
{
    public class LookupInBuilder<TDocument> : ILookupInBuilder<TDocument>, ITypeSerializerProvider
    {
        private readonly ISubdocInvoker _invoker;
        private readonly ConcurrentQueue<SubDocOperationResult> _commands = new ConcurrentQueue<SubDocOperationResult>();
        private readonly Func<ITypeSerializer> _serializer;
        private ITypeSerializer _cachedSerializer;

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

        public ILookupInBuilder<TDocument> Get(string path)
        {
            _commands.Enqueue(new SubDocOperationResult
            {
                Path = path,
                OpCode = OperationCode.SubGet
            });
            return this;
        }

        public ILookupInBuilder<TDocument> Exists(string path)
        {
            _commands.Enqueue(new SubDocOperationResult
            {
                Path = path,
                OpCode = OperationCode.SubExist
            });
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

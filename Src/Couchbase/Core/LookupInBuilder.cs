using System.Collections.Concurrent;
using System.Collections.Generic;
using Couchbase.Core.IO.SubDocument;
using Couchbase.IO.Operations;

namespace Couchbase.Core
{
    public class LookupInBuilder : ILookupInBuilder
    {
        private readonly ISubdocInvoker _invoker;
        private readonly ConcurrentQueue<SubDocOperationResult> _commands = new ConcurrentQueue<SubDocOperationResult>();

        internal LookupInBuilder(ISubdocInvoker invoker, string key)
        {
            _invoker = invoker;
            Key = key;
        }

        public string Key { get; set; }

        public ILookupInBuilder Get(string path)
        {
            _commands.Enqueue(new SubDocOperationResult
            {
                Path = path,
                OpCode = OperationCode.SubGet
            });
            return this;
        }

        public ILookupInBuilder Exists(string path)
        {
            _commands.Enqueue(new SubDocOperationResult
            {
                Path = path,
                OpCode = OperationCode.SubExist
            });
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

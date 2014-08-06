using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO;
using Couchbase.IO.Operations;

namespace Couchbase
{
    public sealed class DocumentResult<T> : IResult<T>
    {
        public DocumentResult(IOperationResult<T> result, string id)
        {
            Document = new Document<T>
            {
                Cas = result.Cas,
                Id = id,
                Value = result.Value
            };
            Value = Document.Value;
            Message = result.Message;
            Status = result.Status;
            Success = result.Success;
        }

        public bool Success { get; private set; }

        public IDocument<T> Document { get; private set; }

        public T Value { get; private set; }

        public string Message { get; private set; }

        public ResponseStatus Status { get; private set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO;
using Couchbase.IO.Operations;

namespace Couchbase
{
    public class DocumentResult : IResult
    {
        public DocumentResult(IOperationResult<object> result, string id)
        {

            Message = result.Message;
            Status = result.Status;
            Success = result.Success;
        }

        public bool Success { get; private set; }

        public string Message { get; private set; }

        public ResponseStatus Status { get; private set; }
    }
}

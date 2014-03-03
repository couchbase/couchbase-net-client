using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Newtonsoft.Json;

namespace Couchbase.Tests.Fakes
{
    internal class FakeOperation : OperationBase<string>
    {
        private FakeOperationResult _operationResult;
        public FakeOperation()
        {
        }

        public void SetOperationResult(FakeOperationResult operationResult)
        {
            _operationResult = operationResult;
            Header = new OperationHeader
            {
                Status = operationResult.Status,
                Cas = operationResult.Cas
            };
        }

        public override IOperationResult<string> GetResult()
        {
            return _operationResult;
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Get; }
        }
    }
}

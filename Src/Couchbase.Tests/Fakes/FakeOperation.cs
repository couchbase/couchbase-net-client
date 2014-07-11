using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Newtonsoft.Json;

namespace Couchbase.Tests.Fakes
{
    internal class FakeOperation : OperationBase<string>
    {
        private FakeOperationResult _operationResult;

        public FakeOperation(IByteConverter converter) : base(converter)
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

        public override void Read(byte[] buffer, int offset, int length)
        {
            throw new NotImplementedException();
        }

        public override byte[] Write()
        {
            throw new NotImplementedException();
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
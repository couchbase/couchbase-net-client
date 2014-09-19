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
    internal class FakeOperationResult : IOperationResult<string>
    {
        public FakeOperationResult()
        {
        }

        public FakeOperationResult(OperationBase<string> operation)
        {

        }

        public bool Success { get; set; }

        public string Value { get; set; }

        public string Message { get; set; }

        public ResponseStatus Status { get; set; }

        public ulong Cas { get; set; }


        public IBucketConfig GetConfig()
        {
            string text = File.ReadAllText(@"Data\\Configuration\\carrier-publication-config.json");
            return JsonConvert.DeserializeObject<BucketConfig>(text);
        }


        public Durability Durability { get; set; }


        public Exception Exception { get; set; }
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
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
    internal class FakeOperationResult : OperationResult<string>
    {
        public FakeOperationResult()
            : base(null)
        {
        }

        public FakeOperationResult(OperationBase<string> operation) : base(operation)
        {

        }

        public new bool Success { get; set; }

        public new string Value { get; set; }

        public new string Message { get; set; }

        public new  ResponseStatus Status { get; set; }

        public new ulong Cas { get; set; }


        public override IBucketConfig GetConfig()
        {
            var text = File.ReadAllText(@"Data\\Configuration\\carrier-publication-config.json");
            return JsonConvert.DeserializeObject<BucketConfig>(text);
        }
    }

}

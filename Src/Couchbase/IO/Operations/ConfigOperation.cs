using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Serializers;

namespace Couchbase.IO.Operations
{
    internal class ConfigOperation : OperationBase<BucketConfig>
    {
        public override OperationCode OperationCode
        {
            get { return OperationCode.GetClusterConfig; }
        }
    }
}

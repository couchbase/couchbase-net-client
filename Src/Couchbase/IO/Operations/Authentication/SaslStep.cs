using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.IO.Operations.Authentication
{
    internal class SaslStep : SaslStart
    {
         public SaslStep(string key, string value) 
            : base(key, value)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.SaslStep; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.IO.Operations.Authentication
{
    internal class SaslContinue : SaslAuthenticate
    {
        public SaslContinue(string key, string userName, string passWord) 
            : base(key, userName, passWord)
        {
        }
    }
}

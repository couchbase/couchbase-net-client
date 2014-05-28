using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;

namespace Couchbase.IO
{
    internal static class IOStrategyFactory
    {
        public static Func<IConnectionPool, Func<string, string, string, ISaslMechanism>, IOStrategy>  GetFactory()
        {
            return null;
        }
    }
}

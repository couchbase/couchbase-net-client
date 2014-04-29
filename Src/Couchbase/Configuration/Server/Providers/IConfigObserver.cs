using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration.Server.Providers
{
    internal interface IConfigObserver
    {
        string Name { get; }

        void NotifyConfigChanged(IConfigInfo configInfo);
    }
}

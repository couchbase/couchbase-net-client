using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO;

namespace Couchbase.Configuration.Server.Providers
{
    internal interface IConfigProvider : IDisposable
    {
        IConfigInfo GetCached(string name);

        IConfigInfo GetConfig(string name);

        IConfigInfo GetConfig(string name, string password);

        bool RegisterObserver(IConfigObserver observer);

        void UnRegisterObserver(IConfigObserver observer);

        bool ObserverExists(IConfigObserver observer);
    }
}

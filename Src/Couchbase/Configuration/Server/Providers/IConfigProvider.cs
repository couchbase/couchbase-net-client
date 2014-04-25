using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO;

namespace Couchbase.Configuration.Server.Providers
{
    internal interface IConfigProvider
    {
        IConfigInfo GetCached(string name);

        IConfigInfo GetConfig(string name);

        IConfigInfo GetConfig(string name, string password);

        void Start();

        bool RegisterListener(IConfigListener listener);

        void UnRegisterListener(IConfigListener listener);

        bool ListenerExists(IConfigListener listener);
    }

    internal interface IConfigListener
    {
        string Name { get; }

        void NotifyConfigChanged(IConfigInfo configInfo);
    }
}

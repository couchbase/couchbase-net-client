using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO;

namespace Couchbase.Configuration.Server.Providers
{
    /// <summary>
    /// A provider for <see cref="IConfigInfo"/> objects which represent Couchbase Server configurations: mappings of VBuckets and keys to cluster nodes.
    /// </summary>
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

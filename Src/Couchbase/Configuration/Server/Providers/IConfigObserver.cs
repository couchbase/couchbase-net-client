using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration.Server.Providers
{
    /// <summary>
    /// An interface for implementing classes which observe changes from configuration providers.
    /// </summary>
    internal interface IConfigObserver
    {
        /// <summary>
        /// The name of the observer - the Bucket's name.
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Notifies the observer that a configuration change has occured and it's internal state must be updated.
        /// </summary>
        /// <param name="configInfo"></param>
        void NotifyConfigChanged(IConfigInfo configInfo);
    }
}

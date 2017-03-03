using System;
using System.Collections.Generic;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.IO;
using Couchbase.IO.Converters;

namespace Couchbase.Configuration.Server.Providers
{
    /// <summary>
    /// A provider for <see cref="IConfigInfo"/> objects which represent Couchbase Server configurations: mappings of VBuckets and keys to cluster nodes.
    /// </summary>
    internal interface IConfigProvider : IDisposable
    {
        IConfigInfo GetCached(string name);

        IConfigInfo GetConfig(string name);

        IConfigInfo GetConfig(string name, string username, string password);

        bool RegisterObserver(IConfigObserver observer);

        void UnRegisterObserver(IConfigObserver observer);

        bool ObserverExists(IConfigObserver observer);

        IByteConverter Converter { get; set; }

        /// <summary>
        /// Updates the new configuration if the new configuration revision is greater than the current configuration.
        /// </summary>
        /// <param name="bucketConfig">The bucket configuration.</param>
        /// <param name="force">if set to <c>true</c> [force].</param>
        void UpdateConfig(IBucketConfig bucketConfig, bool force = false);
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
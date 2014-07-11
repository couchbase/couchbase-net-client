using System;

namespace Couchbase.Configuration.Server.Providers
{
    /// <summary>
    /// An interface for implementing classes which observe changes from configuration providers.
    /// </summary>
    internal interface IConfigObserver : IDisposable
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
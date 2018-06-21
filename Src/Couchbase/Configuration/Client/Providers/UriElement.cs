#if NET452
using System;
using System.Configuration;

namespace Couchbase.Configuration.Client.Providers
{
    /// <summary>
    /// Allows a server to be added to the bootstrap list within an <see cref="CouchbaseClientSection"/>.
    /// </summary>
    public sealed class UriElement : ConfigurationElement
    {
        /// <summary>
        /// The <see cref="Uri"/> of the Couchbase server to connect to.
        /// </summary>
        [ConfigurationProperty("uri", IsRequired = true, IsKey = true)]
        public Uri Uri
        {
            get { return (Uri) this["uri"]; }
            set { this["uri"] = value; }
        }
    }
}

#endif

#region [ License information ]

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
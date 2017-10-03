#if NET45
using System.Configuration;
using Couchbase.IO;

namespace Couchbase.Configuration.Client.Providers
{
    /// <summary>
    /// A configuration element for registering custom <see cref="IIOService"/>s.
    /// </summary>
    public class IOServiceElement : ConfigurationElement
    {
        public IOServiceElement()
        {
            Name = "default";
            Type = "Couchbase.IO.Services.SharedPooledIOService, Couchbase.NetClient";
        }

        /// <summary>
        /// Gets or sets the name of the custom <see cref="IIOService"/>
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        [ConfigurationProperty("name", IsRequired = true, IsKey = true)]
        public string Name
        {
            get { return (string)this["name"]; }
            set { this["name"] = value; }
        }

        /// <summary>
        /// Gets or sets the <see cref="Type"/> of the custom <see cref="IIOService"/>
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        [ConfigurationProperty("type", IsRequired = true, IsKey = false)]
        public string Type
        {
            get { return (string)this["type"]; }
            set { this["type"] = value; }
        }
    }
}

#endif

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

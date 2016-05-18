#if NET45
using System;
using System.Configuration;

namespace Couchbase.Configuration.Client.Providers
{
    /// <summary>
    /// Represents a collection of <see cref="Uri"/> for a Couchbase cluster. The client will use this list for bootstrapping and communicating with the cluster.
    /// </summary>
    public sealed class UriElementCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new UriElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var elm = element as UriElement;
            if (elm == null)
            {
                throw new InvalidCastException("element");
            }
            return elm.Uri;
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
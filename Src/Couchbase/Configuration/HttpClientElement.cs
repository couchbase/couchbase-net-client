﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace Couchbase.Configuration
{
    public class HttpClientElement : ConfigurationElement, IHttpClientConfiguration
    {
        /// <summary>
        /// When true, instructs the client to pre-fetch a given URI
        /// to initialize the ServicePoint for future requests
        /// </summary>
        [ConfigurationProperty("initializeConnection", IsRequired = false, DefaultValue = true)]
        public bool InitializeConnection
        {
            get { return (bool)base["initializeConnection"]; }
            set { base["initializeConnection"] = value; }
        }

        /// <summary>
        /// Gets or sets the timeout for http client connections
        /// </summary>
        [ConfigurationProperty("timeout", IsRequired = false, DefaultValue = "00:01:15")]
        public TimeSpan Timeout
        {
            get { return (TimeSpan)base["timeout"]; }
            set { base["timeout"] = value; }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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
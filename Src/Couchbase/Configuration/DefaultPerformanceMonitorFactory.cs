﻿using System;
using System.Collections.Generic;
using System.Net;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;

namespace Couchbase.Configuration
{
    public class DefaultPerformanceMonitorFactory : ICouchbasePerformanceMonitorFactory
    {
        private string prefix;

        IPerformanceMonitor ICouchbasePerformanceMonitorFactory.Create(string bucket)
        {
            return new DefaultPerformanceMonitor(this.prefix + (String.IsNullOrEmpty(bucket) ? "default" : bucket));
        }

        void IProvider.Initialize(Dictionary<string, string> parameters)
        {
            if (parameters != null)
                parameters.TryGetValue("prefix", out this.prefix);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
 *    @copyright 2010 Attila Kiskó, enyim.com
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
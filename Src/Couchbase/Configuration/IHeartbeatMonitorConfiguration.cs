using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase.Configuration
{
    /// <summary>
    /// Defines an interface for setting Heartbeat monitor attributes
    /// </summary>
    public interface IHeartbeatMonitorConfiguration
    {
        //Uri of heartbeat endpoint
        string Uri { get; set; }

        /// <summary>
        /// Time between checks, in milliseconds
        /// </summary>
        int Interval { get; set; }

        /// <summary>
        /// Determines whether to run the heartbeat checks
        /// </summary>
        bool Enabled { get; set; }
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
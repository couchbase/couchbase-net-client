using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace Couchbase.Configuration
{
	public class HeartbeatMonitorElement : ConfigurationElement, IHeartbeatMonitorConfiguration
	{
		/// <summary>
		/// Gets or sets the endpoint uri for the heartbeat request
		/// </summary>
		[ConfigurationProperty("uri", IsRequired = false)]
		public string Uri
		{
			get { return (string)base["uri"]; }
			set { base["uri"] = value; }
		}

		/// <summary>
		/// Gets or sets the interval between heartbeat requests
		/// </summary>
		[ConfigurationProperty("interval", IsRequired = false, DefaultValue = 10000)]
		public int Interval 
		{
			get { return (int)base["interval"]; }
			set { base["interval"] = value; }
		}

		/// <summary>
		/// Enables or disables the heartbeat requests
		/// </summary>
		[ConfigurationProperty("enabled", IsRequired = false, DefaultValue = true)]
		public bool Enabled
		{
			get { return (bool)base["enabled"]; }
			set { base["enabled"] = value; }
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

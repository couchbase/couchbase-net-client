using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase.Management
{
	public class Bucket
	{
		private const int MIN_RAM_QUOTA = 100;

		public string Name { get; set; }

		public BucketTypes BucketType { get; set; }

		public AuthTypes AuthType { get; set; }

		public int RamQuotaMB { get; set; }

		public int? ProxyPort { get; set; }

		public short ReplicaNumber { get; set; }

		public string Password { get; set; }

		public IDictionary<string, string> ValidationErrors { get; set; }

		public bool IsValid()
		{
			ValidationErrors = new Dictionary<string, string>();

			if (string.IsNullOrEmpty(Name))
				ValidationErrors["Name"] = "Name must be specified";

			if (RamQuotaMB < MIN_RAM_QUOTA)
				ValidationErrors["RamQuotaMB"] = "RamQuotaMB must be at least " + MIN_RAM_QUOTA;

			if (AuthType == AuthTypes.None && (ProxyPort == null || !ProxyPort.HasValue))
				ValidationErrors["ProxyPort"] = "ProxyPort is required when AuthType is 'none'";

			if (AuthType == AuthTypes.Sasl && (ProxyPort != null && ProxyPort.HasValue))
				ValidationErrors["ProxyPort"] = "ProxyPort may not be used with AuthType 'sasl'";

			return ValidationErrors.Keys.Count == 0;
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
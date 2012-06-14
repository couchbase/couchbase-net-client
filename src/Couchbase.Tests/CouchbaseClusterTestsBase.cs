using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Couchbase.Configuration;
using Couchbase.Management;

namespace Couchbase.Tests
{
	public abstract class CouchbaseClusterTestsBase
	{
		protected CouchbaseCluster _Cluster;

		[SetUp]
		public void SetUp()
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri("http://localhost:8091/pools"));
			config.Bucket = "default";

			_Cluster = new CouchbaseCluster(config);
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
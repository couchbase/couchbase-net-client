using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached.Results;
using Couchbase.Operations;

namespace Couchbase.Results
{
	public interface IObserveOperationResult : IOperationResult
	{
		/// <summary>
		/// The key to be observed
		/// </summary>
		string Key { get; set; }

		/// <summary>
		/// Cas associated with a particular observation of the key to be observed
		/// </summary>
		ulong Cas { get; set; }

		/// <summary>
		/// Indicates whether a key has been persisted
		/// </summary>
		ObserveKeyState KeyState { get; set; }

		/// <summary>
		/// Average replication time for the cluster
		/// </summary>
		int ReplicationStats { get; set; }

		/// <summary>
		/// Average persistence time for the cluster
		/// </summary>
		int PersistenceStats { get; set; }
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Membase.Configuration;

namespace Couchbase.Configuration
{
	public interface ICouchbaseClientConfiguration : IMembaseClientConfiguration
	{
		/// <summary>
		/// Creates a name transformer instance whihc will be used to change the design document's name before retrieving it from the server.
		/// </summary>
		/// <remarks>
		/// This way you can create additional views over the same data, and use one set production and other set(s) for testing and development without changing your application code. (Couchbase provides UI support 'development views' where the name of the design document is prefixed with $dev.)
		/// </remarks>
		/// <returns>A transformed name.</returns>
		INameTransformer CreateDesignDocumentNameTransformer();
	}

	public interface INameTransformer
	{
		/// <summary>
		/// Transform the name into a different value.
		/// </summary>
		/// <param name="name">The name to be transformed.</param>
		/// <returns>The new name.</returns>
		string Transform(string name);
	}
}

#region [ License information          ]
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2011 Couchbase, Inc.
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

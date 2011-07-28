using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase.Configuration
{
	/// <summary>
	/// Name transformer for Couchbase's development views. Prefixes all design document names with 'dev_'.
	/// </summary>
	public sealed class DevelopmentModeNameTransformer : INameTransformer
	{
		public const string NamePrefix = "dev_";

		string INameTransformer.Transform(string name)
		{
			return NamePrefix + name;
		}
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

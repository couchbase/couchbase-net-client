using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase.Tests.Utils
{
	public static class KeyValueUtils
	{
		public static string GenerateKey(string prefix = null)
		{
			return (!string.IsNullOrEmpty(prefix) ? prefix + "_" : "") +
				"unit_test_" + DateTime.Now.Ticks;
		}

		public static IEnumerable<string> GenerateKeys(string prefix = null, int max = 5)
		{

			var keys = new List<string>(max);
			for (int i = 0; i < max; i++)
			{
				keys.Add(GenerateKey(prefix));
			}

			return keys;
		}

		public static string GenerateValue()
		{
			var rand = new Random((int)DateTime.Now.Ticks).Next();
			return "unit_test_value_" + rand;
		}

		public static Tuple<string, string> GenerateKeyAndValue(string prefix = null)
		{
			return new Tuple<string, string>(GenerateKey(prefix), GenerateValue());
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
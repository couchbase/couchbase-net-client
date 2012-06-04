using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase.Tests.Mocks
{
	public class MockHttpRequest : IHttpRequest
	{
		public Dictionary<string, string> Parameters { get; set; }

		public string Path { get; set; }

		public void AddParameter(string name, string value)
		{
			if (Parameters == null) Parameters = new Dictionary<string, string>();
			Parameters[name] = value;
		}

		public IHttpResponse GetResponse()
		{
			return new MockHttpResponse();
		}

		public HttpMethod Method { get; set; }
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
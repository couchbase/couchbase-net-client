using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Net;
using Couchbase.Helpers;

namespace Couchbase.Tests.HelperTests
{
	[TestFixture(Category="HelperTests")]
	public class HttpHelperTests
	{
		private string _url = "http://localhost:8888/";
		private string _output = "OK";
		private HttpListener _listener = null;

		[SetUp]
		public void SetUp()
		{
			_listener = new HttpListener();
			_listener.Prefixes.Add(_url);
			_listener.Start();

			var ctxResult = _listener.BeginGetContext(result =>
			{
				var listener = result.AsyncState as HttpListener;
				var bytes = Encoding.Default.GetBytes(_output);
				var ctx = listener.EndGetContext(result);
				ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
				ctx.Response.OutputStream.Close();
			}, _listener);

			
		}

		[TearDown]
		public void TearDown()
		{
			_listener.Stop();
			_listener.Close();
		}
		
		[Test]
		public void When_Performing_Get_Response_Is_OK()
		{
			var output = HttpHelper.Get(new Uri(_url));
			
			Assert.That(output, Is.StringMatching(_output));
		}

		[Test]
		public void When_Performing_Post_Response_Is_OK()
		{
			var output = HttpHelper.Post(new Uri(_url), "", "", "");
			Assert.That(output, Is.StringMatching(_output));
		}

		[Test]
		public void When_Performing_Delete_Response_Is_OK()
		{
			var output = HttpHelper.Delete(new Uri(_url), "", "");
			Assert.That(output, Is.StringMatching(_output));
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
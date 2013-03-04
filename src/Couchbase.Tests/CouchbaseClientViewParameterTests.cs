using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Couchbase.Configuration;
using Couchbase.Tests.Mocks;
using System.Reflection;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientViewParameterTests : CouchbaseClientViewTestsBase
	{

		#region Key tests

        /// <summary>
        /// @test: String keys serialize to json format while retrieving view results
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if the value after serialization matches the expected value
        /// </summary>
        [Test]
		public void When_Requesting_View_String_Keys_Are_Json_Serialized()
		{
			testJsonKeySerialization("foo", "\"foo\"");
		}

        /// <summary>
        /// @test: While retrieving view results for array of keys, they get serialized to json
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if the value after serialization matches the expected value
        /// </summary>
		[Test]
		public void When_Requesting_View_Array_Keys_Are_Json_Serialized()
		{
			testJsonKeySerialization(new object[] { "foo", 3.14 }, "[\"foo\",3.14]");
		}

        /// <summary>
        /// @test: Int keys serialize to json format while retrieving view results
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if the value after serialization matches the expected value
        /// </summary>
		[Test]
		public void When_Requesting_View_Int_Keys_Are_Json_Serialized()
		{
			testJsonKeySerialization(10, "10");
		}

        /// <summary>
        /// @test: Float keys serialize to json format while retrieving view results
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if the value after serialization matches the expected value
        /// </summary>
		[Test]
		public void When_Requesting_View_Float_Keys_Are_Json_Serialized()
		{
			testJsonKeySerialization(3.14, "3.14");
		}

        /// <summary>
        /// @test: Nested array keys serialize to json format while retrieving view results
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if the value after serialization matches the expected value
        /// </summary>
		[Test]
		public void When_Requesting_View_Nested_Array_Keys_Are_Json_Serialized()
		{
			testJsonKeySerialization(new object[] { "foo", 3.14, new object[] { "foo" } }, "[\"foo\",3.14,[\"foo\"]]");
		}

		#endregion

		#region StartKey tests

        /// <summary>
        /// @test: String start-keys serialize to json format while retrieving view results
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if the value after serialization matches the expected value
        /// </summary>
		[Test]
		public void When_Requesting_View_String_StartKeys_Are_Json_Serialized()
		{
			testJsonStartKeySerialization("foo", "\"foo\"");
		}

        /// <summary>
        /// @test: String array start-keys serialize to json format while retrieving view results
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if the value after serialization matches the expected value
        /// </summary>
		[Test]
		public void When_Requesting_View_Array_StartKeys_Are_Json_Serialized()
		{
			testJsonStartKeySerialization(new object[] { "foo", 3.14 }, "[\"foo\",3.14]");
		}

        /// <summary>
        /// @test: Int start-keys serialize to json format while retrieving view results
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if the value after serialization matches the expected value
        /// </summary>
		[Test]
		public void When_Requesting_View_Int_StartKeys_Are_Json_Serialized()
		{
			testJsonStartKeySerialization(10, "10");
		}

        /// <summary>
        /// @test: Floar start-keys serialize to json format while retrieving view results
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if the value after serialization matches the expected value
        /// </summary>
		[Test]
		public void When_Requesting_View_Float_StartKeys_Are_Json_Serialized()
		{
			testJsonStartKeySerialization(3.14, "3.14");
		}

        /// <summary>
        /// @test: Nested array start-keys serialize to json format while retrieving view results
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if the value after serialization matches the expected value
        /// </summary>
		[Test]
		public void When_Requesting_View_Nested_Array_StartKeys_Are_Json_Serialized()
		{
			testJsonStartKeySerialization(new object[] { "foo", 3.14, new object[] { "foo" } }, "[\"foo\",3.14,[\"foo\"]]");
		}

		#endregion

		#region EndKey tests

        /// <summary>
        /// @test: String end-keys serialize to json format while retrieving view results
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if the value after serialization matches the expected value
        /// </summary>
		[Test]
		public void When_Requesting_View_String_EndKeys_Are_Json_Serialized()
		{
			testJsonEndKeySerialization("foo", "\"foo\"");
		}

        /// <summary>
        /// @test: String Array End keys serialize to json format while retrieving view results
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if the value after serialization matches the expected value
        /// </summary>
		[Test]
		public void When_Requesting_View_Array_EndKeys_Are_Json_Serialized()
		{
			testJsonEndKeySerialization(new object[] { "foo", 3.14 }, "[\"foo\",3.14]");
		}

        /// <summary>
        /// @test: Int end keys serialize to json format while retrieving view results
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if the value after serialization matches the expected value
        /// </summary>
		[Test]
		public void When_Requesting_View_Int_EndKeys_Are_Json_Serialized()
		{
			testJsonEndKeySerialization(10, "10");
		}

        /// <summary>
        /// @test: Float end keys serialize to json format while retrieving view results
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if the value after serialization matches the expected value
        /// </summary>
		[Test]
		public void When_Requesting_View_Float_EndKeys_Are_Json_Serialized()
		{
			testJsonStartKeySerialization(3.14, "3.14");
		}

        /// <summary>
        /// @test: Nested array end keys serialize to json format while retrieving view results
        /// @pre: Default configuration to initialize client in app.config 
        /// @post: Test passes if the value after serialization matches the expected value
        /// </summary>
		[Test]
		public void When_Requesting_View_Nested_Array_EndKeys_Are_Json_Serialized()
		{
			testJsonEndKeySerialization(new object[] { "foo", 3.14, new object[] { "foo" } }, "[\"foo\",3.14,[\"foo\"]]");
		}

		#endregion

		private void testJsonStartKeySerialization(object value, string serializedValue)
		{
			testJsonSerialization(value, serializedValue, "startKey");
		}

		private void testJsonEndKeySerialization(object value, string serializedValue)
		{
			testJsonSerialization(value, serializedValue, "endKey");
		}

		private void testJsonKeySerialization(object value, string serializedValue)
		{
			testJsonSerialization(value, serializedValue, "key");
		}

		private void testJsonSerialization(object value, string serializedValue, string paramName)
		{
			var clientWithConfig = GetClientWithConfig();
			var view = clientWithConfig.Item1.GetView("mock", "someview");

			switch (paramName)
			{
				case "startKey":
					view.StartKey(value);
					break;
				case "endKey":
					view.EndKey(value);
					break;
				case "key":
					view.Key(value);
					break;
			}

			//Previously, params were exposed via a public property on the IHttpRequest,
			//which was available on the view instance after executing a view.
			//This is no longer the case and currently, private fields offer the only
			//means of testing values of view params.
			var field = view.GetType().GetField(paramName, BindingFlags.NonPublic | BindingFlags.Instance);
			var val = field.GetValue(view).ToString();

			Assert.That(val, Is.EqualTo(serializedValue), "Key was not " + serializedValue);
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

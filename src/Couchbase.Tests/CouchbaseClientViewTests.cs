using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientViewTests : CouchbaseClientViewTestsBase
	{
        /// <summary>
        /// @test: Retrieve view result with debug true should return debug information
        /// of data type dictionary
        /// @pre: Default configuration to initialize client in app.config and have view wih design document cities and view name by_name 
        /// @post: Test passes if debug info is returned correctly
        /// </summary>
		[Test]
		public void When_Querying_View_With_Debug_True_Debug_Info_Dictionary_Is_Returned()
		{
			var view = _Client.GetView("cities", "by_name").Limit(1).Debug(true);
			foreach (var item in view) { }

			Assert.That(view.DebugInfo, Is.InstanceOf(typeof(Dictionary<string, object>)));
		}

        /// <summary>
        /// @test: Retrieve view result with debug false should return no debug information
        /// @pre: Default configuration to initialize client in app.config and have view wih design document cities and view name by_name 
        /// @post: Test passes if no debug info is returned
        /// </summary>
		[Test]
		public void When_Querying_View_With_Debug_False_Debug_Info_Dictionary_Is_Returned()
		{
			var view = _Client.GetView("cities", "by_name").Limit(1).Debug(true);
			foreach (var item in view) { }

			Assert.That(view.DebugInfo, Is.Null);
		}
	}
}

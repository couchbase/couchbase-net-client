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
		[Test]
		public void When_Querying_View_With_Debug_True_Debug_Info_Dictionary_Is_Returned()
		{
			var view = _Client.GetView("cities", "by_name").Limit(1).Debug(true);
			foreach (var item in view) { }

			Assert.That(view.DebugInfo, Is.InstanceOf(typeof(Dictionary<string, object>)));
		}

		[Test]
		public void When_Querying_View_With_Debug_False_Debug_Info_Dictionary_Is_Returned()
		{
			var view = _Client.GetView("cities", "by_name").Limit(1).Debug(true);
			foreach (var item in view) { }

			Assert.That(view.DebugInfo, Is.Null);
		}
	}
}

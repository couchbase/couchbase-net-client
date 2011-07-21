using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Couchbase.Configuration;
using FluentAssertions;

namespace CouchbaseTests
{
	[TestClass]
	public class DevelopmentModeNameTransformerTest
	{
		[TestMethod]
		public void TestTransformer()
		{
			INameTransformer nt = new DevelopmentModeNameTransformer();

			nt.Transform("key").Should().Be("$dev_key");
		}
	}
}

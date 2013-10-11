using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientViewReduceTests : CouchbaseClientViewTestsBase
	{
        /// <summary>
        /// @test: Row count of view result is one when it is not grouped
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if the view count is one
        /// </summary>
		[Test]
		public void When_View_Is_Reduced_Without_Group_Row_Count_Is_One()
		{
			var view = Client.GetView("cities", "by_state");
			foreach (var item in view) { }

			Assert.That(view.Count(), Is.EqualTo(1), "Row count was not 1");
		}

        /// <summary>
        /// @test: Row count of view result is greater than one when it is grouped
        /// @pre: Default configuration to initialize client in app.config
        /// @post: Test passes if the view count is greater than one
        /// </summary>
		[Test]
		public void When_View_Is_Reduced_With_Group_Row_Count_Is_Greater_Than_One()
		{
			var view = Client.GetView("cities", "by_state").Group(true);
			foreach (var item in view) { }

			Assert.That(view.Count(), Is.GreaterThan(1), "Row count was not 1");

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

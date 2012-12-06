﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClientViewPagingTests : CouchbaseClientViewTestsBase
	{
        /// <summary>
        /// @test: Get the view results and display all items of an index in pages
        /// @pre: Default configuration to initialize client in app.config, configure view name 'by_name' with design name 'cities' 
        /// @post: Test passes if it retrives all items in view and page size is correct
        /// </summary>
		[Test]
		public void When_Paging_Non_Generic_View_Page_Sizes_Are_Correct()
		{
			var view = _Client.GetView("cities", "by_name").GetPagedView(5);

			testPageSizes(view);
		}

        /// <summary>
        /// @test: Get view results with generic type, key and id are not set, page results will throw error
        /// @pre: Default configuration to initialize client in app.config, configure view name 'by_name' with design name 'cities' 
        /// @post: Test passes if the view results throw invalid operation exception
        /// </summary>
		[Test]
		[ExpectedException(typeof(InvalidOperationException))]
		public void Paging_Generic_View_Throws_Exception_When_Key_And_Id_Are_Not_Set()
		{
			var view = _Client.GetView<City>("cities", "by_name", true).GetPagedView(5);

			while (view.MoveNext()) { foreach (var item in view) { } }

		}

        /// <summary>
        /// @test: Get view results with generic type, key and if are set properly,
        /// page results will display correct results
        /// @pre: Default configuration to initialize client in app.config, configure view name 'by_name' with design name 'cities' 
        /// @post: Test passes if the view results show correct page sizes
        /// </summary>
		[Test]
		public void When_Paging_Generic_View_Page_Sizes_Are_Correct()
		{
			var view = _Client.GetView<City>("cities", "by_name", true).GetPagedView(5, "Id", "Name");

			testPageSizes(view);

		}

        /// <summary>
        /// @test: Get view results with generic type, key and id are set, paging view count is correct
        /// @pre: Default configuration to initialize client in app.config, configure view name 'by_name' with design name 'cities' 
        /// @post: Test passes if thecount of view results is correct
        /// </summary>
		[Test]
		public void When_Paging_View_Count_Is_Greater_Than_Zero()
		{
			var view = _Client.GetView<City>("cities", "by_name", true).GetPagedView(5, "Id", "Name");
			while (view.MoveNext()) { foreach (var item in view) { } }
			
			Assert.That(view.TotalRows, Is.GreaterThan(0));
		}

		private void testPageSizes<T>(IPagedView<T> view)
		{
			var totalCount = 0;
			var lastCount = 0;
			var isNotEqualToPageSize = false;

			while (view.MoveNext())
			{
				if (view.Count() != view.PageSize)
				{
					if (isNotEqualToPageSize)
					{
						Assert.Fail("More than 1 page was not the full page size");
					}
					isNotEqualToPageSize = true;
					lastCount = view.Count(); //last page
				}

				foreach (var item in view)
				{	
					totalCount++;
				}
			}

			Assert.That((totalCount - lastCount) % view.PageSize, Is.EqualTo(0), "View size less last page was not ");
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

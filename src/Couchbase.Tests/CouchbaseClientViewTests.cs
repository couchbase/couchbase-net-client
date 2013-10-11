using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Couchbase.Exceptions;

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
		public void When_Querying_Grouped_View_With_Debug_True_Debug_Info_Dictionary_Is_Returned()
		{
			var view = Client.GetView("cities", "by_state").Group(true).Debug(true);
			foreach (var item in view) { }

			Assert.That(view.DebugInfo, Is.InstanceOf(typeof(Dictionary<string, object>)));
			Console.WriteLine(view.DebugInfo.Keys.Count);
		}

		/// <summary>
		/// @test: Retrieve view result with debug true should return debug information
		/// of data type dictionary
		/// @pre: Default configuration to initialize client in app.config and have view wih design document cities and view name by_name 
		/// @post: Test passes if debug info is returned correctly
		/// </summary>
		[Test]
		public void When_Querying_View_With_Debug_True_Debug_Info_Dictionary_Is_Returned()
		{
			var view = Client.GetView("cities", "by_name").Limit(1).Debug(true);
			foreach (var item in view) { }

			Assert.That(view.DebugInfo, Is.InstanceOf(typeof(Dictionary<string, object>)));
			Console.WriteLine(view.DebugInfo.Keys.Count);
		}

        /// <summary>
        /// @test: Retrieve view result with debug false should return no debug information
        /// @pre: Default configuration to initialize client in app.config and have view wih design document cities and view name by_name
        /// @post: Test passes if no debug info is returned
        /// </summary>
		[Test]
		public void When_Querying_View_With_Debug_False_Debug_Info_Dictionary_Is_Null()
		{
			var view = Client.GetView("cities", "by_name").Limit(1).Debug(false);
			foreach (var item in view) { }

			Assert.That(view.DebugInfo, Is.Null);
		}

		/// <summary>
		/// @test: Check design document for non-existent view, expect false from IView.ViewExists
		/// @pre: Default configuration to initialize client in app.config and have missing view with design document cities and view name by_postal_code 
		/// @post: Test passes if false is returned
		/// </summary>
		[Test]
		public void When_Checking_For_View_That_Does_Not_Exist_Check_Exists_Returns_False()
		{
			var view = Client.GetView("cities", "by_postal_code");
			var exists = view.CheckExists();
			Assert.That(exists, Is.False);
		}

		/// <summary>
		/// @test: Check design document for non-existent view, expect false from IView.ViewExists
		/// @pre: Default configuration to initialize client in app.config and have missing view with design document cities and view name by_postal_code 
		/// @post: Test passes if false is returned
		/// </summary>
		[Test]
		public void When_Checking_For_View_That_Exists_Check_Exists_Returns_True()
		{
			var view = Client.GetView("cities", "by_name");
			var exists = view.CheckExists();
			Assert.That(exists, Is.True);
		}

		/// <summary>
		/// @test: Check that ViewNotFoundException is returned when querying a bad view name
		/// @pre: Default configuration to initialize client in app.config and have missing view with design document cities and view name by_postal_code 
		/// @post: Test passes if exception is thrown
		/// </summary>
		[Test]
		[ExpectedException(typeof(ViewNotFoundException))]
		public void When_Querying_A_View_That_Does_Not_Exist_In_A_Design_Doc_That_Does_Exist_View_Not_Found_Exception_Is_Thrown()
		{
			var view = Client.GetView("cities", "by_postal_code");
			view.Count();
		}

		/// <summary>
		/// @test: Check that ViewNotFoundException is returned when querying a bad view name
		/// @pre: Default configuration to initialize client in app.config and have missing view with design document cities and view name by_postal_code 
		/// @post: Test passes if exception is thrown and string contains not_found
		/// </summary>
		[Test]
		public void When_Querying_A_View_That_Does_Not_Exist_In_A_Design_Doc_That_Does_Exist_Exception_Contains_Not_Found_Error()
		{
			try
			{
				var view = Client.GetView("cities", "by_postal_code");
				view.Count();
			}
			catch (ViewNotFoundException e)
			{
				Assert.That(e.Reason, Is.StringContaining("not_found"));
				return;
			}

			Assert.Fail();
		}

		/// <summary>
		/// @test: Check that ViewNotFoundException is returned when querying a bad view name
		/// @pre: Default configuration to initialize client in app.config and have missing view with design document cities and view name by_postal_code 
		/// @post: Test passes if exception is thrown with proper messages
		/// </summary>
		[Test]
		[ExpectedException(typeof(ViewNotFoundException))]
		public void When_Querying_A_View_In_A_Design_Doc_That_Does_Not_Exist_View_Not_Found_Exception_Is_Thrown()
		{
			var view = Client.GetView("states", "by_name");
			view.Count();
		}

		/// <summary>
		/// @test: Check that ViewNotFoundException is returned when querying a bad view name
		/// @pre: Default configuration to initialize client in app.config and have missing view with design document cities and view name by_postal_code 
		/// @post: Test passes if exception is thrown
		/// </summary>
		[Test]
		public void When_Querying_A_View_That_In_A_Design_Doc_That_Does_Not_Exist_Exception_Contains_Not_Found_Error()
		{
			try
			{
				var view = Client.GetView("states", "by_postal_code");
				view.Count();
			}
			catch (ViewNotFoundException e)
			{
				Assert.That(e.Error, Is.StringContaining("not_found"));
				return;
			}

			Assert.Fail();
		}

		/// <summary>
		/// @test: Check that ViewException is returned when querying a view with bad params
		/// @pre: Default configuration to initialize client in app.config and have design document cities
		///	      and view name by_name without a reduce function
		/// @post: ViewException is thrown
		/// </summary>
		[Test]
		[ExpectedException(typeof(ViewException))]
		public void When_Providing_Invalid_Parameters_To_An_Existing_View_A_View_Exception_Is_Thrown()
		{
			var view = Client.GetView("cities", "by_name").Group(true);
			view.Count();
		}

		/// <summary>
		/// @test: Check that ViewException is returned with error and reason when querying a view with bad params
		/// @pre: Default configuration to initialize client in app.config and have design document cities
		///	      and view name by_name without a reduce function
		/// @post: ViewException is thrown with error and reason
		/// </summary>
		[Test]
		public void When_Providing_Invalid_Parameters_To_An_Existing_View_A_View_Exception_Is_Thrown_And_Contains_Error_And_Reason()
		{
			try
			{
				var view = Client.GetView("cities", "by_name").Group(true);
			view.Count();
			}
			catch (ViewException e)
			{
				Assert.That(e.Reason, Is.Not.Null.Or.Empty);
				Assert.That(e.Message, Is.Not.Null.Or.Empty);
				return;
			}

			Assert.Fail();
		}
	}
}

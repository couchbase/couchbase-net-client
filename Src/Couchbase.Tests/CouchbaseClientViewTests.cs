using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Couchbase.Configuration;
using Enyim.Caching.Memcached;
using Moq;
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
            foreach (var item in view)
            {
            }

            Assert.That(view.DebugInfo, Is.InstanceOf(typeof (Dictionary<string, object>)));
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
            foreach (var item in view)
            {
            }

            Assert.That(view.DebugInfo, Is.InstanceOf(typeof (Dictionary<string, object>)));
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
            foreach (var item in view)
            {
            }

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
        [ExpectedException(typeof (ViewNotFoundException))]
        public void
            When_Querying_A_View_That_Does_Not_Exist_In_A_Design_Doc_That_Does_Exist_View_Not_Found_Exception_Is_Thrown()
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
        public void
            When_Querying_A_View_That_Does_Not_Exist_In_A_Design_Doc_That_Does_Exist_Exception_Contains_Not_Found_Error()
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
        [ExpectedException(typeof (ViewNotFoundException))]
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
        ///          and view name by_name without a reduce function
        /// @post: ViewException is thrown
        /// </summary>
        [Test]
        [ExpectedException(typeof (ViewException))]
        public void When_Providing_Invalid_Parameters_To_An_Existing_View_A_View_Exception_Is_Thrown()
        {
            var view = Client.GetView("cities", "by_name").Group(true);
            view.Count();
        }

        /// <summary>
        /// @test: Check that ViewException is returned with error and reason when querying a view with bad params
        /// @pre: Default configuration to initialize client in app.config and have design document cities
        ///          and view name by_name without a reduce function
        /// @post: ViewException is thrown with error and reason
        /// </summary>
        [Test]
        public void
            When_Providing_Invalid_Parameters_To_An_Existing_View_A_View_Exception_Is_Thrown_And_Contains_Error_And_Reason
            ()
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

        [Test]
        public void When_UrlEncodeKeys_Is_True_Keys_With_Special_Chars_Are_Succesful()
        {
            CreateViewFromFile(@"Data\\ViewWithCompoundKey.json", "test");
            var view = Client.GetView("test", "all").UrlEncode(true);
            IViewRow item = view.Key(new object[]
            {
                123,
                "a+b"
            }).Stale(StaleMode.False).FirstOrDefault();

            Assert.IsNotNull(item);
        }

        [Test]
        public void When_UrlEncodeKeys_Is_True_In_Ctor_Keys_With_Special_Chars_Are_Succesful()
        {
            CreateViewFromFile(@"Data\\ViewWithCompoundKey.json", "test");
            IViewRow item = Client.GetView("test", "all", true).Key(new object[]
            {
                123,
                "a+b"
            }).Stale(StaleMode.False).FirstOrDefault();

            Assert.IsNotNull(item);
        }

        [Test]
        public void When_UrlEncodeKeys_Is_False_In_Ctor_Keys_With_Special_Chars_Fails()
        {
            CreateViewFromFile(@"Data\\ViewWithCompoundKey.json", "test");
            IViewRow item = Client.GetView("test", "all", false).Key(new object[]
            {
                123,
                "a+b"
            }).Stale(StaleMode.False).FirstOrDefault();

            Assert.IsNull(item);
        }

        [Test]
        public void When_UrlEncodeKeys_Is_False_Keys_With_Special_Chars_Fails()
        {
            CreateViewFromFile(@"Data\\ViewWithCompoundKey.json", "test");
            var view = Client.GetView("test", "all");
            IViewRow item = view.Key(new object[]
            {
                123,
                "a+b"
            }).Stale(StaleMode.False).FirstOrDefault();

            Assert.IsNull(item);
        }

        [Test]
        public void When_NonGeneric_GetView_Is_Called_Non_Json_Data_Can_Be_Returned()
        {
            CreateViewFromFile(@"Data\\ViewThatReturnsDocsWithAsyncInsertInKeys.json", "Async");
            var key1 = "SnapBucketAsyncInsertPhotoLike.4639.7616";
            var data =
                "AAEAAAD/////AQAAAAAAAAAMAgAAAEJPUkZyYW1ld29yaywgVmVyc2lvbj0xLjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPW51bGwFAQAAACBPUkZyYW1ld29yay5Nb2RlbHMuQXN5bmNJbnNlcnRUTwQAAAAPX2ludmFsaWRhdGVsaXN0CV9jYWNoZWtleQRfc3FsDl9zcWxQYXJhbWV0ZXJzAwEBA9UCU3lzdGVtLkNvbGxlY3Rpb25zLkdlbmVyaWMuTGlzdGAxW1tTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYy5LZXlWYWx1ZVBhaXJgMltbU3lzdGVtLlN0cmluZywgbXNjb3JsaWIsIFZlcnNpb249NC4wLjAuMCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1iNzdhNWM1NjE5MzRlMDg5XSxbU3lzdGVtLkludDMyLCBtc2NvcmxpYiwgVmVyc2lvbj00LjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPWI3N2E1YzU2MTkzNGUwODldXSwgbXNjb3JsaWIsIFZlcnNpb249NC4wLjAuMCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1iNzdhNWM1NjE5MzRlMDg5XV2OAVN5c3RlbS5Db2xsZWN0aW9ucy5HZW5lcmljLkxpc3RgMVtbT1JGcmFtZXdvcmsuTW9kZWxzLlNpbXBsZVNxbFBhcmFtZXRlciwgT1JGcmFtZXdvcmssIFZlcnNpb249MS4wLjAuMCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1udWxsXV0CAAAACQMAAAAGBAAAABNQaG90b0xpa2UuNDYzOS43NjE2BgUAAAClBiBJbnNlcnQgaW50byBbUGhvdG9MaWtlXSAoW1NuYXBQaG90b0lkXSAsIFtTbmFwUGhvdG9TaXRlSWRdICwgW1NuYXBVc2VySWRdICwgW09SVXNlcklkXSAsIFtPUlVzZXJTaXRlSWRdICwgW1JhdGluZ10gLCBbQ3JlYXRlVGltZV0gKSB2YWx1ZXMgKEBTbmFwUGhvdG9JZCAsIEBTbmFwUGhvdG9TaXRlSWQgLCBAU25hcFVzZXJJZCAsIEBPUlVzZXJJZCAsIEBPUlVzZXJTaXRlSWQgLCBAUmF0aW5nICwgQENyZWF0ZVRpbWUgKSA7IFVQREFURSBbU25hcFBob3RvXSBTRVQgTGlrZUNvdW50PUxpa2VDb3VudCsxLExpa2VDb3VudDI0SG91cnM9TGlrZUNvdW50MjRIb3VycysxLExhc3RMaWtlVGltZT1AQ3VycmVudERhdGVUaW1lMiwNCiAgICAgICAgICAgICAgICAgICAgICAgICAgICBMYXN0TGlrZURhdGVTZXJpYWw9QExhc3RMaWtlRGF0ZVNlcmlhbDINCiAgICAgICAgICAgICAgICAgICAgICAgICBXSEVSRSBTbmFwUGhvdG9JZD1AU25hcFBob3RvSWQyO0lOU0VSVCBJTlRPIFNuYXBVc2VyQWN0aXZpdHkgKFtTbmFwVXNlcklkXSxbVHlwZUlkXSxbQWN0aW9uVXNlcklkXSxbU25hcFBob3RvSWRdLFtJc1JlYWRdLFtDcmVhdGVUaW1lXSkgVkFMVUVTDQogICAgICAgICAgICAgICAgICAgICAgICAoIChTRUxFQ1QgU25hcFVzZXJJZCBGUk9NIFNuYXBQaG90byBXSVRIKG5vbG9jaykNCiAgICAgICAgICAgICAgICAgICAgICAgIFdIRVJFIFNuYXBQaG90b0lkID0gQFNuYXBQaG90b0lkMiksIEBTbmFwVXNlckFjdGl2aXR5VHlwZUlkMixAQWN0aW9uVXNlcklkMixAU25hcFBob3RvSWQyLDAsQEN1cnJlbnREYXRlVGltZTIpOyAJBgAAAAQDAAAA1QJTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYy5MaXN0YDFbW1N5c3RlbS5Db2xsZWN0aW9ucy5HZW5lcmljLktleVZhbHVlUGFpcmAyW1tTeXN0ZW0uU3RyaW5nLCBtc2NvcmxpYiwgVmVyc2lvbj00LjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPWI3N2E1YzU2MTkzNGUwODldLFtTeXN0ZW0uSW50MzIsIG1zY29ybGliLCBWZXJzaW9uPTQuMC4wLjAsIEN1bHR1cmU9bmV1dHJhbCwgUHVibGljS2V5VG9rZW49Yjc3YTVjNTYxOTM0ZTA4OV1dLCBtc2NvcmxpYiwgVmVyc2lvbj00LjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPWI3N2E1YzU2MTkzNGUwODldXQMAAAAGX2l0ZW1zBV9zaXplCF92ZXJzaW9uAwAA5QFTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYy5LZXlWYWx1ZVBhaXJgMltbU3lzdGVtLlN0cmluZywgbXNjb3JsaWIsIFZlcnNpb249NC4wLjAuMCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1iNzdhNWM1NjE5MzRlMDg5XSxbU3lzdGVtLkludDMyLCBtc2NvcmxpYiwgVmVyc2lvbj00LjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPWI3N2E1YzU2MTkzNGUwODldXVtdCAgJBwAAAAEAAAABAAAABAYAAACOAVN5c3RlbS5Db2xsZWN0aW9ucy5HZW5lcmljLkxpc3RgMVtbT1JGcmFtZXdvcmsuTW9kZWxzLlNpbXBsZVNxbFBhcmFtZXRlciwgT1JGcmFtZXdvcmssIFZlcnNpb249MS4wLjAuMCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1udWxsXV0DAAAABl9pdGVtcwVfc2l6ZQhfdmVyc2lvbgQAACdPUkZyYW1ld29yay5Nb2RlbHMuU2ltcGxlU3FsUGFyYW1ldGVyW10CAAAACAgJCAAAAAwAAAAMAAAABwcAAAAAAQAAAAQAAAAD4wFTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYy5LZXlWYWx1ZVBhaXJgMltbU3lzdGVtLlN0cmluZywgbXNjb3JsaWIsIFZlcnNpb249NC4wLjAuMCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1iNzdhNWM1NjE5MzRlMDg5XSxbU3lzdGVtLkludDMyLCBtc2NvcmxpYiwgVmVyc2lvbj00LjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPWI3N2E1YzU2MTkzNGUwODldXQT3////4wFTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYy5LZXlWYWx1ZVBhaXJgMltbU3lzdGVtLlN0cmluZywgbXNjb3JsaWIsIFZlcnNpb249NC4wLjAuMCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1iNzdhNWM1NjE5MzRlMDg5XSxbU3lzdGVtLkludDMyLCBtc2NvcmxpYiwgVmVyc2lvbj00LjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPWI3N2E1YzU2MTkzNGUwODldXQIAAAADa2V5BXZhbHVlAQAIBgoAAAAJU25hcFBob3RvwB0AAAH1////9////woAAAAAAfT////3////CgAAAAAB8/////f///8KAAAAAAcIAAAAAAEAAAAQAAAABCVPUkZyYW1ld29yay5Nb2RlbHMuU2ltcGxlU3FsUGFyYW1ldGVyAgAAAAkOAAAACQ8AAAAJEAAAAAkRAAAACRIAAAAJEwAAAAkUAAAACRUAAAAJFgAAAAkXAAAACRgAAAAJGQAAAA0EDBoAAABOU3lzdGVtLkRhdGEsIFZlcnNpb249NC4wLjAuMCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1iNzdhNWM1NjE5MzRlMDg5BQ4AAAAlT1JGcmFtZXdvcmsuTW9kZWxzLlNpbXBsZVNxbFBhcmFtZXRlcgMAAAAFX25hbWUGX3ZhbHVlB19kYnR5cGUBAgQVU3lzdGVtLkRhdGEuU3FsRGJUeXBlGgAAAAIAAAAGGwAAAAxAU25hcFBob3RvSWQJHAAAAAXj////FVN5c3RlbS5EYXRhLlNxbERiVHlwZQEAAAAHdmFsdWVfXwAIGgAAAAgAAAABDwAAAA4AAAAGHgAAABBAU25hcFBob3RvU2l0ZUlkCR8AAAAB4P///+P///8IAAAAARAAAAAOAAAABiEAAAALQFNuYXBVc2VySWQJIgAAAAHd////4////wgAAAABEQAAAA4AAAAGJAAAAAlAT1JVc2VySWQJJQAAAAHa////4////wgAAAABEgAAAA4AAAAGJwAAAA1AT1JVc2VyU2l0ZUlkCSgAAAAB1////+P///8IAAAAARMAAAAOAAAABioAAAAHQFJhdGluZwkrAAAAAdT////j////CAAAAAEUAAAADgAAAAYtAAAAC0BDcmVhdGVUaW1lCS4AAAAB0f///+P///8EAAAAARUAAAAOAAAABjAAAAANQFNuYXBQaG90b0lkMgkxAAAAAc7////j////CAAAAAEWAAAADgAAAAYzAAAAEUBDdXJyZW50RGF0ZVRpbWUyCTQAAAABy////+P///8EAAAAARcAAAAOAAAABjYAAAAYQFNuYXBVc2VyQWN0aXZpdHlUeXBlSWQyCTcAAAAByP///+P///8IAAAAARgAAAAOAAAABjkAAAAOQEFjdGlvblVzZXJJZDIJOgAAAAHF////4////wgAAAABGQAAAA4AAAAGPAAAABRATGFzdExpa2VEYXRlU2VyaWFsMgk9AAAAAcL////j////CAAAAAUcAAAAHVN5c3RlbS5EYXRhLlNxbFR5cGVzLlNxbEludDMyAgAAAAptX2ZOb3ROdWxsB21fdmFsdWUAAAEIGgAAAAHAHQAAAR8AAAAcAAAAAQAAAAABIgAAABwAAAABHxIAAAElAAAAHAAAAAHYtAEAASgAAAAcAAAAAQAAAAABKwAAABwAAAABAQAAAAUuAAAAIFN5c3RlbS5EYXRhLlNxbFR5cGVzLlNxbERhdGVUaW1lAwAAAAptX2ZOb3ROdWxsBW1fZGF5Bm1fdGltZQAAAAEICBoAAAAB66IAABwsGgABMQAAABwAAAABwB0AAAE0AAAALgAAAAHrogAAHCwaAAE3AAAAHAAAAAEDAAAAAToAAAAcAAAAAR8SAAABPQAAABwAAAABF1EzAQs=";
            Client.ExecuteStore(StoreMode.Set, key1, data);

            var view = Client.GetView("Async", "Insert").Stale(StaleMode.AllowStale).Limit(10000);
            var rowKeys = view.Select(r => r.Info["key"].ToString()).ToArray();
            Assert.IsNotEmpty(rowKeys);
        }
    }
}

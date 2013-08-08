using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using Couchbase.Management;
using NUnit.Framework;
using System.Net;
using Couchbase.Configuration;

namespace Couchbase.Tests
{
    /// <summary>
    /// Assumes that a SASL enabled bucket called "authenticated" exists with a password of "secret". Also
    /// note that a design document called "cities" must exist (with data from Data\CityDocs.json and views from Data\CityViews.json) 
    /// </summary>
    [TestFixture]
    public class CouchbaseAuthenticatedViewTests
    {
        private CouchbaseClient _client;
        private readonly string _bucketName;
        private readonly string _bucketPassword;
        private readonly Uri _couchBasePools;

        public CouchbaseAuthenticatedViewTests()
        {
            _bucketName = ConfigurationManager.AppSettings["SaslBucketName"];
            _bucketPassword = ConfigurationManager.AppSettings["SaslBucketPassword"];
            _couchBasePools = new Uri(ConfigurationManager.AppSettings["CouchbaseServerUrl"] + "/pools");
        }

        [TearDown]
        public void TearDown()
        {
            //clean up clients to avoid leakage
            if (_client != null)
            {
                _client.Dispose();
            }
        }

        /// <summary>
        /// @test: Verifies that for the bucket which is authenticated with password, it is able to connect successfully
        /// if correct cedentials are provided and then returns the object representing the view in specfied design document
        /// @pre: Provide the bucket name and password in method GetClient(),
        /// provide correct design name and view name to method GetView()
        /// @post: Test passes if connects to client successfully and get the details of View with given data, fails otherwise
        /// </summary>
        [Test]
        public void When_Bucket_Is_Authenticated_View_Returns_Results()
        {
            _client = GetClient(_bucketName, _bucketPassword);
            var view = _client.GetView("cities", "by_name");

            //Whether or view has data is regardless, what matters is that a authenticated call returned succesfully
            Assert.IsNotNull(view.FirstOrDefault()); 
        }

        /// <summary>
        /// @test: Verifies that for the bucket which is authenticated with password, it throws an error
        /// if no credentials are provided and hence cannot return the object representing the view in specfied design document
        /// @pre: Provide the bucket name but no password in method GetClient(),
        /// provide design name and view name to method GetView()
        /// @post: Test passes if the web exception (401) is thrown
        /// </summary>
		[ExpectedException(typeof(InvalidOperationException))]
		[Test]
		public void When_Bucket_Is_Authenticated_And_No_Credentials_Are_Provided_Exception_Is_Thrown()
		{
			_client = GetClient(_bucketName, "");
            var view = _client.GetView("cities", "by_name");

            var row = view.FirstOrDefault();
            Assert.IsNotNull(row);
		}

		/// <summary>
		/// @test: Verifies that for the bucket which is authenticated with password, it throws an error
		/// if bad credentials are provided and hence cannot return the object representing the view in specfied design document
		/// @pre: Provide the bucket name but no password in method GetClient(),
		/// provide design name and view name to method GetView()
		/// @post: Test passes if the web exception (401) is thrown
		/// </summary>
		[ExpectedException(typeof(InvalidOperationException))]
		[Test]
		public void When_Bucket_Is_Authenticated_And_Bad_Credentials_Are_Provided_Exception_Is_Thrown()
		{
			_client = GetClient(_bucketName, "bad_credentials");
			var view = _client.GetView("cities", "by_name");

		    var row = view.FirstOrDefault();
            Assert.IsNotNull(row);
		}

        /// <summary>
        /// Connects to couchbase client with given configuration details
        /// </summary>
        /// <param name="bucketName">Name of bucket to be used</param>
        /// <param name="bucketPassword">Password used to connect to bucket</param>
        /// <returns>Couchbase client object</returns>
		private CouchbaseClient GetClient(string bucketName, string bucketPassword)
		{
			var config = new CouchbaseClientConfiguration();
            config.Urls.Add(_couchBasePools);
			config.Bucket = bucketName;
			config.BucketPassword = bucketPassword;
            config.DesignDocumentNameTransformer = new ProductionModeNameTransformer();
			return new CouchbaseClient(config);
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

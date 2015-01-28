﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Couchbase.Configuration;
using System.Configuration;

namespace Couchbase.Tests
{
    [TestFixture]
    public class CouchbaseClientConfigTests
    {
        /// <summary>
        /// @test: Reads the information about couchbase client configuration from App.Config,
        /// and verifies that all properties are set
        /// @pre: Add section named "couchbase" in App.config file,
        /// configure all the parameters required to initialize Couchbase client like Uri, bucket, username, password etc.
        /// @post: Test passes if able to retrieve couchbase client configuration, the username and password match with
        /// expected details, fails otherwise
        /// </summary>
        [Test]
        public void When_Setting_Username_And_Password_In_Config_Section_Config_Instance_Properties_Are_Set()
        {
            var config = ConfigurationManager.GetSection("couchbase") as CouchbaseClientSection;
            Assert.That(config, Is.Not.Null, "Config was null");
            Assert.That(config.Servers.Username, Is.StringMatching(ConfigurationManager.AppSettings["UserNameForLoginToCouchbaseServer"]));
            Assert.That(config.Servers.Password, Is.StringMatching(ConfigurationManager.AppSettings["UserPasswordForLoginToCouchbaseServer"]));
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
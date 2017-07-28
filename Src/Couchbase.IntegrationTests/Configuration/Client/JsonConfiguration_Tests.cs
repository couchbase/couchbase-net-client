using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Configuration.Client
{
    [TestFixture]
    // ReSharper disable once InconsistentNaming
    public class JsonConfiguration_Tests
    {
        [Test]
        public void ClientConfiguration_IgnoreHostnameValidation_IsTrue()
        {
            //arrange/act
            var clientConfig = Utils.TestConfiguration.GetConfiguration("ssl");

            //assert
            Assert.IsTrue(ClientConfiguration.IgnoreRemoteCertificateNameMismatch);
        }

        [Test]
        public void ClientConfiguration_IgnoreHostnameValidation_IsFalse()
        {
            //arrange/act
            var clientConfig = Utils.TestConfiguration.GetConfiguration("multiplexio");

            //assert
            Assert.IsFalse(ClientConfiguration.IgnoreRemoteCertificateNameMismatch);
        }

        [Test]
        public void ClientConfiguration_VBucketRetrySleepTime_DefaultsTo100ms()
        {
            var config = Utils.TestConfiguration.GetConfiguration("basic");

            Assert.AreEqual(100, config.VBucketRetrySleepTime);
        }

        [Test]
        public void ClientConfiguration_VBucketRetrySleepTime_Is200ms()
        {
            var config = Utils.TestConfiguration.GetConfiguration("multiplexio");

            Assert.AreEqual(200, config.VBucketRetrySleepTime);
        }

        [Test]
        public void ClientConfiguration_Bucketconfigs_AreLoaded_From_Config()
        {
            // NOTE: Do not change the CouchbaseClientDefinition's Buckets property from
            // List<BucketConfiguration> to List<IBucketConfiguration>.
            // The ConfigurationProvider and ConfigurationBinder do not support interface
            // types when binding.

            var config = Utils.TestConfiguration.GetConfiguration("basic");

            Assert.AreEqual(1, config.BucketConfigs.Count);
            Assert.IsTrue(config.BucketConfigs.ContainsKey("default"));
        }

        [Test]
        public void ClientConfiguration_Username_Password_From_Config()
        {
            var config = Utils.TestConfiguration.GetConfiguration("secure");
            var authenticator = config.Authenticator as PasswordAuthenticator;

            Assert.IsNotNull(authenticator);
            Assert.AreEqual("CustomUser", authenticator.Username);
            Assert.AreEqual("secure123", authenticator.Password);
        }

        [Test]
        public void ClientConfiguration_Username_Password_From_Config_And_ConnectionString()
        {
            var config = Utils.TestConfiguration.GetConfiguration("secureConnectionString");
            var authenticator = config.Authenticator as PasswordAuthenticator;

            Assert.IsNotNull(authenticator);
            Assert.AreEqual("CustomUser", authenticator.Username);
            Assert.AreEqual("secure123", authenticator.Password);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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

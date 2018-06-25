using System.IO;
using System.Text;
using Couchbase.Configuration.Client;
using Couchbase.Tests.Documents;
using Couchbase.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Couchbase.Tests.Views
{
    [TestFixture]
    class JsonDataMapperTests
    {

        [Test]
        public void Should_Hydrate_Poco_In_PascalCase_Whatever_The_Case_In_Json()
        {
            const string jsonData = "{ \"SomeProperty\": \"SOME\", \"someIntProperty\": 12345, \"haspAscalCASE\": true }";
            var mapper = new JsonDataMapper(new ClientConfiguration());
            var hydrated = mapper.Map<Pascal>(new MemoryStream(Encoding.UTF8.GetBytes(jsonData)));

            Assert.AreEqual("SOME", hydrated.SomeProperty);
            Assert.AreEqual(12345, hydrated.SomeIntProperty);
            Assert.AreEqual(true, hydrated.HasPascalCase);
        }

        [Test]
        public void Should_Convert_To_CamelCase_Json_With_Default_Client_Configuration_Serialization_Settings()
        {
            var data = new Pascal
            {
                SomeProperty = "SOME",
                SomeIntProperty = 12345,
                HasPascalCase = true
            };
            const string expectedJson = "{\"someProperty\":\"SOME\",\"someIntProperty\":12345,\"hasPascalCase\":true}";
#pragma warning disable 618
            var actualJson = JsonConvert.SerializeObject(data, new ClientConfiguration().SerializationSettings);
#pragma warning restore 618

            Assert.AreEqual(expectedJson, actualJson);
        }

        [Test]
        public void Should_Convert_To_PascalCase_Json_With_Altered_Serialization_Settings()
        {
            var data = new Pascal
            {
                SomeProperty = "SOME",
                SomeIntProperty = 12345,
                HasPascalCase = true
            };
            const string expectedJson = "{\"SomeProperty\":\"SOME\",\"SomeIntProperty\":12345,\"HasPascalCase\":true}";
#pragma warning disable 618
            var serializationSetting = new ClientConfiguration().SerializationSettings;
#pragma warning restore 618
            serializationSetting.ContractResolver = new DefaultContractResolver();
            var actualJson = JsonConvert.SerializeObject(data, serializationSetting);

            Assert.AreEqual(expectedJson, actualJson);
        }

    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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
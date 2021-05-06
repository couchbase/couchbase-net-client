using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class JTokenExtensionsTests
    {
        [Fact]
        public void Test_GetTokenValue()
        {
            var jObject = JObject.Parse("{\"name\": \"bill\",\"maxTtl\": 123,\"flush\": true}");
            Assert.Equal("bill", jObject.GetTokenValue<string>("name"));
            Assert.Equal(123, jObject.GetTokenValue<int>("maxTtl"));
            Assert.True(jObject.GetTokenValue<bool>("flush"));
        }

        [Fact]
        public void Test_GetTokenValue_Default()
        {
            var jObject = JObject.Parse("{\"dog\": \"woof\",\"age\": 12,\"walked\": true}");
            Assert.Null(jObject.GetTokenValue<string>("name"));
            Assert.Equal(0, jObject.GetTokenValue<int>("maxTtl"));
            Assert.False(jObject.GetTokenValue<bool>("flush"));
        }
    }
}

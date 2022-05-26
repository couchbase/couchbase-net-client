using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.Operations;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Couchbase.UnitTests.Query
{
    public class QueryErrorContextTests
    {
        [Fact]
        public void Test_MixedJson_ToString()
        {
            var ctx = new QueryErrorContext
            {
                ClientContextId = "contextId",
                HttpStatus = System.Net.HttpStatusCode.InternalServerError,
                Errors = new System.Collections.Generic.List<Couchbase.Query.Error>
                {
                    new Couchbase.Query.Error()
                    {
                        Code = 8675309,
                        Message = "example error",
                        AdditionalData = new Dictionary<string, object>()
                        {
                            { "foobar", Newtonsoft.Json.Linq.JObject.Parse("{ \"example\": 1 }") }
                        }
                    }
                }
            };

            var json = ctx.ToString();
            Assert.Contains("foobar", json);

            var roundTrip = JsonSerializer.Deserialize<KeyValueErrorContext>(json, Couchbase.Core.InternalSerializationContext.Default.KeyValueErrorContext);
            Assert.Equal(ctx.ClientContextId, roundTrip.ClientContextId);
        }
    }
}

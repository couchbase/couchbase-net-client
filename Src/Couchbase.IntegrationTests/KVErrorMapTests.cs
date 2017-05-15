using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IntegrationTests.Utils;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    [Ignore("Only supported on cluster 5.0+ or CouchbaseMock")]
    public class KVErrorMapTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void UseKvErrorMap_Retuns_True_When_KVErrorMap_Is_Enabled(bool enabled)
        {
            var config = TestConfiguration.GetConfiguration("basic");
            config.BucketConfigs = new Dictionary<string, BucketConfiguration>
            {
                {"default", new BucketConfiguration {UseKvErrorMap = enabled}}
            };

            var cluster = new Cluster(config);
            var bucket = cluster.OpenBucket("default");
            Assert.AreEqual(enabled, bucket.SupportsKvErrorMap);
        }

        [TestCase("7ff0")] // constant
        [TestCase("7ff1")] // linear
        [TestCase("7ff2")] // exponential
        public void Test_Operation_Retry_Strategys(string code)
        {
            // convert hex error code into integerq.d`nxbOBO9XL`N, .,        /
            var errorCode = int.Parse(code, NumberStyles.AllowHexSpecifier);

            // boostrap client
            var config = TestConfiguration.GetConfiguration("basic");
            config.BucketConfigs = new Dictionary<string, BucketConfiguration>
            {
                {"default", new BucketConfiguration {UseKvErrorMap = true}}
            };

            using (var cluster = new Cluster(config))
            {
                var bucket = cluster.OpenBucket("default");

                // preload document to ensure everything is connected and working as expected
                const string documentKey = "hello";
                var upsertResult = bucket.Upsert(documentKey, new { });
                Assert.IsTrue(upsertResult.Success);

                // Get server index for the key
                var vbucket = (bucket as CouchbaseBucket).GetKeyMapper().MapKey(documentKey) as IVBucket;
                var serverIndex = vbucket.Primary;

                // get cb mock uri
                var serer = config.Servers.First();
                var serverUri = string.Format("{0}://{1}", serer.Scheme, serer.Authority);

                using (var client = new HttpClient())
                {
                    try
                    {
                        // enable retry behaviour
                        var response = client.GetAsync(string.Format("{0}/mock/start_retry_verify?idx={1}&bucket=default", serverUri, serverIndex)).Result;
                        Assert.IsTrue(CheckContentIsValid(response));

                        // setup opcode failures
                        response = client.GetAsync(string.Format("{0}/mock/opfail?servers=[{1}]&bucket=default&count=-1&code={2}", serverUri, serverIndex, errorCode)).Result;
                        Assert.IsTrue(CheckContentIsValid(response));

                        // execute get operation - should fail with expected retry behaviour
                        var result = bucket.Get<dynamic>(documentKey);
                        Assert.IsFalse(result.Success);

                        // verify behaviour
                        response = client.GetAsync(string.Format("{0}/mock/check_retry_verify?idx={1}&bucket=default&opcode=0&errcode={2}&fuzz_ms=20", serverUri, serverIndex, errorCode)).Result;
                        Assert.IsTrue(CheckContentIsValid(response));
                    }
                    finally
                    {
                        // stop opfail failures
                        var response = client.GetAsync(string.Format("{0}/mock/opfail?servers=[{1}]&bucket=default&count=0&code=0", serverUri, serverIndex)).Result;
                        Assert.IsTrue(CheckContentIsValid(response));
                    }
                }
            }
        }

        private static bool CheckContentIsValid(HttpResponseMessage message)
        {
            dynamic result;
            try
            {
                var json = message.Content.ReadAsStringAsync().Result;
                result = JsonConvert.DeserializeObject<dynamic>(json);
            }
            catch
            {
                return false;
            }

            if (result.status != "ok")
            {
                Assert.Fail(result.error.ToString());
            }

            return true;
        }

    }
}

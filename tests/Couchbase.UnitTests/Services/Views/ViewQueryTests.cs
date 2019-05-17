using System;
using Couchbase.Services.Views;
using Xunit;

namespace Couchbase.UnitTests.Services.Views
{
    public class ViewQueryTests
    {
        [Fact]
        public void When_Development_True_DesignDoc_Has_dev_Prefix()
        {
            var expected = new Uri("http://127.0.0.1:8092/default/_design/dev_cities/_view/by_name");
            const string baseUri = "http://127.0.0.1:8092";
            var query = new ViewQuery(baseUri).
                Bucket("default").
                DesignDoc("cities").
                View("by_name").
                Development(true);

            Assert.Equal(expected, query.RawUri());
        }

        [Fact]
        public void Test_Build_Basic_Query()
        {
            var expected = new Uri("http://127.0.0.1:8092/default/_design/cities/_view/by_name");
            const string baseUri = "http://127.0.0.1:8092";
            var query = new ViewQuery(baseUri).
                Bucket("default").
                DesignDoc("cities").
                View("by_name");

            Assert.Equal(expected, query.RawUri());
        }

        [Fact]
        public void Test_Build_Basic_Query_Using_From()
        {
            var expected = new Uri("http://127.0.0.1:8092/default/_design/cities/_view/by_name");
            const string baseUri = "http://127.0.0.1:8092";
            var query = new ViewQuery("default", baseUri).
                From("cities", "by_name");

            Assert.Equal(expected, query.RawUri());
        }

        [Fact]
        public void Test_Build_Basic_Query_Using_From_Limit_10()
        {
            var expected = new Uri("http://127.0.0.1:8092/default/_design/cities/_view/by_name?limit=10");
            const string baseUri = "http://127.0.0.1:8092";
            var query = new ViewQuery("default", baseUri).
                From("cities", "by_name").
                Limit(10);

            Assert.Equal(expected, query.RawUri());
        }

        [Fact]
        public void When_BaseUri_Returns_BucketName_And_UUID_Bucket_Property_IsIgnored()
        {
            const string expected = "http://192.168.56.102:8092/beer-sample%2B179b38da638e51deee5bcf5be82d2093/_design/beer/_view/brewery_beers";
            const string baseUriWithUuid = "http://192.168.56.102:8092/";

            var actual = new ViewQuery(baseUriWithUuid).
                From("beer", "brewery_beers").
                Bucket("beer-sample%2B179b38da638e51deee5bcf5be82d2093");

            Assert.Equal(new Uri(expected), actual.RawUri());
        }

        [Fact]
        public void Test_Build_Basic_Query_Using_From_Limit_10_And_Start_and_EndKeys_With_Encode_False()
        {
            var expected = new Uri("http://127.0.0.1:8092/beer-sample/_design/beer/_view/brewery_beers?endkey=[\"aass_brewery\"]&limit=10&startkey=[\"21st_amendment_brewery_cafe\"]");
            const string baseUri = "http://127.0.0.1:8092";
            var query = new ViewQuery("beer-sample", baseUri).
                From("beer", "brewery_beers").
                StartKey("[\"21st_amendment_brewery_cafe\"]", false).
                EndKey("[\"aass_brewery\"]", false).
                Limit(10);
            var uri = query.RawUri();
            Assert.Equal(expected, uri);
        }


        [Fact]
        public void Test_Build_Basic_Query_Using_From_Limit_10_And_Start_and_EndKeys_2()
        {
            var expected = new Uri("http://127.0.0.1:8092/default/_design/test/_view/test_view?stale=update_after&endkey=\"doc3\"&limit=10&startkey=\"doc2\"");
            const string baseUri = "http://127.0.0.1:8092";
            var query = new ViewQuery("default", baseUri).
                From("test", "test_view").
                StartKey("doc2").
                EndKey("doc3").
                Stale(StaleState.UpdateAfter).
                Limit(10);
            var uri = query.RawUri();
            Assert.Equal(expected, uri);
        }
    }
}

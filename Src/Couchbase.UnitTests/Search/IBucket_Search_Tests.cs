using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Search;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    // ReSharper disable once InconsistentNaming
    public class IBucket_Search_Tests
    {
        [Test]
        public void Query_ExampleAPI_Test()
        {
            var searchResult = new Mock<ISearchQueryResult>();
            var bucketMock = new Mock<IBucket>();
            bucketMock.Setup(x => x.Query(It.IsAny<SearchQuery>())).Returns(searchResult.Object);

            var bucket = bucketMock.Object;
            var searchQuery = new Mock<IFtsQuery>().Object;
            var searchParams = new Mock<ISearchParams>().Object;

            var searchQueryResult = bucket.Query(new SearchQuery
            {
                Index = "foo",
                SearchParams = searchParams,
                Query = searchQuery
            });
        }
    }
}

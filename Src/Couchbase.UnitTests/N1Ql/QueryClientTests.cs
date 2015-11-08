using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.N1QL;
using Couchbase.Views;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.N1Ql
{
    [TestFixture]
    public class QueryClientTests
    {
        #region GetDataMapper

        [Test]
        public void GetDataMapper_IQueryRequest_ReturnsClientDataMapper()
        {
            // Arrange

            var dataMapper = new Mock<IDataMapper>();

            var queryRequest = new Mock<IQueryRequest>();

            var queryClient = new QueryClient(new HttpClient(), dataMapper.Object, new ClientConfiguration(),
                new ConcurrentDictionary<string, QueryPlan>());

            // Act

            var result = queryClient.GetDataMapper(queryRequest.Object);

            // Assert

            Assert.AreEqual(dataMapper.Object, result);
        }

        [Test]
        public void GetDataMapper_IQueryRequestWithDataMapper_NoDataMapper_ReturnsClientDataMapper()
        {
            // Arrange

            var clientDataMapper = new Mock<IDataMapper>();

            var queryRequest = new Mock<IQueryRequestWithDataMapper>();
            queryRequest.SetupProperty(p => p.DataMapper, null);

            var queryClient = new QueryClient(new HttpClient(), clientDataMapper.Object, new ClientConfiguration(),
                new ConcurrentDictionary<string, QueryPlan>());

            // Act

            var result = queryClient.GetDataMapper(queryRequest.Object);

            // Assert

            Assert.AreEqual(clientDataMapper.Object, result);
        }

        [Test]
        public void GetDataMapper_IQueryRequestWithDataMapper_HasDataMapper_ReturnsRequestDataMapper()
        {
            // Arrange

            var clientDataMapper = new Mock<IDataMapper>();
            var requestDataMapper = new Mock<IDataMapper>();

            var queryRequest = new Mock<IQueryRequestWithDataMapper>();
            queryRequest.SetupProperty(p => p.DataMapper, requestDataMapper.Object);

            var queryClient = new QueryClient(new HttpClient(), clientDataMapper.Object, new ClientConfiguration(),
                new ConcurrentDictionary<string, QueryPlan>());

            // Act

            var result = queryClient.GetDataMapper(queryRequest.Object);

            // Assert

            Assert.AreEqual(requestDataMapper.Object, result);
        }

        #endregion
    }
}

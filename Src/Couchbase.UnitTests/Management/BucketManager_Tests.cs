﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Management;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Management
{
    [TestFixture]
    public class BucketManagerTests
    {
        private IBucket _mockBucket;
        private ClientConfiguration _clientConfiguration;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var mockBucket = new Mock<IBucket>();
            mockBucket.SetupGet(m => m.Name).Returns("test");
            _mockBucket = mockBucket.Object;

            _clientConfiguration = new ClientConfiguration()
            {
                BucketConfigs = new Dictionary<string, BucketConfiguration>()
                {
                    { "test", new BucketConfiguration() }
                }
            };
        }

        #region GetDesignDocument

        [Test]
        public void GetDesignDocument_WhenSuccessful_ReturnsSuccess()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("response")
                });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
            managerMock.Setup(x => x.GetDesignDocument(It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.GetDesignDocumentAsync(It.IsAny<string>())).CallBase();

            // Act

            var result = managerMock.Object.GetDesignDocument("test");

            // Assert

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void GetDesignDocument_WhenFails_ReturnsFailure()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
                new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("response")
                });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
            managerMock.Setup(x => x.GetDesignDocument(It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.GetDesignDocumentAsync(It.IsAny<string>())).CallBase();

            // Act

            var result = managerMock.Object.GetDesignDocument("test");

            // Assert

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void GetDesignDocument_ThrowsException_ReturnsFailure()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
            {
                throw new AggregateException();
            });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
            managerMock.Setup(x => x.GetDesignDocument(It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.GetDesignDocumentAsync(It.IsAny<string>())).CallBase();

            // Act

            var result = managerMock.Object.GetDesignDocument("test");

            // Assert

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.Exception);
            Assert.IsAssignableFrom<AggregateException>(result.Exception);
        }

        #endregion

        #region GetDesignDocuments

        [Test]
        public void GetDesignDocuments_WhenSuccessful_ReturnsSuccess()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("response")
                });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
#pragma warning disable 618
            managerMock.Setup(x => x.GetDesignDocuments(It.IsAny<bool>())).CallBase();
#pragma warning restore 618
            managerMock.Setup(x => x.GetDesignDocumentsAsync(It.IsAny<bool>())).CallBase();

            // Act

#pragma warning disable 618
            var result = managerMock.Object.GetDesignDocuments();
#pragma warning restore 618

            // Assert

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void GetDesignDocuments_WhenFails_ReturnsFailure()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
                new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("response")
                });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
#pragma warning disable 618
            managerMock.Setup(x => x.GetDesignDocuments(It.IsAny<bool>())).CallBase();
#pragma warning restore 618
            managerMock.Setup(x => x.GetDesignDocumentsAsync(It.IsAny<bool>())).CallBase();

            // Act

#pragma warning disable 618
            var result = managerMock.Object.GetDesignDocuments();
#pragma warning restore 618

            // Assert

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void GetDesignDocuments_ThrowsException_ReturnsFailure()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
            {
                throw new AggregateException();
            });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
#pragma warning disable 618
            managerMock.Setup(x => x.GetDesignDocuments(It.IsAny<bool>())).CallBase();
#pragma warning restore 618
            managerMock.Setup(x => x.GetDesignDocumentsAsync(It.IsAny<bool>())).CallBase();

            // Act

#pragma warning disable 618
            var result = managerMock.Object.GetDesignDocuments();
#pragma warning restore 618

            // Assert

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.Exception);
            Assert.IsAssignableFrom<AggregateException>(result.Exception);
        }

        #endregion

        #region InsertDesignDocument

        [Test]
        public void InsertDesignDocument_WhenSuccessful_ReturnsSuccess()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("response")
                });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
            managerMock.Setup(x => x.InsertDesignDocument(It.IsAny<string>(), It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.InsertDesignDocumentAsync(It.IsAny<string>(), It.IsAny<string>())).CallBase();

            // Act

            var result = managerMock.Object.InsertDesignDocument("test", "document");

            // Assert

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void InsertDesignDocument_WhenFails_ReturnsFailure()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
                new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("response")
                });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
            managerMock.Setup(x => x.InsertDesignDocument(It.IsAny<string>(), It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.InsertDesignDocumentAsync(It.IsAny<string>(), It.IsAny<string>())).CallBase();

            // Act

            var result = managerMock.Object.InsertDesignDocument("test", "document");

            // Assert

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void InsertDesignDocument_ThrowsException_ReturnsFailure()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
            {
                throw new AggregateException();
            });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
            managerMock.Setup(x => x.InsertDesignDocument(It.IsAny<string>(), It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.InsertDesignDocumentAsync(It.IsAny<string>(), It.IsAny<string>())).CallBase();

            // Act

            var result = managerMock.Object.InsertDesignDocument("test", "document");

            // Assert

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.Exception);
            Assert.IsAssignableFrom<AggregateException>(result.Exception);
        }

        #endregion

        #region UpdateDesignDocument

        [Test]
        public void UpdateDesignDocument_WhenSuccessful_ReturnsSuccess()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("response")
                });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
            managerMock.Setup(x => x.UpdateDesignDocument(It.IsAny<string>(), It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.UpdateDesignDocumentAsync(It.IsAny<string>(), It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.InsertDesignDocument(It.IsAny<string>(), It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.InsertDesignDocumentAsync(It.IsAny<string>(), It.IsAny<string>())).CallBase();

            // Act

            var result = managerMock.Object.UpdateDesignDocument("test", "document");

            // Assert

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void UpdateDesignDocument_WhenFails_ReturnsFailure()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
                new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("response")
                });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
            managerMock.Setup(x => x.UpdateDesignDocument(It.IsAny<string>(), It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.UpdateDesignDocumentAsync(It.IsAny<string>(), It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.InsertDesignDocument(It.IsAny<string>(), It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.InsertDesignDocumentAsync(It.IsAny<string>(), It.IsAny<string>())).CallBase();

            // Act

            var result = managerMock.Object.UpdateDesignDocument("test", "document");

            // Assert

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void UpdateDesignDocument_ThrowsException_ReturnsFailure()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
            {
                throw new AggregateException();
            });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
            managerMock.Setup(x => x.UpdateDesignDocument(It.IsAny<string>(), It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.UpdateDesignDocumentAsync(It.IsAny<string>(), It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.InsertDesignDocument(It.IsAny<string>(), It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.InsertDesignDocumentAsync(It.IsAny<string>(), It.IsAny<string>())).CallBase();

            // Act

            var result = managerMock.Object.UpdateDesignDocument("test", "document");

            // Assert

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.Exception);
            Assert.IsAssignableFrom<AggregateException>(result.Exception);
        }

        #endregion

        #region RemoveDesignDocument

        [Test]
        public void RemoveDesignDocument_WhenSuccessful_ReturnsSuccess()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("response")
                });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
            managerMock.Setup(x => x.RemoveDesignDocument(It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.RemoveDesignDocumentAsync(It.IsAny<string>())).CallBase();

            // Act

            var result = managerMock.Object.RemoveDesignDocument("test");

            // Assert

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void RemoveDesignDocument_WhenFails_ReturnsFailure()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
                new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("response")
                });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
            managerMock.Setup(x => x.RemoveDesignDocument(It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.RemoveDesignDocumentAsync(It.IsAny<string>())).CallBase();

            // Act

            var result = managerMock.Object.RemoveDesignDocument("test");

            // Assert

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void RemoveDesignDocument_ThrowsException_ReturnsFailure()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
            {
                throw new AggregateException();
            });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
            managerMock.Setup(x => x.RemoveDesignDocument(It.IsAny<string>())).CallBase();
            managerMock.Setup(x => x.RemoveDesignDocumentAsync(It.IsAny<string>())).CallBase();

            // Act

            var result = managerMock.Object.RemoveDesignDocument("test");

            // Assert

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.Exception);
            Assert.IsAssignableFrom<AggregateException>(result.Exception);
        }

        #endregion

        #region Flush

        [Test]
        public void Flush_WhenSuccessful_ReturnsSuccess()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("response")
                });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
            managerMock.Setup(x => x.FlushAsync()).CallBase();

            // Act

            var result = managerMock.Object.Flush();

            // Assert

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Flush_WhenFails_ReturnsFailure()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
                new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("response")
                });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
            managerMock.Setup(x => x.FlushAsync()).CallBase();

            // Act

            var result = managerMock.Object.Flush();

            // Assert

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Flush_ThrowsException_ReturnsFailure()
        {
            // Arrange

            var handler = FakeHttpMessageHandler.Create(request =>
            {
                throw new AggregateException();
            });

            var managerMock = new Mock<BucketManager>(_mockBucket, _clientConfiguration,
                new JsonDataMapper(_clientConfiguration), new HttpClient(handler), "username", "password");
            managerMock.Setup(x => x.FlushAsync()).CallBase();

            // Act

            var result = managerMock.Object.Flush();

            // Assert

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.Exception);
            Assert.IsAssignableFrom<AggregateException>(result.Exception);
        }

        #endregion
    }
}
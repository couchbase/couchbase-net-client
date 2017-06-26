using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server;
using Couchbase.Management;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Management
{
    [TestFixture]
    public class ClusterManagerTests
    {
        private ClientConfiguration _clientConfiguration;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _clientConfiguration = new ClientConfiguration()
            {
                BucketConfigs = new Dictionary<string, BucketConfiguration>()
                {
                    { "test", new BucketConfiguration() }
                }
            };
        }

        #region CreateBucket

        [Test]
        public void CreateBucket_FlushEnabledTrue_SendsWithCorrectParameter()
        {
            // Arrange

            var mockServerConfig = new Mock<IServerConfig>();

            var managerMock = new Mock<ClusterManager>(_clientConfiguration, mockServerConfig.Object,
                new JsonDataMapper(_clientConfiguration), new HttpClient(), "username", "password");
            managerMock
                .Setup(x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["flushEnabled"] == "1")))
                .Returns(Task.FromResult((IResult) new DefaultResult(true, "success", null)));

            // Act

            managerMock.Object.CreateBucket("test", flushEnabled: true);

            // Assert

            managerMock.Verify(
                x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["flushEnabled"] == "1")),
                Times.Once);
        }

        [Test]
        public void CreateBucket_FlushEnabledFalse_SendsWithCorrectParameter()
        {
            // Arrange

            var mockServerConfig = new Mock<IServerConfig>();

            var managerMock = new Mock<ClusterManager>(_clientConfiguration, mockServerConfig.Object,
                new JsonDataMapper(_clientConfiguration), new HttpClient(), "username", "password");
            managerMock
                .Setup(x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["flushEnabled"] == "0")))
                .Returns(Task.FromResult((IResult)new DefaultResult(true, "success", null)));

            // Act

            managerMock.Object.CreateBucket("test", flushEnabled: false);

            // Assert

            managerMock.Verify(
                x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["flushEnabled"] == "0")),
                Times.Once);
        }

        [Test]
        public void CreateBucket_IndexReplicasTrue_SendsWithCorrectParameter()
        {
            // Arrange

            var mockServerConfig = new Mock<IServerConfig>();

            var managerMock = new Mock<ClusterManager>(_clientConfiguration, mockServerConfig.Object,
                new JsonDataMapper(_clientConfiguration), new HttpClient(), "username", "password");
            managerMock
                .Setup(x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["replicaIndex"] == "1")))
                .Returns(Task.FromResult((IResult)new DefaultResult(true, "success", null)));

            // Act

            managerMock.Object.CreateBucket("test", indexReplicas: true);

            // Assert

            managerMock.Verify(
                x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["replicaIndex"] == "1")),
                Times.Once);
        }

        [Test]
        public void CreateBucket_IndexReplicasFalse_SendsWithCorrectParameter()
        {
            // Arrange

            var mockServerConfig = new Mock<IServerConfig>();

            var managerMock = new Mock<ClusterManager>(_clientConfiguration, mockServerConfig.Object,
                new JsonDataMapper(_clientConfiguration), new HttpClient(), "username", "password");
            managerMock
                .Setup(x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["replicaIndex"] == "0")))
                .Returns(Task.FromResult((IResult)new DefaultResult(true, "success", null)));

            // Act

            managerMock.Object.CreateBucket("test", indexReplicas: false);

            // Assert

            managerMock.Verify(
                x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["replicaIndex"] == "0")),
                Times.Once);
        }

        #endregion

        #region UserManagement

        [TestCase(HttpStatusCode.OK)]
        [TestCase(HttpStatusCode.BadRequest)]
        [TestCase(HttpStatusCode.InternalServerError)]
        public void UpsertUser_Returns_True_When_Response_Is_Success(HttpStatusCode responseHttpCode)
        {
            var handler = FakeHttpMessageHandler.Create(request => new HttpResponseMessage(responseHttpCode));
            var client = new HttpClient(handler);
            var clientConfig = new ClientConfiguration();
            var serverConfigMock = new Mock<IServerConfig>();
            var dataMapper = new JsonDataMapper(clientConfig);

            var manager = new ClusterManager(clientConfig, serverConfigMock.Object, dataMapper, client, "username", "password");
            var result = manager.UpsertUser("alice", "secure123", "Alice Liddell", new Role {Name = "Admin"});

            Assert.AreEqual(responseHttpCode == HttpStatusCode.OK, result.Success);
        }

        [Test]
        public void UpsertUser_Formats_FormValues()
        {
            const string expectedFormValueString = "password=secure123&name=Alice+Liddell&roles=Admin%2CBucketManager%5Bdefault%5D";

            var handler = FakeHttpMessageHandler.Create(request =>
            {
                Assert.AreEqual(expectedFormValueString, request.Content.ReadAsStringAsync().Result);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
            var client = new HttpClient(handler);
            var clientConfig = new ClientConfiguration();
            var serverConfigMock = new Mock<IServerConfig>();
            var dataMapper = new JsonDataMapper(clientConfig);

            var manager = new ClusterManager(clientConfig, serverConfigMock.Object, dataMapper, client, "username", "password");
            manager.UpsertUser("alice", "secure123", "Alice Liddell", new Role { Name = "Admin" }, new Role { Name = "BucketManager", BucketName = "default"});
        }

        [Test]
        public void UpsertUser_Builds_Correct_Uri()
        {
            const string expectedFormValueString = "http://localhost:8091/settings/rbac/users/local/alice";

            var handler = FakeHttpMessageHandler.Create(request =>
            {
                Assert.AreEqual(expectedFormValueString, request.RequestUri.OriginalString);
                Assert.AreEqual(HttpMethod.Put, request.Method);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
            var client = new HttpClient(handler);
            var clientConfig = new ClientConfiguration();
            var serverConfigMock = new Mock<IServerConfig>();
            var dataMapper = new JsonDataMapper(clientConfig);

            var manager = new ClusterManager(clientConfig, serverConfigMock.Object, dataMapper, client, "username", "password");
            manager.UpsertUser("alice", "secure123", "Alice Liddell", new Role { Name = "Admin" }, new Role { Name = "BucketManager", BucketName = "default" });
        }

        [TestCase(HttpStatusCode.OK, true)]
        [TestCase(HttpStatusCode.BadRequest, false)]
        [TestCase(HttpStatusCode.InternalServerError, false)]
        public void RemoveUser_Returns_True_When_Response_Is_Success(HttpStatusCode responseHttpCode, bool expectedResult)
        {
            var handler = FakeHttpMessageHandler.Create(request => new HttpResponseMessage(responseHttpCode));
            var client = new HttpClient(handler);
            var clientConfig = new ClientConfiguration();
            var serverConfigMock = new Mock<IServerConfig>();
            var dataMapper = new JsonDataMapper(clientConfig);

            var manager = new ClusterManager(clientConfig, serverConfigMock.Object, dataMapper, client, "username", "password");
            var result = manager.RemoveUser("alice");

            Assert.AreEqual(expectedResult, result.Success);
        }

        [Test]
        public void RemoveUser_Builds_Correct_Uri()
        {
            const string expectedFormValueString = "http://localhost:8091/settings/rbac/users/local/alice";

            var handler = FakeHttpMessageHandler.Create(request =>
            {
                Assert.AreEqual(expectedFormValueString, request.RequestUri.OriginalString);
                Assert.AreEqual(HttpMethod.Delete, request.Method);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
            var client = new HttpClient(handler);
            var clientConfig = new ClientConfiguration();
            var serverConfigMock = new Mock<IServerConfig>();
            var dataMapper = new JsonDataMapper(clientConfig);

            var manager = new ClusterManager(clientConfig, serverConfigMock.Object, dataMapper, client, "username", "password");
            manager.RemoveUser("alice");
        }

        [TestCase(HttpStatusCode.OK)]
        [TestCase(HttpStatusCode.BadRequest)]
        [TestCase(HttpStatusCode.InternalServerError)]
        public void GetUsers_Returns_Users_When_Response_Is_Success(HttpStatusCode responseHttpCode)
        {
            var expectedUsers = new List<User>
            {
                new User
                {
                    Username = "alice",
                    Name = "Alice Liddell",
                    Domain = "builtin",
                    Roles = new List<Role> {new Role {Name = "Admin"}}
                },
                new User
                {
                    Username = "rabbit",
                    Name = "White Rabbit",
                    Domain = "builtin",
                    Roles = new List<Role> {new Role {Name = "BucketManager", BucketName = "default"}}
                },
                new User
                {
                    Username = "hatter",
                    Name = "Mad Hatter",
                    Domain = "builtin",
                    Roles = new List<Role> {new Role {Name = "FTSAdmin"}}
                }
            };

            var handler = FakeHttpMessageHandler.Create(
                request => new HttpResponseMessage(responseHttpCode)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(expectedUsers))
                }
            );
            var client = new HttpClient(handler);
            var clientConfig = new ClientConfiguration();
            var serverConfigMock = new Mock<IServerConfig>();
            var dataMapper = new JsonDataMapper(clientConfig);

            var manager = new ClusterManager(clientConfig, serverConfigMock.Object, dataMapper, client, "username", "password");
            var result = manager.GetUsers();

            if (responseHttpCode == HttpStatusCode.OK)
            {
                Assert.IsTrue(result.Success);
                Assert.AreEqual(expectedUsers.Count, result.Value.Count());
            }
            else
            {
                Assert.IsFalse(result.Success);
                Assert.IsNull(result.Value);
            }
        }

        [Test]
        public void GetUsers_Builds_Correct_Uri()
        {
            const string expectedFormValueString = "http://localhost:8091/settings/rbac/users/local";

            var handler = FakeHttpMessageHandler.Create(request =>
            {
                Assert.AreEqual(expectedFormValueString, request.RequestUri.OriginalString);
                Assert.AreEqual(HttpMethod.Get, request.Method);
                var response = new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent("")};
                return response;
            });
            var client = new HttpClient(handler);
            var clientConfig = new ClientConfiguration();
            var serverConfigMock = new Mock<IServerConfig>();
            var dataMapper = new JsonDataMapper(clientConfig);

            var manager = new ClusterManager(clientConfig, serverConfigMock.Object, dataMapper, client, "username", "password");
            manager.GetUsers();
        }

        [TestCase(HttpStatusCode.OK)]
        [TestCase(HttpStatusCode.BadRequest)]
        [TestCase(HttpStatusCode.InternalServerError)]
        public void GetUser_Returns_Users_When_Response_Is_Success(HttpStatusCode responseHttpCode)
        {
            var user = new User
            {
                Username = "alice",
                Name = "Alice Liddell",
                Domain = "local",
                Roles = new List<Role> {new Role {Name = "Admin"}}
            };

            var handler = FakeHttpMessageHandler.Create(
                request => new HttpResponseMessage(responseHttpCode)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(user))
                }
            );
            var client = new HttpClient(handler);
            var clientConfig = new ClientConfiguration();
            var serverConfigMock = new Mock<IServerConfig>();
            var dataMapper = new JsonDataMapper(clientConfig);

            var manager = new ClusterManager(clientConfig, serverConfigMock.Object, dataMapper, client, "username", "password");
            var result = manager.GetUser(user.Username);

            if (responseHttpCode == HttpStatusCode.OK)
            {
                Assert.IsTrue(result.Success);
                Assert.AreEqual(user.Username, result.Value.Username);
                Assert.AreEqual(user.Name, result.Value.Name);
                Assert.AreEqual("local", result.Value.Domain);
            }
            else
            {
                Assert.IsFalse(result.Success);
                Assert.IsNull(result.Value);
            }
        }

        [Test]
        public void GetUser_Builds_Correct_Uri()
        {
            const string expectedFormValueString = "http://localhost:8091/settings/rbac/users/local/alice";

            var handler = FakeHttpMessageHandler.Create(request =>
            {
                Assert.AreEqual(expectedFormValueString, request.RequestUri.OriginalString);
                Assert.AreEqual(HttpMethod.Get, request.Method);
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
                return response;
            });
            var client = new HttpClient(handler);
            var clientConfig = new ClientConfiguration();
            var serverConfigMock = new Mock<IServerConfig>();
            var dataMapper = new JsonDataMapper(clientConfig);

            var manager = new ClusterManager(clientConfig, serverConfigMock.Object, dataMapper, client, "username", "password");
            manager.GetUser("alice");
        }

        #endregion
    }
}
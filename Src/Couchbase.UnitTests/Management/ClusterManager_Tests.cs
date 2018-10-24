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
using Couchbase.Utils;
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

        [Test]
        public void ConflictResolutionType_descriptions_are_correct()
        {
            Assert.AreEqual("seqno", ConflictResolutionType.SequenceNumber.GetDescription());
            Assert.AreEqual("lww", ConflictResolutionType.LastWriteWins.GetDescription());
        }

        [TestCase(ConflictResolutionType.SequenceNumber)]
        [TestCase(ConflictResolutionType.LastWriteWins)]
        public void CreateBucket_conflictResolutionType_is_sent_to_server(ConflictResolutionType conflictResolutionType)
        {
            // Arrange

            var mockServerConfig = new Mock<IServerConfig>();

            var managerMock = new Mock<ClusterManager>(_clientConfiguration, mockServerConfig.Object,
                new JsonDataMapper(_clientConfiguration), new HttpClient(), "username", "password");
            managerMock
                .Setup(x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["replicaIndex"] == "0")))
                .Returns(Task.FromResult((IResult)new DefaultResult(true, "success", null)));

            // Act

            managerMock.Object.CreateBucket("test", conflictResolutionType: conflictResolutionType);

            // Assert

            managerMock.Verify(
                x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["conflictResolutionType"] == conflictResolutionType.GetDescription())),
                Times.Once);
        }

        [TestCase(ConflictResolutionType.SequenceNumber)]
        [TestCase(ConflictResolutionType.LastWriteWins)]
        public void CreateBucket_with_proxy_port_conflictResolutionType_is_sent_to_server(ConflictResolutionType conflictResolutionType)
        {
            // Arrange

            var mockServerConfig = new Mock<IServerConfig>();

            var managerMock = new Mock<ClusterManager>(_clientConfiguration, mockServerConfig.Object,
                new JsonDataMapper(_clientConfiguration), new HttpClient(), "username", "password");
            managerMock
                .Setup(x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["replicaIndex"] == "0")))
                .Returns(Task.FromResult((IResult)new DefaultResult(true, "success", null)));

            // Act

            managerMock.Object.CreateBucket("test", 1234, conflictResolutionType: conflictResolutionType);

            // Assert

            managerMock.Verify(
                x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["conflictResolutionType"] == conflictResolutionType.GetDescription())),
                Times.Once);
        }

        [TestCase(ConflictResolutionType.SequenceNumber)]
        [TestCase(ConflictResolutionType.LastWriteWins)]
        public async Task CreateBucketAsync_conflictResolutionType_is_sent_to_server(ConflictResolutionType conflictResolutionType)
        {
            // Arrange

            var mockServerConfig = new Mock<IServerConfig>();

            var managerMock = new Mock<ClusterManager>(_clientConfiguration, mockServerConfig.Object,
                new JsonDataMapper(_clientConfiguration), new HttpClient(), "username", "password");
            managerMock
                .Setup(x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["replicaIndex"] == "0")))
                .Returns(Task.FromResult((IResult)new DefaultResult(true, "success", null)));

            // Act

            await managerMock.Object.CreateBucketAsync("test", conflictResolutionType: conflictResolutionType);

            // Assert

            managerMock.Verify(
                x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["conflictResolutionType"] == conflictResolutionType.GetDescription())),
                Times.Once);
        }

        [TestCase(ConflictResolutionType.SequenceNumber)]
        [TestCase(ConflictResolutionType.LastWriteWins)]
        public async Task CreateBucketAsync_with_proxy_port_conflictResolutionType_is_sent_to_server(ConflictResolutionType conflictResolutionType)
        {
            // Arrange

            var mockServerConfig = new Mock<IServerConfig>();

            var managerMock = new Mock<ClusterManager>(_clientConfiguration, mockServerConfig.Object,
                new JsonDataMapper(_clientConfiguration), new HttpClient(), "username", "password");
            managerMock
                .Setup(x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["replicaIndex"] == "0")))
                .Returns(Task.FromResult((IResult)new DefaultResult(true, "success", null)));

            // Act

            await managerMock.Object.CreateBucketAsync("test", 1234, conflictResolutionType: conflictResolutionType);

            // Assert

            managerMock.Verify(
                x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["conflictResolutionType"] == conflictResolutionType.GetDescription())),
                Times.Once);
        }

        [TestCase(ConflictResolutionType.SequenceNumber)]
        [TestCase(ConflictResolutionType.LastWriteWins)]
        public void CreateBucket_conflictResolutionType_using_bucket_settings_is_sent_to_server(ConflictResolutionType conflictResolutionType)
        {
            // Arrange

            var mockServerConfig = new Mock<IServerConfig>();

            var managerMock = new Mock<ClusterManager>(_clientConfiguration, mockServerConfig.Object,
                new JsonDataMapper(_clientConfiguration), new HttpClient(), "username", "password");
            managerMock
                .Setup(x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["replicaIndex"] == "0")))
                .Returns(Task.FromResult((IResult)new DefaultResult(true, "success", null)));

            // Act

            var settings = new BucketSettings
            {
                Name = "test",
                ConflictResolutionType = conflictResolutionType
            };
            managerMock.Object.CreateBucket(settings);

            // Assert

            managerMock.Verify(
                x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["conflictResolutionType"] == conflictResolutionType.GetDescription())),
                Times.Once);
        }

        [TestCase(ConflictResolutionType.SequenceNumber)]
        [TestCase(ConflictResolutionType.LastWriteWins)]
        public async Task CreateBucketAsync_conflictResolutionType_using_bucket_settings_is_sent_to_server(ConflictResolutionType conflictResolutionType)
        {
            // Arrange

            var mockServerConfig = new Mock<IServerConfig>();

            var managerMock = new Mock<ClusterManager>(_clientConfiguration, mockServerConfig.Object,
                new JsonDataMapper(_clientConfiguration), new HttpClient(), "username", "password");
            managerMock
                .Setup(x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["replicaIndex"] == "0")))
                .Returns(Task.FromResult((IResult)new DefaultResult(true, "success", null)));

            // Act

            var settings = new BucketSettings
            {
                Name = "test",
                ConflictResolutionType = conflictResolutionType
            };
            await managerMock.Object.CreateBucketAsync(settings);

            // Assert

            managerMock.Verify(
                x => x.PostFormDataAsync(It.IsAny<Uri>(), It.Is<Dictionary<string, string>>(p => p["conflictResolutionType"] == conflictResolutionType.GetDescription())),
                Times.Once);
        }

        #endregion

        #region UserManagement

        [TestCase(HttpStatusCode.OK)]
        [TestCase(HttpStatusCode.BadRequest)]
        [TestCase(HttpStatusCode.InternalServerError)]
        public void UpsertUser_Returns_True_When_Response_Is_Success(HttpStatusCode responseHttpCode)
        {
            const string responseBody = "respose body";
            var handler = FakeHttpMessageHandler.Create(request => new HttpResponseMessage(responseHttpCode)
            {
                Content = new StringContent(responseBody)
            });
            var client = new HttpClient(handler);
            var clientConfig = new ClientConfiguration();
            var serverConfigMock = new Mock<IServerConfig>();
            var dataMapper = new JsonDataMapper(clientConfig);

            var manager = new ClusterManager(clientConfig, serverConfigMock.Object, dataMapper, client, "username", "password");
            var result = manager.UpsertUser(AuthenticationDomain.Local, "alice", "secure123", "Alice Liddell", new Role {Name = "Admin"});

            Assert.AreEqual(responseHttpCode == HttpStatusCode.OK, result.Success);
            Assert.AreEqual(responseBody, result.Message);
        }

        [TestCase(null, "password=secure123&roles=Admin%2CBucketManager%5Bdefault%5D")]
        [TestCase("", "password=secure123&roles=Admin%2CBucketManager%5Bdefault%5D")]
        [TestCase("Alice Liddell", "password=secure123&roles=Admin%2CBucketManager%5Bdefault%5D&name=Alice+Liddell")]
        public void UpsertUser_Formats_FormValues(string name, string expectedFormValueString)
        {
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
            manager.UpsertUser(AuthenticationDomain.Local, "alice", "secure123", name, new Role { Name = "Admin" }, new Role { Name = "BucketManager", BucketName = "default"});
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
            manager.UpsertUser(AuthenticationDomain.Local, "alice", "secure123", "Alice Liddell", new Role { Name = "Admin" }, new Role { Name = "BucketManager", BucketName = "default" });
        }

        [TestCase(null)]
        [TestCase("")]
        public void UpsertUser_Throws_Exception_When_Password_Is_Empty_For_Local_User(string password)
        {
            var manager = new ClusterManager(null, null, null, null, "username", "password");

            // Throws AggregateException because work is done async
            Assert.Throws<AggregateException>(() =>
            {
                manager.UpsertUser(AuthenticationDomain.Local, "username", password, null, new Role());
            });
        }

        [TestCase(HttpStatusCode.OK, true)]
        [TestCase(HttpStatusCode.BadRequest, false)]
        [TestCase(HttpStatusCode.InternalServerError, false)]
        public void RemoveUser_Returns_True_When_Response_Is_Success(HttpStatusCode responseHttpCode, bool expectedResult)
        {
            const string responseBody = "respose body";
            var handler = FakeHttpMessageHandler.Create(request => new HttpResponseMessage(responseHttpCode)
            {
                Content = new StringContent(responseBody)
            });
            var client = new HttpClient(handler);
            var clientConfig = new ClientConfiguration();
            var serverConfigMock = new Mock<IServerConfig>();
            var dataMapper = new JsonDataMapper(clientConfig);

            var manager = new ClusterManager(clientConfig, serverConfigMock.Object, dataMapper, client, "username", "password");
            var result = manager.RemoveUser(AuthenticationDomain.Local, "alice");

            Assert.AreEqual(expectedResult, result.Success);
            Assert.AreEqual(responseBody, result.Message);
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
            manager.RemoveUser(AuthenticationDomain.Local, "alice");
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
            var result = manager.GetUsers(AuthenticationDomain.Local);

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
            manager.GetUsers(AuthenticationDomain.Local);
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
            var result = manager.GetUser(AuthenticationDomain.Local, user.Username);

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

        [TestCase(AuthenticationDomain.Local)]
        [TestCase(AuthenticationDomain.External)]
        public void GetUser_Builds_Correct_Uri(AuthenticationDomain domain)
        {
            var expextedRequestPath = string.Format(
                "http://localhost:8091/settings/rbac/users/{0}/alice",
                domain == AuthenticationDomain.Local ? "local" : "external"
            );

            var handler = FakeHttpMessageHandler.Create(request =>
            {
                Assert.AreEqual(expextedRequestPath, request.RequestUri.OriginalString);
                Assert.AreEqual(HttpMethod.Get, request.Method);
                var response = new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent("")};
                return response;
            });
            var client = new HttpClient(handler);
            var clientConfig = new ClientConfiguration();
            var serverConfigMock = new Mock<IServerConfig>();
            var dataMapper = new JsonDataMapper(clientConfig);

            var manager = new ClusterManager(clientConfig, serverConfigMock.Object, dataMapper, client, "username", "password");
            manager.GetUser(domain, "alice");
        }

        #endregion
    }
}

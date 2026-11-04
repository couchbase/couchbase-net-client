using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Logging;
using Couchbase.Management;
using Couchbase.Management.Buckets;
using Couchbase.Management.Eventing;
using Couchbase.Management.Users;
using Couchbase.UnitTests.Helpers;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Management;

/// <summary>
/// RFC 58: a management API raises <see cref="InvalidArgumentException"/> for an HTTP 400 when no more
/// specific error applies.
/// </summary>
public class Http400InvalidArgumentTests
{
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "{\"errors\":{\"ramQuota\":\"RAM quota cannot be less than 100 MiB\"}}", typeof(InvalidArgumentException))]
    [InlineData(HttpStatusCode.BadRequest, "index not found", typeof(IndexNotFoundException))]
    [InlineData(HttpStatusCode.BadRequest, "Number of vbuckets cannot be modified", typeof(InvalidArgumentException))]
    [InlineData(HttpStatusCode.InternalServerError, "unexpected", typeof(CouchbaseException))]
    public void ThrowOnError_Maps_Status(HttpStatusCode statusCode, string body, Type expectedType)
    {
        using var response = new HttpResponseMessage(statusCode);
        var ctx = new ManagementErrorContext { HttpStatus = statusCode, Message = body };

        var ex = Assert.ThrowsAny<CouchbaseException>(() => response.ThrowOnError(ctx));

        // Exact type: every mapped exception derives from CouchbaseException.
        Assert.IsType(expectedType, ex);
        Assert.Same(ctx, ex.Context);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public void ThrowOnError_Unmapped_Uses_Body_As_Message(HttpStatusCode statusCode)
    {
        using var response = new HttpResponseMessage(statusCode);
        var ctx = new ManagementErrorContext { HttpStatus = statusCode, Message = "the body" };

        var ex = Assert.ThrowsAny<CouchbaseException>(() => response.ThrowOnError(ctx));

        Assert.Equal("the body", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, typeof(InvalidArgumentException))]
    [InlineData(HttpStatusCode.InternalServerError, typeof(CouchbaseException))]
    public void CreateUnmappedError_Maps_Status(HttpStatusCode statusCode, Type expectedType)
    {
        using var response = new HttpResponseMessage(statusCode);

        var ex = response.CreateUnmappedError("the body");

        Assert.IsType(expectedType, ex);
        Assert.Equal("the body", ex.Message);
        var ctx = Assert.IsType<ManagementErrorContext>(ex.Context);
        Assert.Equal(statusCode, ctx.HttpStatus);
        Assert.Equal("the body", ctx.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, typeof(InvalidArgumentException))]
    [InlineData(HttpStatusCode.InternalServerError, typeof(CouchbaseException))]
    public void CreateUnmappedError_Keeps_Context(HttpStatusCode statusCode, Type expectedType)
    {
        using var response = new HttpResponseMessage(statusCode);
        var ctx = new ManagementErrorContext { HttpStatus = statusCode, Message = "the body", Statement = "/path" };

        var ex = response.CreateUnmappedError(ctx);

        Assert.IsType(expectedType, ex);
        Assert.Same(ctx, ex.Context);
        Assert.Equal("the body", ex.Message);
    }

    [Fact]
    public async Task CreateBucket_BucketExists_Takes_Precedence()
    {
        var manager = CreateBucketManager(HttpStatusCode.BadRequest,
            "{\"errors\":{\"name\":\"Bucket with given name already exists\"}}");

        await Assert.ThrowsAsync<BucketExistsException>(() =>
            manager.CreateBucketAsync(new BucketSettings { Name = "default", RamQuotaMB = 100 }));
    }

    [Fact]
    public async Task CreateBucket_Unmapped_400_Throws_InvalidArgument()
    {
        var manager = CreateBucketManager(HttpStatusCode.BadRequest,
            "{\"errors\":{\"ramQuota\":\"RAM quota cannot be less than 100 MiB\"}}");

        var ex = await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            manager.CreateBucketAsync(new BucketSettings { Name = "default", RamQuotaMB = 1 }));
        Assert.Equal(HttpStatusCode.BadRequest, Assert.IsType<ManagementErrorContext>(ex.Context).HttpStatus);
    }

    [Fact]
    public async Task GetUser_400_Throws_InvalidArgument()
    {
        var manager = CreateUserManager(HttpStatusCode.BadRequest, "{\"errors\":{\"domain\":\"unknown domain\"}}");

        var ex = await Assert.ThrowsAsync<InvalidArgumentException>(() => manager.GetUserAsync("user"));
        Assert.Equal(HttpStatusCode.BadRequest, Assert.IsType<ManagementErrorContext>(ex.Context).HttpStatus);
    }

    [Fact]
    public async Task GetUser_500_Still_Throws_HttpRequestException()
    {
        var manager = CreateUserManager(HttpStatusCode.InternalServerError, "unexpected");

        await Assert.ThrowsAsync<HttpRequestException>(() => manager.GetUserAsync("user"));
    }

    [Fact]
    public async Task ChangeUserPassword_400_Throws_InvalidArgument()
    {
        var manager = CreateUserManager(HttpStatusCode.BadRequest,
            "{\"errors\":{\"password\":\"The password must be at least 6 characters long.\"}}");

        await Assert.ThrowsAsync<InvalidArgumentException>(() => manager.ChangeUserPasswordAsync("short"));
    }

    [Fact]
    public async Task UpsertFunction_Unmapped_400_Throws_InvalidArgument()
    {
        var service = new Mock<IEventingFunctionService>();
        service.Setup(x => x.PostAsync(It.IsAny<Uri>(), It.IsAny<IRequestSpan>(), It.IsAny<IRequestSpan>(),
                It.IsAny<CancellationToken>(), It.IsAny<EventingFunction>(), It.IsAny<EventingFunctionKeyspace>()))
            .ReturnsAsync(() => EventingResponse(HttpStatusCode.BadRequest, "ERR_SOMETHING_NEW"));

        var ex = await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            CreateEventingManager(service).UpsertFunctionAsync(new EventingFunction { Name = "some_func" }));
        Assert.Equal(HttpStatusCode.BadRequest, Assert.IsType<ManagementErrorContext>(ex.Context).HttpStatus);
    }

    [Fact]
    public async Task UpsertFunction_Unmapped_500_Still_Throws_CouchbaseException()
    {
        var service = new Mock<IEventingFunctionService>();
        service.Setup(x => x.PostAsync(It.IsAny<Uri>(), It.IsAny<IRequestSpan>(), It.IsAny<IRequestSpan>(),
                It.IsAny<CancellationToken>(), It.IsAny<EventingFunction>(), It.IsAny<EventingFunctionKeyspace>()))
            .ReturnsAsync(() => EventingResponse(HttpStatusCode.InternalServerError, "ERR_SOMETHING_NEW"));

        var ex = await Assert.ThrowsAnyAsync<CouchbaseException>(() =>
            CreateEventingManager(service).UpsertFunctionAsync(new EventingFunction { Name = "some_func" }));
        Assert.IsType<CouchbaseException>(ex);
    }

    [Fact]
    public async Task DropFunction_Unmapped_400_Throws_InvalidArgument()
    {
        var service = new Mock<IEventingFunctionService>();
        service.Setup(x => x.DeleteAsync(It.IsAny<Uri>(), It.IsAny<IRequestSpan>(), It.IsAny<IRequestSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => EventingResponse(HttpStatusCode.BadRequest, "ERR_SOMETHING_NEW"));

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            CreateEventingManager(service).DropFunctionAsync("some_func"));
    }

    [Fact]
    public async Task GetAllFunctions_400_Throws_InvalidArgument()
    {
        var service = new Mock<IEventingFunctionService>();
        service.Setup(x => x.GetAsync(It.IsAny<Uri>(), It.IsAny<IRequestSpan>(), It.IsAny<IRequestSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => EventingResponse(HttpStatusCode.BadRequest, "ERR_SOMETHING_NEW"));

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            CreateEventingManager(service).GetAllFunctionsAsync());
    }

    [Fact]
    public async Task GetAllFunctions_500_Still_Throws_HttpRequestException()
    {
        var service = new Mock<IEventingFunctionService>();
        service.Setup(x => x.GetAsync(It.IsAny<Uri>(), It.IsAny<IRequestSpan>(), It.IsAny<IRequestSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => EventingResponse(HttpStatusCode.InternalServerError, "ERR_SOMETHING_NEW"));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateEventingManager(service).GetAllFunctionsAsync());
    }

    private static HttpResponseMessage EventingResponse(HttpStatusCode statusCode, string name) =>
        new(statusCode)
        {
            Content = new StringContent(
                $"{{\"name\":\"{name}\",\"code\":99,\"description\":\"Something new\",\"attributes\":null,\"runtime_info\":{{\"code\":99,\"info\":\"new\"}}}}")
        };

    private static Mock<IClusterNode> CreateNode(string address)
    {
        var nodeAdapter = new Mock<NodeAdapter>();
        nodeAdapter.Object.CanonicalHostname = "localhost";

        var node = new Mock<IClusterNode>();
        node.Setup(n => n.NodesAdapter).Returns(nodeAdapter.Object);
        node.Setup(n => n.ManagementUri).Returns(new Uri(address));
        node.Setup(n => n.EventingUri).Returns(new Uri(address));
        return node;
    }

    private static Mock<IServiceUriProvider> CreateManagementUriProvider()
    {
        var node = CreateNode("http://localhost:8091");
        var provider = new Mock<IServiceUriProvider>();
        provider.Setup(m => m.GetRandomManagementNode()).Returns(node.Object);
        return provider;
    }

    private static MockHttpClientFactory CreateHttpClientFactory(HttpStatusCode statusCode, string body) =>
        new(new HttpClient(FakeHttpMessageHandler.Create(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body)
        })));

    private static BucketManager CreateBucketManager(HttpStatusCode statusCode, string body) =>
        new(CreateManagementUriProvider().Object, CreateHttpClientFactory(statusCode, body), TestRedactor.None,
            new Mock<ILogger<BucketManager>>().Object);

    private static UserManager CreateUserManager(HttpStatusCode statusCode, string body) =>
        new(CreateManagementUriProvider().Object, CreateHttpClientFactory(statusCode, body),
            new Mock<ILogger<UserManager>>().Object, TestRedactor.None);

    private static EventingFunctionManager CreateEventingManager(Mock<IEventingFunctionService> service)
    {
        var node = CreateNode("http://localhost:8096");
        var provider = new Mock<IServiceUriProvider>();
        provider.Setup(m => m.GetRandomEventingNode()).Returns(node.Object);
        provider.Setup(m => m.GetRandomEventingUri()).Returns(new Uri("http://localhost:8096"));

        return new EventingFunctionManager(service.Object, new Mock<ILogger<EventingFunctionManager>>().Object,
            NoopRequestTracer.Instance, provider.Object);
    }
}

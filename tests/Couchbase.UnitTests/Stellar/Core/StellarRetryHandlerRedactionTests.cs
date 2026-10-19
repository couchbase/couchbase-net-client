#if NETCOREAPP3_1_OR_GREATER
using System;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Management.Collections;
using Couchbase.Stellar.Core.Retry;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Status = Grpc.Core.Status;

namespace Couchbase.UnitTests.Stellar.Core;

/// <summary>
/// The Stellar error contexts and exception messages honour <see cref="ClusterOptions.RedactionLevel"/>
/// (NCBC-4300). The errors are built the way the gateway builds them, with the document key in both
/// the detail text and the resource name, and go through the real status-details deserializer.
/// </summary>
public class StellarRetryHandlerRedactionTests
{
    private const string Key = "mykey";
    private const string Keyspace = "travel/inventory/hotels";

    // The gateway's message and resource name for a missing document (errorhandler.go).
    private const string DocNotFoundDetail = $"Document '{Key}' not found in '{Keyspace}'.";
    private const string DocResourceName = $"{Keyspace}/{Key}";

    [Fact]
    public void DocumentNotFound_At_Partial_Redacts_Detail_And_ResourceName()
    {
        var context = MapDocumentNotFound(RedactionLevel.Partial);

        Assert.Equal($"<ud>{DocNotFoundDetail}</ud>", context.Fields["Detail"]);
        Assert.Equal($"<ud>{DocResourceName}</ud>", context.Fields["ResourceName"]);
        Assert.Equal("document", context.Fields["ResourceType"]);
    }

    [Fact]
    public void DocumentNotFound_At_None_Leaves_The_Context_Unchanged()
    {
        var context = MapDocumentNotFound(RedactionLevel.None);

        Assert.Equal(DocNotFoundDetail, context.Fields["Detail"]);
        Assert.Equal(DocResourceName, context.Fields["ResourceName"]);
    }

    [Fact]
    public void ToString_Keeps_The_Tags_Readable_By_cblogredaction()
    {
        var json = MapDocumentNotFound(RedactionLevel.Partial).ToString();

        Assert.Contains($"\"ResourceName\":\"<ud>{DocResourceName}</ud>\"", json);
        // A regular literal: in a verbatim one the compiler would turn \u003C into "<".
        Assert.DoesNotContain("\\u003C", json);
    }

    [Fact]
    public void Exception_Message_Built_From_The_Detail_Is_Redacted()
    {
        const string detail = $"Sync write operation on '{Key}' in '{Keyspace}' timed out.";
        var handler = CreateHandler(RedactionLevel.Partial);

        var mapped = handler.MapMidStreamException(
            new RpcException(new Status(StatusCode.DeadlineExceeded, detail)), new StellarRequest());

        var timeout = Assert.IsType<AmbiguousTimeoutException>(mapped);
        Assert.Contains($"<ud>{detail}</ud>", timeout.Message);
    }

    [Fact]
    public void The_Raw_Detail_Still_Picks_The_Exception()
    {
        // The detail is matched before it is redacted, so redaction cannot change the mapping.
        var handler = CreateHandler(RedactionLevel.Full);

        var mapped = handler.MapMidStreamException(
            new RpcException(new Status(StatusCode.FailedPrecondition, $"{StellarRetryStrings.DocNotJson} {Key}")),
            new StellarRequest());

        Assert.IsType<DocumentNotJsonException>(mapped);
    }

    [Fact]
    public void CollectionExists_Message_Is_MetaData()
    {
        var handler = CreateHandler(RedactionLevel.Full);

        var mapped = handler.MapMidStreamException(
            CreateResourceError(StatusCode.AlreadyExists, "Collection 'hotels' already existed in 'travel/inventory'.",
                "collection", Keyspace),
            new StellarRequest());

        var exists = Assert.IsType<CollectionExistsException>(mapped);
        Assert.Contains($"<md>{Keyspace}</md>", exists.Message);
    }

    [Theory]
    [InlineData("document")]
    [InlineData("path")]
    [InlineData("somethingnew")]
    [InlineData(null)]
    public void RedactResourceName_Treats_Document_Path_And_Unknown_As_UserData(string resourceType)
    {
        var redactor = new Redactor(RedactionLevel.Partial);

        Assert.Equal("<ud>name</ud>", StellarRetryHandler.RedactResourceName(redactor, resourceType, "name"));
    }

    [Theory]
    [InlineData("bucket")]
    [InlineData("scope")]
    [InlineData("collection")]
    [InlineData("queryindex")]
    [InlineData("searchindex")]
    [InlineData("searchIndex")]
    [InlineData("analyticsindex")]
    [InlineData("user")]
    [InlineData("cluster")]
    public void RedactResourceName_Treats_Keyspace_Index_User_And_Cluster_As_MetaData(string resourceType)
    {
        // Metadata is left as it is at Partial and tagged at Full.
        Assert.Equal("name",
            StellarRetryHandler.RedactResourceName(new Redactor(RedactionLevel.Partial), resourceType, "name"));
        Assert.Equal("<md>name</md>",
            StellarRetryHandler.RedactResourceName(new Redactor(RedactionLevel.Full), resourceType, "name"));
    }

    private static GenericErrorContext MapDocumentNotFound(RedactionLevel level)
    {
        var mapped = CreateHandler(level).MapMidStreamException(
            CreateResourceError(StatusCode.NotFound, DocNotFoundDetail, "document", DocResourceName),
            new StellarRequest());

        // Read through the base class: KeyValueException's typed Context hides it and is null for a
        // GenericErrorContext.
        CouchbaseException notFound = Assert.IsType<DocumentNotFoundException>(mapped);
        return Assert.IsType<GenericErrorContext>(notFound.Context);
    }

    private static StellarRetryHandler CreateHandler(RedactionLevel level) =>
        new(TimeProvider.System, NullLogger<StellarRetryHandler>.Instance, new Redactor(level));

    /// <summary>
    /// An error with a <see cref="ResourceInfo"/> detail, carried in the grpc-status-details-bin
    /// trailer as the gateway sends it.
    /// </summary>
    private static RpcException CreateResourceError(StatusCode code, string detail, string resourceType,
        string resourceName)
    {
        var status = new Google.Rpc.Status
        {
            Code = (int) code,
            Message = detail,
            Details = { Any.Pack(new ResourceInfo { ResourceType = resourceType, ResourceName = resourceName }) }
        };

        var trailers = new Metadata { { "grpc-status-details-bin", status.ToByteArray() } };
        return new RpcException(new Status(code, detail), trailers);
    }
}
#endif

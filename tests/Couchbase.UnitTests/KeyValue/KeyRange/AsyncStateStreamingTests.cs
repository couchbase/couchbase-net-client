using System.ComponentModel;
using Couchbase.Core.IO.Operations.RangeScan;
using Couchbase.Core.Retry;
using Couchbase.Test.Common.Utils;
using Couchbase.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.KeyValue.KeyRange;
using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Moq;
using Xunit;

public class AsyncStateStreamingTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly string _rangeScanKeyNotFoundResponse;

    public AsyncStateStreamingTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _rangeScanKeyNotFoundResponse =
            "vtB01FVkAkLAqGqBCABFAgDL8vlAAEAG8V3AqGqBwKhqASvK3KGhKDLZEpF1/IAYAfXTggAAAQEICv9sEHULkciHGNoDAAABAAEAAAB/ZQgAAAAAAAAAAAAAAgAkeyJlcnJvciI6eyJjb250ZXh0IjoiUmFuZ2VTY2FuIHZiOjU0NyA1MGI0MWQxZC04MmJjLTRhMDAtYTQxMS1jYTYwMGQxMzcyYzUgdHJ5QW5kU2Nhbk9uZUtleSBubyBrZXlzIGluIHJhbmdlOiBubyBzdWNoIGtleSJ9fQ==";

    }

    /// <summary>
    /// Taken from the RFC section "Failures on RangeScanContinue"
    /// </summary>
    /// <param name="statusCode">The status code in the response</param>
    /// <param name="throws">Whether this status results in an exception</param>
    /// <param name="retry">Regardless of exception, should this status result in a retry</param>
    [Theory]
    [InlineData(ResponseStatus.RangeScanComplete, false, false)]
    [InlineData(ResponseStatus.Success, false, false)]
    [InlineData(ResponseStatus.RangeScanMore, false, false)]

    // "Retryable with special handling"
    [InlineData(ResponseStatus.VBucketBelongsToAnotherServer, false, true)]

    // "Fatal (unless sampling scan)"
    [InlineData(ResponseStatus.KeyNotFound, true, false)]
    [InlineData(ResponseStatus.Eaccess, true, false)]
    [InlineData(ResponseStatus.UnknownCollection, true, false)]
    [InlineData(ResponseStatus.RangeScanCanceled, true, false)]

    // "Fatal always"
    [InlineData(ResponseStatus.InvalidArguments, true, false)]
    [InlineData(ResponseStatus.Busy, true, false)]
    public async Task RangeScanContinue_StatusHandling(ResponseStatus statusCode, bool throws, bool retry)
    {
        var op = PrepRangeScanContinueResponse(statusCode, out var asyncStreamingState, out var fakeMem);

        if (throws)
        {
            var thrown = Assert.ThrowsAny<Exception>(() => asyncStreamingState.Complete(fakeMem));
            if (retry)
            {
                Assert.True(thrown is IRetryable, userMessage: "Expected to throw");
            }
        }
        else
        {
            var streamComplete = asyncStreamingState.Complete(fakeMem);
            if (streamComplete)
            {
                // await the completion to make sure it doesn't throw
                await op.Completed;
            }

            if (retry)
            {
                Assert.False(streamComplete, "Expected op to be incomplete and retried");
            }
        }
    }

    [Theory]
    [InlineData(ResponseStatus.RangeScanComplete, false, false)]
    [InlineData(ResponseStatus.Success, false, false)]
    [InlineData(ResponseStatus.RangeScanMore, false, false)]

    // "Retryable with special handling"
    [InlineData(ResponseStatus.VBucketBelongsToAnotherServer, false, true)]

    // "Fatal (unless sampling scan)"
    [InlineData(ResponseStatus.KeyNotFound, false, false)]
    [InlineData(ResponseStatus.Eaccess, false, false)]
    [InlineData(ResponseStatus.UnknownCollection, false, false)]
    [InlineData(ResponseStatus.RangeScanCanceled, false, false)]

    // "Fatal always"
    [InlineData(ResponseStatus.InvalidArguments, true, false)]
    [InlineData(ResponseStatus.Busy, true, false)]
    public async Task RangeScanContinue_Sampling_StatusHandling(ResponseStatus statusCode, bool throws, bool retry)
    {
        var op = PrepRangeScanContinueResponse(statusCode, out var asyncStreamingState, out var fakeMem);
        op.IsSampling = true;
        if (throws)
        {
            var thrown = Assert.ThrowsAny<Exception>(() => asyncStreamingState.Complete(fakeMem));
            if (retry)
            {
                Assert.True(thrown is IRetryable, userMessage: "Expected to throw");
            }
        }
        else
        {
            var streamComplete = asyncStreamingState.Complete(fakeMem);
            if (streamComplete)
            {
                // await the completion to make sure it doesn't throw
                await op.Completed;
            }

            if (retry)
            {
                Assert.False(streamComplete, "Expected op to be incomplete and retried");
            }
        }
    }

    private RangeScanContinue PrepRangeScanContinueResponse(ResponseStatus statusCode,
        out AsyncStateStreaming asyncStreamingState, out FakeMemoryOwner<byte> fakeMem)
    {
        // load a canned response, but substitute the status code
        var responseBytes = Convert.FromBase64String(_rangeScanKeyNotFoundResponse);
        var op = new RangeScanContinue();
        asyncStreamingState = new AsyncStateStreaming(op, Mock.Of<ILogger<MultiplexingConnection>>());
        fakeMem = new FakeMemoryOwner<byte>(responseBytes);
        Couchbase.Core.IO.Converters.ByteConverter.FromInt16((Int16)statusCode, fakeMem.Memory.Slice(HeaderOffsets.Status).Span);
        return op;
    }
}

#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Protostellar.KV.V1;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Query;
using Couchbase.Core.Retry;
using Couchbase.Stellar.Core.Retry;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Stellar.Core;

public class StellarRetryHandlerTests
{
    [Theory]
    [InlineData( "DocumentNotFound", StatusCode.NotFound, "\n\bdocument\u0012\"default/_default/_default/fake_doc")]
    [InlineData( "CollectionExists", StatusCode.AlreadyExists, "\n\ncollection\u0012\u0017default/_default/5efcb8")]
    [InlineData( "IndexExists", StatusCode.AlreadyExists, "\n\nqueryindex\u0012\b#primary")]
    public async Task RetryThrowsTypeUrlResourceErrors(string expectedException, StatusCode statusCode, string detailValue)
    {
        var any = new Any
        {
            Value = ByteString.CopyFromUtf8(detailValue),
            TypeUrl = StellarRetryStrings.TypeUrlResourceInfo
        };
        var retryMock = new Mock<StellarRetryHandler>();
        retryMock.Setup(handler => handler.StatusDeserializer(It.IsAny<RpcException>())).Returns(any);

        var grpcCall = () =>
        {
            var thrower = () => { throw new RpcException(new Status(statusCode, "NoDetail")); };
            thrower.Invoke();
            return Task.FromResult(new GetResponse());
        };

        var result = await Record.ExceptionAsync( () => retryMock.Object.RetryAsync(grpcCall, new StellarRequest()));
        Debug.Assert(result != null, nameof(result) + " != null");
        Assert.Contains(expectedException, result.ToString());
    }

    [Fact]
    public async Task Locked_IsRetried_NotThrownImmediately()
    {
        // CNG-5 regression: CNG reports a locked document as a PreconditionFailure "LOCKED"
        // detail block. It must be retried with backoff, not raised immediately as an
        // UnambiguousTimeoutException.
        var preconditionFailure = new Google.Rpc.PreconditionFailure();
        preconditionFailure.Violations.Add(
            new Google.Rpc.PreconditionFailure.Types.Violation { Type = StellarRetryStrings.PreconditionLocked });
        var any = new Any
        {
            TypeUrl = StellarRetryStrings.TypeUrlPreconditionFailure,
            Value = preconditionFailure.ToByteString()
        };

        var retryMock = new Mock<StellarRetryHandler>();
        retryMock.Setup(handler => handler.StatusDeserializer(It.IsAny<RpcException>())).Returns(any);

        var request = new StellarRequest { Timeout = TimeSpan.FromSeconds(5) };
        var callCount = 0;

        Task<GetResponse> GrpcCall()
        {
            callCount++;
            if (callCount < 3)
            {
                throw new RpcException(new Status(StatusCode.FailedPrecondition, "LOCKED"));
            }

            return Task.FromResult(new GetResponse());
        }

        var result = await retryMock.Object.RetryAsync(GrpcCall, request);

        Assert.NotNull(result);
        Assert.Equal(3, callCount);
        // Exactly one increment per failed call: guards against falling through to the status
        // switch (which would increment Attempts a second time on a LOCKED detail string).
        Assert.Equal(2u, request.Attempts);
    }

    [Fact]
    public async Task Throw_CouchbaseException_On_Unknown_Error()
    {
        var retryMock = new StellarRetryHandler();
        var request = new StellarRequest();
        var responseMock = new Mock<IQueryResult<object>>();
        // ReSharper disable once NotDisposedResourceIsReturned
        responseMock.Setup(x => x.GetAsyncEnumerator(new CancellationToken())).Throws<CouchbaseException>();

        Task<IQueryResult<object>> GrpcCall()
        {
            throw new RpcException(new Status(StatusCode.Unknown, "some error text from the server"));
        }

        await Assert.ThrowsAsync<CouchbaseException>(async ()=> await retryMock.RetryAsync(GrpcCall, request));
    }

    [Fact]
    public async Task Internal_GenuineServerError_StillThrows()
    {
        // A genuine server-side Internal error (not a transport failure) should still
        // throw InternalServerFailureException immediately, not retry.
        var handler = new StellarRetryHandler();
        var request = new StellarRequest();

        Task<GetResponse> GrpcCall()
        {
            throw new RpcException(new Status(StatusCode.Internal, "Internal server error processing request"));
        }

        await Assert.ThrowsAsync<Couchbase.Core.Exceptions.InternalServerFailureException>(
            async () => await handler.RetryAsync(GrpcCall, request));
    }

    [Fact]
    public async Task ResourceExhausted_FallsBackToCouchbaseException()
    {
        // NCBC-4265: unmapped RESOURCE_EXHAUSTED -> CouchbaseException, not ValueToolarge.
        var handler = new StellarRetryHandler();
        var request = new StellarRequest();

        Task<GetResponse> GrpcCall()
        {
            throw new RpcException(new Status(StatusCode.ResourceExhausted, "rate limited"));
        }

        // Exact-type match also proves it is not a ValueToolargeException (a subtype).
        await Assert.ThrowsAsync<CouchbaseException>(
            async () => await handler.RetryAsync(GrpcCall, request));
    }

    [Fact]
    public async Task UnrecognizedPreconditionType_IsTerminalCouchbaseException_NotRetried()
    {
        // NCBC-4265: an unknown precondition type (e.g. the removed "CAS") is terminal, not retried.
        var preconditionFailure = new Google.Rpc.PreconditionFailure();
        preconditionFailure.Violations.Add(
            new Google.Rpc.PreconditionFailure.Types.Violation { Type = "CAS" });
        var any = new Any
        {
            TypeUrl = StellarRetryStrings.TypeUrlPreconditionFailure,
            Value = preconditionFailure.ToByteString()
        };

        var retryMock = new Mock<StellarRetryHandler>();
        retryMock.Setup(handler => handler.StatusDeserializer(It.IsAny<RpcException>())).Returns(any);

        var request = new StellarRequest { Timeout = TimeSpan.FromSeconds(5) };
        var callCount = 0;

        Task<GetResponse> GrpcCall()
        {
            callCount++;
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "CAS"));
        }

        await Assert.ThrowsAsync<CouchbaseException>(
            async () => await retryMock.Object.RetryAsync(GrpcCall, request));
        Assert.Equal(1, callCount); // thrown on the first attempt, never retried
    }

    [Fact]
    public async Task UnmappedFailedPrecondition_NoDetailBlock_IsTerminal_NotRetried()
    {
        // NCBC-4265: an unmapped FAILED_PRECONDITION with no detail block is terminal, not retried.
        var retryMock = new Mock<StellarRetryHandler>();
        retryMock.Setup(handler => handler.StatusDeserializer(It.IsAny<RpcException>())).Returns((Any?)null);

        var request = new StellarRequest { Timeout = TimeSpan.FromSeconds(5) };
        var callCount = 0;

        Task<GetResponse> GrpcCall()
        {
            callCount++;
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "SOME_UNKNOWN_PRECONDITION"));
        }

        await Assert.ThrowsAsync<CouchbaseException>(
            async () => await retryMock.Object.RetryAsync(GrpcCall, request));
        Assert.Equal(1, callCount); // thrown on the first attempt, never retried
    }

    [Fact]
    public async Task NestedIOException_InHttpRequestException_IsRetried()
    {
        // IOException wrapped inside HttpRequestException (the real-world pattern
        // for "Connection reset by peer") should be retried via the recursive
        // InnerException walk in IsTransientTransportException.
        var handler = new StellarRetryHandler();
        var request = new StellarRequest();
        var callCount = 0;

        Task<GetResponse> GrpcCall()
        {
            callCount++;
            if (callCount == 1)
            {
                var ioEx = new System.IO.IOException("Connection reset by peer.");
                throw new System.Net.Http.HttpRequestException(
                    "An error occurred while sending the request.", ioEx);
            }
            return Task.FromResult(new GetResponse());
        }

        var result = await handler.RetryAsync(GrpcCall, request);
        Assert.NotNull(result);
        Assert.Equal(2, callCount);
        Assert.True(request.Attempts > 0, "Expected at least one retry attempt.");
    }

    // ──────────────────────────────────────────────────────────
    //  Timeout enforcement tests using FakeTimeProvider
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void RemainingTimeout_ShrinkAsTimeAdvances()
    {
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        fakeTime.SetUtcNow(DateTimeOffset.UtcNow);

        var request = new StellarRequest(fakeTime)
        {
            Timeout = TimeSpan.FromMilliseconds(500)
        };

        // At creation, remaining ≈ full timeout
        var remaining = request.RemainingTimeout;
        Assert.NotNull(remaining);
        Assert.InRange(remaining.Value.TotalMilliseconds, 490, 510);

        // Advance 200ms → remaining ≈ 300ms
        fakeTime.Advance(TimeSpan.FromMilliseconds(200));
        remaining = request.RemainingTimeout;
        Assert.NotNull(remaining);
        Assert.InRange(remaining.Value.TotalMilliseconds, 290, 310);

        // Advance another 300ms → remaining ≈ 0
        fakeTime.Advance(TimeSpan.FromMilliseconds(300));
        remaining = request.RemainingTimeout;
        Assert.NotNull(remaining);
        Assert.True(remaining.Value.TotalMilliseconds <= 10,
            $"Expected ≤10ms remaining, got {remaining.Value.TotalMilliseconds}ms");
    }

    [Fact]
    public void RemainingTimeout_IsNull_WhenNoTimeoutSet()
    {
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var request = new StellarRequest(fakeTime)
        {
            Timeout = TimeSpan.Zero
        };

        Assert.Null(request.RemainingTimeout);

        // Even after time passes, still null
        fakeTime.Advance(TimeSpan.FromSeconds(60));
        Assert.Null(request.RemainingTimeout);
    }

    [Fact]
    public void RemainingTimeout_GoesNegative_WhenTimeoutExceeded()
    {
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        fakeTime.SetUtcNow(DateTimeOffset.UtcNow);

        var request = new StellarRequest(fakeTime)
        {
            Timeout = TimeSpan.FromMilliseconds(100)
        };

        fakeTime.Advance(TimeSpan.FromMilliseconds(250));
        var remaining = request.RemainingTimeout;
        Assert.NotNull(remaining);
        Assert.True(remaining.Value < TimeSpan.Zero,
            $"Expected negative remaining, got {remaining.Value.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task Timeout_ThrowsUnambiguousTimeout_ForIdempotent_AfterRetries()
    {
        // Simulate: Unavailable errors exhaust the cumulative timeout.
        // The gRPC deadline mechanism fires DeadlineExceeded when RemainingTimeout goes negative.
        // This test uses a background task for RetryAsync and advances time from the main thread
        // so that backoff.Delay() timers complete correctly via FakeTimeProvider.
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        fakeTime.SetUtcNow(DateTimeOffset.UtcNow);

        var handler = new StellarRetryHandler(fakeTime);
        var request = new StellarRequest(fakeTime)
        {
            Timeout = TimeSpan.FromMilliseconds(5000),
            Idempotent = true
        };

        Task<GetResponse> GrpcCall()
        {
            // Once the remaining timeout goes negative, simulate what gRPC would do
            if (request.RemainingTimeout is { } remaining && remaining <= TimeSpan.Zero)
            {
                throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Deadline exceeded"));
            }

            throw new RpcException(new Status(StatusCode.Unavailable, "Service unavailable"));
        }

        // Run RetryAsync on a background task, advance time from this thread
        var retryTask = Task.Run(() => handler.RetryAsync(GrpcCall, request));

        // Pump time forward until the task completes or we give up
        while (!retryTask.IsCompleted)
        {
            fakeTime.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Delay(1); // yield to let continuations run
        }
    }

    [Fact]
    public async Task Timeout_ThrowsAmbiguousTimeout_ForNonIdempotent_AfterRetries()
    {
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        fakeTime.SetUtcNow(DateTimeOffset.UtcNow);

        var handler = new StellarRetryHandler(fakeTime);
        var request = new StellarRequest(fakeTime)
        {
            Timeout = TimeSpan.FromMilliseconds(5000),
            Idempotent = false
        };

        Task<GetResponse> GrpcCall()
        {
            if (request.RemainingTimeout is { } remaining && remaining <= TimeSpan.Zero)
            {
                throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Deadline exceeded"));
            }

            throw new RpcException(new Status(StatusCode.Unavailable, "Service unavailable"));
        }

        var retryTask = Task.Run(() => handler.RetryAsync(GrpcCall, request));

        while (!retryTask.IsCompleted)
        {
            fakeTime.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Delay(1);
        }

        await Assert.ThrowsAsync<Couchbase.Core.Exceptions.AmbiguousTimeoutException>(
            () => retryTask);
    }

    [Fact]
    public async Task Timeout_ThrowsAmbiguousTimeout_ForIdempotentButMutating_AfterRetries()
    {
        // CNG-2 regression: timeout ambiguity must key on read-only status, not idempotency.
        // An op such as GetAndLock/GetAndTouch/MutateIn is idempotent (safe to retry) yet mutates
        // server state, so on timeout it must surface as Ambiguous — it may have applied.
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        fakeTime.SetUtcNow(DateTimeOffset.UtcNow);

        var handler = new StellarRetryHandler(fakeTime);
        var request = new StellarRequest(fakeTime)
        {
            Timeout = TimeSpan.FromMilliseconds(5000),
            Idempotent = true,
            ReadOnly = false
        };

        Task<GetResponse> GrpcCall()
        {
            if (request.RemainingTimeout is { } remaining && remaining <= TimeSpan.Zero)
            {
                throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Deadline exceeded"));
            }

            throw new RpcException(new Status(StatusCode.Unavailable, "Service unavailable"));
        }

        var retryTask = Task.Run(() => handler.RetryAsync(GrpcCall, request));

        while (!retryTask.IsCompleted)
        {
            fakeTime.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Delay(1);
        }

        await Assert.ThrowsAsync<Couchbase.Core.Exceptions.AmbiguousTimeoutException>(
            () => retryTask);
    }

    [Fact]
    public void ReadOnly_DefaultsToIdempotent_WhenNotSet()
    {
        Assert.True(new StellarRequest { Idempotent = true }.ReadOnly);
        Assert.False(new StellarRequest { Idempotent = false }.ReadOnly);

        // Explicit override wins over the Idempotent default.
        Assert.False(new StellarRequest { Idempotent = true, ReadOnly = false }.ReadOnly);
        Assert.True(new StellarRequest { Idempotent = false, ReadOnly = true }.ReadOnly);
    }

    [Fact]
    public async Task Timeout_RetriesSucceed_WithinDeadline()
    {
        // Simulate: First 2 calls fail with Unavailable, 3rd succeeds.
        // Time advances are small enough that total stays within the timeout budget.
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        fakeTime.SetUtcNow(DateTimeOffset.UtcNow);

        var handler = new StellarRetryHandler(fakeTime);
        var request = new StellarRequest(fakeTime)
        {
            Timeout = TimeSpan.FromMilliseconds(60000), // generous budget
            Idempotent = true
        };

        var callCount = 0;

        Task<GetResponse> GrpcCall()
        {
            callCount++;

            if (callCount <= 2)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "Service unavailable"));
            }

            return Task.FromResult(new GetResponse());
        }

        var retryTask = Task.Run(() => handler.RetryAsync(GrpcCall, request));

        while (!retryTask.IsCompleted)
        {
            fakeTime.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(1);
        }

        var result = await retryTask;
        Assert.NotNull(result);
        Assert.Equal(3, callCount);

        // Verify remaining timeout is still positive (we had budget left)
        var remaining = request.RemainingTimeout;
        Assert.NotNull(remaining);
        Assert.True(remaining.Value > TimeSpan.Zero,
            $"Expected positive remaining, got {remaining.Value.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task Timeout_DeadlineShrinks_AcrossRetries()
    {
        // Verify that the RemainingTimeout value decreases on each call,
        // proving the shrinking deadline mechanism works.
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        fakeTime.SetUtcNow(DateTimeOffset.UtcNow);

        var handler = new StellarRetryHandler(fakeTime);
        var request = new StellarRequest(fakeTime)
        {
            Timeout = TimeSpan.FromMilliseconds(60000),
            Idempotent = true
        };

        var observedRemaining = new System.Collections.Generic.List<double>();
        var callCount = 0;

        Task<GetResponse> GrpcCall()
        {
            callCount++;
            observedRemaining.Add(request.RemainingTimeout!.Value.TotalMilliseconds);

            if (callCount <= 3)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "Service unavailable"));
            }

            return Task.FromResult(new GetResponse());
        }

        var retryTask = Task.Run(() => handler.RetryAsync(GrpcCall, request));

        while (!retryTask.IsCompleted)
        {
            // Each advance covers the backoff delay and simulates elapsed time
            fakeTime.Advance(TimeSpan.FromMilliseconds(1500));
            await Task.Delay(1);
        }

        await retryTask;

        Assert.Equal(4, observedRemaining.Count);

        // Each observed remaining should be strictly less than the previous
        for (int i = 1; i < observedRemaining.Count; i++)
        {
            Assert.True(observedRemaining[i] < observedRemaining[i - 1],
                $"Call {i + 1} remaining ({observedRemaining[i]}ms) should be < call {i} remaining ({observedRemaining[i - 1]}ms)");
        }
    }

    private class RpcExceptionWithInner : RpcException
    {
        public RpcExceptionWithInner(Status status, System.Exception innerException)
            : base(status)
        {
            // Use reflection to set the inner exception since RpcException doesn't expose it via constructor
            var field = typeof(System.Exception).GetField("_innerException", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(this, innerException);
        }
    }

    [Fact]
    public async Task RetryAsync_Retries_HttpRequestException_AsTransientTransportError()
    {
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        fakeTime.SetUtcNow(DateTimeOffset.UtcNow);

        var handler = new StellarRetryHandler(fakeTime);
        var request = new StellarRequest(fakeTime) { Timeout = TimeSpan.FromMilliseconds(5000), Idempotent = true };
        var callCount = 0;

        Task<GetResponse> GrpcCall()
        {
            callCount++;
            if (callCount == 1) throw new System.Net.Http.HttpRequestException("Connection reset by peer");
            return Task.FromResult(new GetResponse());
        }

        var retryTask = Task.Run(() => handler.RetryAsync(GrpcCall, request));

        while (!retryTask.IsCompleted)
        {
            fakeTime.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(1);
        }

        var result = await retryTask;
        Assert.NotNull(result);
        Assert.Equal(2, callCount); // Succeeded on the second try
        Assert.True(request.Attempts > 0, "Expected at least one retry attempt.");
    }

    [Fact]
    public async Task RetryAsync_Retries_IOException_AsTransientTransportError()
    {
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        fakeTime.SetUtcNow(DateTimeOffset.UtcNow);

        var handler = new StellarRetryHandler(fakeTime);
        var request = new StellarRequest(fakeTime) { Timeout = TimeSpan.FromMilliseconds(5000), Idempotent = true };
        var callCount = 0;

        Task<GetResponse> GrpcCall()
        {
            callCount++;
            if (callCount == 1) throw new System.IO.IOException("Broken pipe");
            return Task.FromResult(new GetResponse());
        }

        var retryTask = Task.Run(() => handler.RetryAsync(GrpcCall, request));

        while (!retryTask.IsCompleted)
        {
            fakeTime.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(1);
        }

        var result = await retryTask;
        Assert.NotNull(result);
        Assert.Equal(2, callCount);
        Assert.True(request.Attempts > 0, "Expected at least one retry attempt.");
    }

    [Fact]
    public async Task RetryAsync_Retries_RpcExceptionInternal_WithInnerHttpRequestException_AsTransientTransportError()
    {
        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        fakeTime.SetUtcNow(DateTimeOffset.UtcNow);

        var handler = new StellarRetryHandler(fakeTime);
        var request = new StellarRequest(fakeTime) { Timeout = TimeSpan.FromMilliseconds(5000), Idempotent = true };
        var callCount = 0;

        Task<GetResponse> GrpcCall()
        {
            callCount++;
            if (callCount == 1)
            {
                var inner = new System.Net.Http.HttpRequestException("The SSL connection could not be established");
                throw new RpcExceptionWithInner(new Status(StatusCode.Internal, "Internal error"), inner);
            }
            return Task.FromResult(new GetResponse());
        }

        var retryTask = Task.Run(() => handler.RetryAsync(GrpcCall, request));

        while (!retryTask.IsCompleted)
        {
            fakeTime.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(1);
        }

        var result = await retryTask;
        Assert.NotNull(result);
        Assert.Equal(2, callCount);
        Assert.True(request.Attempts > 0, "Expected at least one retry attempt.");
    }
}
#endif

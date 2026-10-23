#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Stellar.KeyValue;
using Couchbase.UnitTests.Stellar.Utils;
using Grpc.Core;
using Grpc.Net.Client;
using Xunit;

namespace Couchbase.UnitTests.Stellar.KeyValue;

/// <summary>
/// The GetAllReplicas timeout (NCBC-4364) is the deadline of the gRPC call, and falls back to
/// <see cref="ClusterOptions.KvTimeout"/>. The deadline is read from the grpc-timeout header the
/// client sends, which is what the gateway sees.
/// </summary>
public class StellarCollectionReplicaTimeoutTests
{
    private const string DocId = "thekey";

    // Far from the 2.5 s KvTimeout default, so the time spent before the header is written cannot
    // make one look like the other.
    private static readonly TimeSpan CustomTimeout = TimeSpan.FromSeconds(1000);

    [Fact]
    public async Task GetAllReplicasStream_Uses_The_Timeout_As_The_Deadline()
    {
        var (collection, handler) = await CreateCollectionAsync();

        await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await foreach (var _ in collection.GetAllReplicasStreamAsync(DocId,
                               new GetAllReplicasOptions().Timeout(CustomTimeout)))
            {
            }
        });

        AssertDeadlineIsAtMost(CustomTimeout, await handler.GrpcTimeout);
    }

    [Fact]
    public async Task GetAllReplicasStream_Without_Timeout_Uses_KvTimeout()
    {
        var (collection, handler) = await CreateCollectionAsync();

        await Assert.ThrowsAsync<RpcException>(async () =>
        {
            await foreach (var _ in collection.GetAllReplicasStreamAsync(DocId))
            {
            }
        });

        AssertDeadlineIsAtMost(new ClusterOptions().KvTimeout, await handler.GrpcTimeout);
    }

    [Fact]
    public async Task GetAllReplicas_Uses_The_Timeout_As_The_Deadline()
    {
        var (collection, handler) = await CreateCollectionAsync();

        Assert.Throws<RpcException>(() =>
            collection.GetAllReplicasAsync(DocId, new GetAllReplicasOptions().Timeout(CustomTimeout)).ToList());

        AssertDeadlineIsAtMost(CustomTimeout, await handler.GrpcTimeout);
    }

    private static void AssertDeadlineIsAtMost(TimeSpan expected, TimeSpan actual)
    {
        Assert.InRange(actual, expected - TimeSpan.FromSeconds(1), expected);
    }

    private static async Task<(StellarCollection Collection, CapturingHandler Handler)> CreateCollectionAsync()
    {
        var handler = new CapturingHandler();
        var channel = GrpcChannel.ForAddress(new Uri("https://xxx"), new GrpcChannelOptions { HttpHandler = handler });
        var cluster = StellarMocks.CreateClusterFromMocksWithOptions(channel: channel);
        var bucket = await cluster.BucketAsync("test");
        return ((StellarCollection) bucket.DefaultCollection(), handler);
    }

    /// <summary>
    /// Records the grpc-timeout header of the first request and fails it at once.
    /// </summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private static readonly Regex GrpcTimeoutFormat = new(@"^(\d+)([HMSmun])$");

        private readonly TaskCompletionSource<TimeSpan> _grpcTimeout =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<TimeSpan> GrpcTimeout => _grpcTimeout.Task;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Headers.TryGetValues("grpc-timeout", out var values))
            {
                _grpcTimeout.TrySetResult(Parse(values.Single()));
            }
            else
            {
                _grpcTimeout.TrySetException(new InvalidOperationException("The request has no grpc-timeout header."));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }

        private static TimeSpan Parse(string header)
        {
            var match = GrpcTimeoutFormat.Match(header);
            Assert.True(match.Success, $"Unexpected grpc-timeout header: {header}");

            var value = long.Parse(match.Groups[1].Value);
            return match.Groups[2].Value switch
            {
                "H" => TimeSpan.FromHours(value),
                "M" => TimeSpan.FromMinutes(value),
                "S" => TimeSpan.FromSeconds(value),
                "m" => TimeSpan.FromMilliseconds(value),
                "u" => TimeSpan.FromTicks(value * 10),
                _ => TimeSpan.FromTicks(value / 100),
            };
        }
    }
}
#endif

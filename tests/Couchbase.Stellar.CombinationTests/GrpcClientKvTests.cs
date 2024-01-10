using System;
using System.Net.Http;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Test.Common.Utils;
using Grpc.Core;
using Grpc.Net.Client;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests
{
    public class GrpcClientKvTests
    {
        private readonly ITestOutputHelper _outputHelper;

        public GrpcClientKvTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        private class HeaderLoggingHttpHandler : DelegatingHandler
        {
            private readonly ITestOutputHelper _outputHelper;

            public HeaderLoggingHttpHandler(ITestOutputHelper outputHelper)
            {
                _outputHelper = outputHelper;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                foreach (var header in request.Headers)
                {
                    foreach (var val in header.Value)
                    {
                        _outputHelper.WriteLine($"REQUEST {header.Key}: {val}");
                    }


                }

                if (request.Content != null)
                {
                    _outputHelper.WriteLine("\n");
                    _outputHelper.WriteLine($"({request.Content.GetType()}");
                }
                else
                {
                    _outputHelper.WriteLine("(Empty Content)");
                }

                return base.SendAsync(request, cancellationToken);
            }
        }

        [Fact]
        public async Task Exists()
        {
            var (metadata, channel) = GetChannelAndMetadata();
            var kvClient = new Couchbase.Protostellar.KV.V1.KvService.KvServiceClient(channel);

            var existsRequests = new Couchbase.Protostellar.KV.V1.ExistsRequest() { Key = "ITEM_DOES_NOT_EXIST", BucketName = "default" };
            var existsResponse = await kvClient.ExistsAsync(existsRequests, new Grpc.Core.CallOptions(headers: metadata));
            Assert.NotNull(existsResponse);
            Assert.False(existsResponse.Result);
        }

        [Fact]
        public async Task Upsert()
        {
            var (metadata, channel) = GetChannelAndMetadata();
            var kvClient = new Couchbase.Protostellar.KV.V1.KvService.KvServiceClient(channel);

            var upsertRequest = new Couchbase.Protostellar.KV.V1.UpsertRequest() { Key = "UnitTestUpsert01", BucketName = "default" };
            upsertRequest.Content = Google.Protobuf.ByteString.CopyFromUtf8("{ 'id': 'foo' }");
            var upsertResponse = await kvClient.UpsertAsync(upsertRequest, new Grpc.Core.CallOptions(headers: metadata));
            Assert.NotEqual(0u, upsertResponse.Cas);
        }

        private (Metadata metadata, GrpcChannel channel) GetChannelAndMetadata()
        {
            var authBytes = System.Text.Encoding.UTF8.GetBytes("Administrator:password");
            var auth64 = Convert.ToBase64String(authBytes);
            var authHeader = $"Basic {auth64}";
            var metadata = new Grpc.Core.Metadata();
            metadata.Add("Authorization", authHeader);

            var callCredentials = CallCredentials.FromInterceptor((context, md) =>
            {
                md.Add("Authorization", authHeader);
                return Task.CompletedTask;
            });

            var channel = GrpcChannel.ForAddress("https://localhost:18098", new GrpcChannelOptions()
            {
                LoggerFactory = new TestOutputLoggerFactory(_outputHelper),
                HttpHandler = new SocketsHttpHandler()
                {
                    SslOptions = new SslClientAuthenticationOptions()
                        {RemoteCertificateValidationCallback = ((sender, certificate, chain, errors) => true)}
                }
            });
            return (metadata, channel);
        }
    }
}

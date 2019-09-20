using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Core.IO.Operations.Legacy.Authentication;
using Couchbase.Core.IO.Operations.Legacy.Collections;
using Couchbase.Core.IO.Operations.Legacy.Errors;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Services.KeyValue;
using SequenceGenerator = Couchbase.Core.IO.Operations.SequenceGenerator;

namespace Couchbase.Utils
{
    internal static class ConnectionExtensions
    {
        internal static async Task<Manifest> GetManifest(this IConnection connection)
        {
            var completionSource = new TaskCompletionSource<IMemoryOwner<byte>>();
            using (var manifestOp = new GetManifest
            {
                Converter = new DefaultConverter(),
                Transcoder = new DefaultTranscoder(new DefaultConverter()),
                Opaque = SequenceGenerator.GetNext(),
                Completed = s =>
                {
                    completionSource.TrySetResult(s.ExtractData());
                    return completionSource.Task;
                }
            })
            {
                await manifestOp.SendAsync(connection).ConfigureAwait(false);
                var manifestBytes = await completionSource.Task.ConfigureAwait(false);
                await manifestOp.ReadAsync(manifestBytes).ConfigureAwait(false);

                var manifestResult = manifestOp.GetResultWithValue();
                return manifestResult.Content;
            }
        }

        internal static async Task<short[]> Hello(this IConnection connection)
        {
            //TODO missing MutationSeqno (0x04) and ServerDuration (0x0f)
            var features = new List<short>
            {
                (short) ServerFeatures.SelectBucket,
                (short) ServerFeatures.Collections,
                (short) ServerFeatures.AlternateRequestSupport,
                (short) ServerFeatures.SynchronousReplication,
                (short) ServerFeatures.SubdocXAttributes,
                (short) ServerFeatures.XError
            };
            var completionSource = new TaskCompletionSource<IMemoryOwner<byte>>();
            using (var heloOp = new Hello
            {
                Key = Core.IO.Operations.Legacy.Hello.BuildHelloKey(connection.ConnectionId),
                Content = features.ToArray(),
                Converter = new DefaultConverter(),
                Transcoder = new DefaultTranscoder(new DefaultConverter()),
                Opaque = SequenceGenerator.GetNext(),
                Completed = s =>
                {
                    completionSource.TrySetResult(s.ExtractData());
                    return completionSource.Task;
                }
            })
            {
                await heloOp.SendAsync(connection).ConfigureAwait(false);
                var result = await completionSource.Task.ConfigureAwait(false);
                await heloOp.ReadAsync(result).ConfigureAwait(false);

                //returns all supported features
                return heloOp.GetResultWithValue().Content;
            }
        }

        public static async Task Authenticate(this IConnection connection, ClusterOptions clusterOptions, string bucketName)
        {
            var sasl = new PlainSaslMechanism(clusterOptions.UserName, clusterOptions.Password);
            var authenticated = await sasl.AuthenticateAsync(connection).ConfigureAwait(false);
            if (!authenticated)
            {
                throw new AuthenticationException($"Cannot authenticate {bucketName}");
            }
        }

        public static async Task SelectBucket(this IConnection connection, string bucketName)
        {
            var completionSource = new TaskCompletionSource<bool>();
            using (var selectBucketOp = new SelectBucket
            {
                Converter = new DefaultConverter(),
                Transcoder = new DefaultTranscoder(new DefaultConverter()),
                Key = bucketName,
                Completed = s =>
                {
                    completionSource.TrySetResult(s.Status == ResponseStatus.Success);
                    return completionSource.Task;
                }
            })
            {
                await selectBucketOp.SendAsync(connection).ConfigureAwait(false);
            }
        }

        public static async Task<ErrorMap> GetErrorMap(this IConnection connection)
        {
            var completionSource = new TaskCompletionSource<IMemoryOwner<byte>>();
            using (var errorMapOp = new GetErrorMap
            {
                Converter = new DefaultConverter(),
                Transcoder = new DefaultTranscoder(new DefaultConverter()),
                Opaque = SequenceGenerator.GetNext(),
                Completed = s =>
                {
                    completionSource.TrySetResult(s.ExtractData());
                    return completionSource.Task;
                }
            })
            {
                await errorMapOp.SendAsync(connection).ConfigureAwait(false);
                var result = await completionSource.Task.ConfigureAwait(false);
                await errorMapOp.ReadAsync(result).ConfigureAwait(false);

                return errorMapOp.GetResultWithValue().Content;
            }
        }

        internal static async Task<BucketConfig> GetClusterMap(this IConnection connection, IPEndPoint endPoint, Uri bootstrapUri)
        {
            var completionSource = new TaskCompletionSource<IMemoryOwner<byte>>();
            using (var configOp = new Config
            {
                CurrentHost = endPoint,
                Converter = new DefaultConverter(),
                Transcoder = new DefaultTranscoder(new DefaultConverter()),
                Opaque = SequenceGenerator.GetNext(),
                EndPoint = endPoint,
                Completed = s =>
                {
                    if (s.Status == ResponseStatus.Success)
                    {
                        completionSource.TrySetResult(s.ExtractData());
                    }
                    else
                    {
                        if (s.Status == ResponseStatus.KeyNotFound || s.Status == ResponseStatus.BucketNotConnected)
                        {
                            completionSource.TrySetResult(s.ExtractData());
                        }
                        else
                        {
                            completionSource.TrySetException(new KeyValueException(s.Status.ToString()));
                        }
                    }

                    return completionSource.Task;
                }
            })
            {
                await configOp.SendAsync(connection).ConfigureAwait(false);

                var clusterMapBytes = await completionSource.Task.ConfigureAwait(false);
                await configOp.ReadAsync(clusterMapBytes).ConfigureAwait(false);

                var configResult = configOp.GetResultWithValue();
                var config = configResult.Content;

                if (config != null)
                {
                    config.ReplacePlaceholderWithBootstrapHost(bootstrapUri);
                }

                return config;
            }
        }
    }
}

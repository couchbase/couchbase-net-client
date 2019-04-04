using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DataMapping;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Core.IO.Operations.Legacy.Authentication;
using Couchbase.Core.IO.Operations.Legacy.Collections;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Sharding;
using Couchbase.Services.Views;
using Couchbase.Utils;
using SequenceGenerator = Couchbase.Core.IO.Operations.SequenceGenerator;

namespace Couchbase
{
    internal interface IBucketSender
    {
        Task Send(IOperation op, TaskCompletionSource<IMemoryOwner<byte>> tcs);
    }

    public class CouchbaseBucket : IBucket, IBucketSender
    {
        internal const string DefaultScope = "_default";
        private readonly ICluster _cluster;
        private readonly ConcurrentDictionary<string, IScope> _scopes = new ConcurrentDictionary<string, IScope>();
        private readonly Lazy<IViewClient> _viewClientLazy;

        private bool _disposed;
        private BucketConfig _bucketConfig;
        private Manifest _manifest;
        private IKeyMapper _keyMapper;
        private IConfiguration _configuration;
        internal ConcurrentDictionary<IPEndPoint, IConnection> Connections = new ConcurrentDictionary<IPEndPoint, IConnection>();
        private bool _supportsCollections;

        public CouchbaseBucket(ICluster cluster, string name)
        {
            _cluster = cluster;
            Name = name;

            _viewClientLazy = new Lazy<IViewClient>(() =>
                new ViewClient(new CouchbaseHttpClient(_configuration, _bucketConfig), new JsonDataMapper(new DefaultSerializer()), _configuration)
            );
        }

        public string Name { get; }

        public Task<IScope> this[string name]
        {
            get
            {
                if (_scopes.TryGetValue(name, out var scope))
                {
                    return Task.FromResult(scope);
                }

                throw new ScopeMissingException("Cannot locate the scope {scopeName}");
            }
        }

        public Task<ICollection> DefaultCollection => Task.FromResult(_scopes[DefaultScope][CouchbaseCollection.DefaultCollection]);

        public async Task BootstrapAsync(Uri uri, IConfiguration configuration)
        {
            _configuration = configuration;

            var ipAddress = uri.GetIpAddress(false);
            var endPoint = new IPEndPoint(ipAddress, 11210);
            var connection = GetConnection(endPoint);

            await Authenticate(connection);
            await Negotiate(connection);
            await GetClusterMap(connection, endPoint);

            if (_supportsCollections)
            {
                await GetManifest(connection);
            }
            else
            {
                //build a fake scope and collection for pre-6.5 clusters
                var defaultCollection = new CouchbaseCollection(this, null, "_default");
                var defaultScope = new Scope("_default", "0", new List<ICollection> {defaultCollection}, this);
                _scopes.TryAdd("_default", defaultScope);
            }

            Connections.AddOrUpdate(endPoint, connection, (ep, conn) => connection);
            await LoadConnections(configuration);
        }

        private async Task LoadConnections(IConfiguration configuration)
        {
            foreach (var server in _bucketConfig.VBucketServerMap.ServerList)
            {
                var uri = new UriBuilder
                {
                    Scheme = Uri.UriSchemeHttp,
                    Host = server.Split(':')[0]
                }.Uri;

                var ipAddress = uri.GetIpAddress(false);
                var endpoint = new IPEndPoint(ipAddress, 11210);

                if (Connections.ContainsKey(endpoint)) continue;

                var connection = GetConnection(endpoint);
                await Authenticate(connection);
                await Negotiate(connection);
                Connections.AddOrUpdate(endpoint, connection, (ep, conn) => connection);
            }
        }

        IConnection GetConnection(IPEndPoint endPoint)
        {
            var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            var waitHandle = new ManualResetEvent(false);
            var asyncEventArgs = new SocketAsyncEventArgs
            {
                RemoteEndPoint = endPoint
            };
            asyncEventArgs.Completed += delegate { waitHandle.Set(); };

            if (socket.ConnectAsync(asyncEventArgs))
            {
                // True means the connect command is running asynchronously, so we need to wait for completion
                if (!waitHandle.WaitOne(10000))//default connect timeout
                {
                    socket.Dispose();
                    const int connectionTimedOut = 10060;
                    throw new SocketException(connectionTimedOut);
                }
            }

            if ((asyncEventArgs.SocketError != SocketError.Success) || !socket.Connected)
            {
                socket.Dispose();
                throw new SocketException((int)asyncEventArgs.SocketError);
            }

            socket.SetKeepAlives(true, 2*60*60*1000, 1000);

            return new MultiplexingConnection(null, socket, new DefaultConverter());
        }

        private async Task Authenticate(IConnection connection)
        {
            var sasl = new PlainSaslMechanism(_configuration.UserName, _configuration.Password);
            var authenticated = await sasl.AuthenticateAsync(connection).ConfigureAwait(false);
            if (authenticated)
            {
                var completionSource = new TaskCompletionSource<bool>();
                var selectBucketOp = new SelectBucket
                {
                    Converter = new DefaultConverter(),
                    Transcoder = new DefaultTranscoder(new DefaultConverter()),
                    Key = Name,
                    Completed = s =>
                    {
                        //Status will be Success if bucket select was bueno
                        completionSource.SetResult(s.Status == ResponseStatus.Success);
                        return completionSource.Task;
                    }
                };

                await selectBucketOp.SendAsync(connection).ConfigureAwait(false);
            }
            else
            {
                //cache exception for later use when op is used
            }
        }

        private async Task GetClusterMap(IConnection connection, IPEndPoint endPoint)
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
                    //Status will be Success if bucket select was bueno
                    completionSource.SetResult(s.ExtractData());
                    return completionSource.Task;
                }
            })
            {
                await configOp.SendAsync(connection).ConfigureAwait(false);

                var clusterMapBytes = await completionSource.Task.ConfigureAwait(false);
                await configOp.ReadAsync(clusterMapBytes).ConfigureAwait(false);

                var configResult = configOp.GetResultWithValue();
                _bucketConfig = configResult.Content; //the cluster map
                _keyMapper = new VBucketKeyMapper(_bucketConfig); //for vbucket key mapping
            }
        }

        private async Task Negotiate(IConnection connection)
        {
            var features = new List<short>
            {
                (short) ServerFeatures.SelectBucket,
                (short) ServerFeatures.Collections,
                (short) ServerFeatures.AlternateRequestSupport,
                (short) ServerFeatures.SynchronousReplication
            };
            var completionSource = new TaskCompletionSource<IMemoryOwner<byte>>();
            using (var heloOp = new Hello
            {
                Key = Hello.BuildHelloKey(1), //temp
                Content = features.ToArray(),
                Converter = new DefaultConverter(),
                Transcoder = new DefaultTranscoder(new DefaultConverter()),
                Opaque = SequenceGenerator.GetNext(),
                Completed = s =>
                {
                    //Status will be Success if bucket select was bueno
                    completionSource.SetResult(s.ExtractData());
                    return completionSource.Task;
                }
            })
            {
                await heloOp.SendAsync(connection).ConfigureAwait(false);
                var result = await completionSource.Task.ConfigureAwait(false);
                await heloOp.ReadAsync(result).ConfigureAwait(false);
                var supported = heloOp.GetResultWithValue();
                _supportsCollections = supported.Content.Contains((short) ServerFeatures.Collections);
            }
        }

        private async Task GetManifest(IConnection connection)
        {
            var completionSource = new TaskCompletionSource<IMemoryOwner<byte>>();
            using (var manifestOp = new GetManifest
            {
                Converter = new DefaultConverter(),
                Transcoder = new DefaultTranscoder(new DefaultConverter()),
                Opaque = SequenceGenerator.GetNext(),
                Completed = s =>
                {
                    //Status will be Success if bucket select was bueno
                    completionSource.SetResult(s.ExtractData());
                    return completionSource.Task;
                }
            })
            {
                await manifestOp.SendAsync(connection).ConfigureAwait(false);
                var manifestBytes = await completionSource.Task.ConfigureAwait(false);
                await manifestOp.ReadAsync(manifestBytes).ConfigureAwait(false);

                var manifestResult = manifestOp.GetResultWithValue();
                _manifest = manifestResult.Content;
            }

            //warmup the scopes/collections and cache them
            foreach (var scopeDef in _manifest.scopes)
            {
                var collections = new List<ICollection>();
                foreach (var collectionDef in scopeDef.collections)
                {
                    collections.Add(new CouchbaseCollection(this,
                        Convert.ToUInt32(collectionDef.uid), collectionDef.name));
                }

                _scopes.TryAdd(scopeDef.name, new Scope(scopeDef.name, scopeDef.uid, collections, this));
            }
        }

        public Task<IScope> Scope(string name)
        {
            return this[name];
        }

        private Uri GetViewUri()
        {
            var server = _bucketConfig.Nodes.GetRandom();
            var uri = new UriBuilder
            {
                Scheme = Uri.UriSchemeHttp,
                Host = server.hostname.Split(':')[0],
                Port = 8092
            }.Uri;
            return uri;
        }

        public Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName, ViewOptions options = default)
        {
            if (options == default)
            {
                options = new ViewOptions();
            }

            // create old style query
            var query = new ViewQuery(GetViewUri().ToString())
            {
                UseSsl = _configuration.UseSsl
            };
            query.Bucket(Name);
            query.From(designDocument, viewName);
            query.Stale(options.StaleState);
            query.Limit(options.Limit);
            query.Skip(options.Skip);
            query.StartKey(options.StartKey);
            query.StartKeyDocId(options.StartKeyDocId);
            query.EndKey(options.EndKey);
            query.EndKeyDocId(options.EndKeyDocId);
            query.InclusiveEnd(options.InclusiveEnd);
            query.Group(options.Group);
            query.GroupLevel(options.GroupLevel);
            query.Key(options.Key);
            query.Keys(options.Keys);
            query.GroupLevel(options.GroupLevel);
            query.Reduce(options.Reduce);
            query.Development(options.Development);
            query.ConnectionTimeout(options.ConnectionTimeout);

            if (options.Descending.HasValue)
            {
                if (options.Descending.Value)
                {
                    query.Desc();
                }
                else
                {
                    query.Asc();
                }
            }

            if (options.FullSet.HasValue && options.FullSet.Value)
            {
                query.FullSet();
            }

            return _viewClientLazy.Value.ExecuteAsync<T>(query);
        }

        public Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName, Action<ViewOptions> configureOptions)
        {
            var options = new ViewOptions();
            configureOptions(options);

            return ViewQueryAsync<T>(designDocument, viewName, options);
        }

        public Task<IViewResult<T>> SpatialViewQuery<T>(string designDocument, string viewName, SpatialViewOptions options = default)
        {
            if (options == default)
            {
                options = new SpatialViewOptions();
            }

            var uri = GetViewUri();

            // create old style query
            var query = new SpatialViewQuery(uri)
            {
                UseSsl = _configuration.UseSsl
            };
            query.Bucket(Name);
            query.From(designDocument, viewName);
            query.Stale(options.StaleState);
            query.Skip(options.Skip);
            query.Limit(options.Limit);
            query.StartRange(options.StartRange.ToList());
            query.EndRange(options.EndRange.ToList());
            query.Development(options.Development);
            query.ConnectionTimeout(options.ConnectionTimeout);

            return _viewClientLazy.Value.ExecuteAsync<T>(query);
        }

        public Task<IViewResult<T>> SpatialViewQuery<T>(string designDocument, string viewName, Action<SpatialViewOptions> configureOptions)
        {
            var options = new SpatialViewOptions();
            configureOptions(options);

            return SpatialViewQuery<T>(designDocument, viewName, options);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (var connection in Connections.Values)
            {
                connection.Dispose();
            }

            _disposed = true;
        }

        public async Task Send(IOperation op, TaskCompletionSource<IMemoryOwner<byte>> tcs)
        {
            var vBucket = (VBucket) _keyMapper.MapKey(op.Key);
            op.VBucketId = vBucket.Index; //hack - make vBucketIndex a short

            var node =  vBucket.LocatePrimary();
            await op.SendAsync(Connections[node]).ConfigureAwait(false);
        }
    }
}

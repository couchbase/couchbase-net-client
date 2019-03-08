using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Core.IO.Operations.Legacy.Authentication;
using Couchbase.Core.IO.Operations.Legacy.Collections;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Sharding;
using Couchbase.Services.Views;
using Couchbase.Utils;
using SequenceGenerator = Couchbase.Core.IO.Operations.SequenceGenerator;

namespace Couchbase
{
    internal interface IBucketSender
    {
        Task Send(IOperation op, TaskCompletionSource<byte[]> tcs);
    }

    public class CouchbaseBucket : IBucket, IBucketSender
    {
        internal const string DefaultScope = "_default";
        private readonly ICluster _cluster;
        private readonly ConcurrentDictionary<string, IScope> _scopes = new ConcurrentDictionary<string, IScope>();

        private bool _disposed;
        private BucketConfig _bucketConfig;
        private Manifest _manifest;
        private IKeyMapper _keyMapper;
        private IConfiguration _configuration;
        internal ConcurrentDictionary<IPEndPoint, IConnection> Connections = new ConcurrentDictionary<IPEndPoint, IConnection>();

        public CouchbaseBucket(ICluster cluster, string name)
        {
            _cluster = cluster;
            Name = name;
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

                throw new ScopeNotFoundException("Cannot locate the scope {scopeName");
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
            await GetClusterMap(connection, endPoint);
            await Negotiate(connection);   
            await GetManifest(connection);

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

                await connection.SendAsync(selectBucketOp.Write(), selectBucketOp.Completed)
                    .ConfigureAwait(false);
            }
            else
            {
                //cache exception for later use when op is used
            }
        }

        private async Task GetClusterMap(IConnection connection, IPEndPoint endPoint)
        {
            var completionSource = new TaskCompletionSource<byte[]>();
            var configOp = new Config
            {
                CurrentHost = endPoint,
                Converter = new DefaultConverter(),
                Transcoder = new DefaultTranscoder(new DefaultConverter()),
                Opaque = SequenceGenerator.GetNext(),
                EndPoint = endPoint,
                Completed = s =>
                {
                    //Status will be Success if bucket select was bueno
                    completionSource.SetResult(s.Data.ToArray());
                    return completionSource.Task;
                }
            };

            await connection.SendAsync(configOp.Write(), configOp.Completed).ConfigureAwait(false);

            var clusterMapBytes = await completionSource.Task.ConfigureAwait(false);
            await configOp.ReadAsync(clusterMapBytes).ConfigureAwait(false);

            var configResult = configOp.GetResultWithValue();
            _bucketConfig = configResult.Content;              //the cluster map
            _keyMapper = new VBucketKeyMapper(_bucketConfig);  //for vbucket key mapping
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
            var completionSource = new TaskCompletionSource<byte[]>();
            var heloOp = new Hello
            {
                Key = Hello.BuildHelloKey(1),//temp
                Content = features.ToArray(),
                Converter = new DefaultConverter(),
                Transcoder = new DefaultTranscoder(new DefaultConverter()),
                Opaque = SequenceGenerator.GetNext(),
                Completed = s =>
                {
                    //Status will be Success if bucket select was bueno
                    completionSource.SetResult(s.Data.ToArray());
                    return completionSource.Task;
                }
            };

            await connection.SendAsync(heloOp.Write(), heloOp.Completed).ConfigureAwait(false);
            var result = await completionSource.Task.ConfigureAwait(false);
            await heloOp.ReadAsync(result).ConfigureAwait(false);
            var supported = heloOp.GetResultWithValue();
        }

        private async Task GetManifest(IConnection connection)
        {
            var completionSource = new TaskCompletionSource<byte[]>();
            var manifestOp = new GetManifest
            {
                Converter = new DefaultConverter(),
                Transcoder = new DefaultTranscoder(new DefaultConverter()),
                Opaque = SequenceGenerator.GetNext(),
                Completed = s =>
                {
                    //Status will be Success if bucket select was bueno
                    completionSource.SetResult(s.Data.ToArray());
                    return completionSource.Task;
                }
            };

            await connection.SendAsync(manifestOp.Write(), manifestOp.Completed).ConfigureAwait(false);
            var manifestBytes = await completionSource.Task.ConfigureAwait(false);
            await manifestOp.ReadAsync(manifestBytes).ConfigureAwait(false);

            var manifestResult = manifestOp.GetResultWithValue();
            _manifest = manifestResult.Content;

            //warmup the scopes/collections and cache them
            foreach (var scopeDef in _manifest.scopes)
            {
                var collections = new List<ICollection>();
                foreach (var collectionDef in scopeDef.collections)
                {
                    collections.Add(new CouchbaseCollection(this, collectionDef.uid, collectionDef.name));
                }

                _scopes.TryAdd(scopeDef.name, new Scope(scopeDef.name, scopeDef.uid, collections, this));
            }
        }

        public Task<IScope> Scope(string name)
        {
            return this[name];
        }

        public Task<IViewResult> ViewQuery<T>(string statement, IViewOptions options)
        {
            throw new NotImplementedException();
        }

        public Task<ISpatialViewResult> SpatialViewQuery<T>(string statement, ISpatialViewOptions options)
        {
            throw new NotImplementedException();
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

        public async Task Send(IOperation op, TaskCompletionSource<byte[]> tcs)
        {
            var vBucket = (VBucket) _keyMapper.MapKey(op.Key);
            op.VBucketId = vBucket.Index; //hack - make vBucketIndex a short

            var node =  vBucket.LocatePrimary();
            await Connections[node].SendAsync(op.Write(), op.Completed).ConfigureAwait(false);
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using Enyim.Reflection;

namespace Couchbase.Configuration
{
    /// <summary>
    /// Configuration class
    /// </summary>
    public class CouchbaseClientConfiguration : ICouchbaseClientConfiguration
    {
        private Type nodeLocator;
        private ITranscoder transcoder;
        private IMemcachedKeyTransformer keyTransformer;
        private TimeSpan defaultHttpRequestTimeout = TimeSpan.FromMinutes(1);
        private TimeSpan defaultObserveTimeout = TimeSpan.FromMinutes(1);

        /// <summary>
        /// The number of loops to try over a set of nodes when cluster has changed
        /// (i.e. rebalance, failover, etc) and NotMyVBucket response is returned from
        /// the cluster. Most of the times this is resolved in the first iteration.
        /// </summary>
        private int _vBucketRetryCount = 2;

        private int _viewRetryCount = 2;

        #region

        /// <summary>
        /// Initializes a new instance of the <see cref="T:MemcachedClientConfiguration"/> class.
        /// </summary>
        public CouchbaseClientConfiguration()
        {
            this.HttpClientFactory = DefaultHttpClientFactory.Instance;

            this.Urls = new List<Uri>();

            this.SocketPool = new SocketPoolConfiguration();

            this.HeartbeatMonitor = new HeartbeatMonitorElement();

            this.HttpClient = new HttpClientElement() { Timeout = TimeSpan.Parse("00:01:15") };
        }

        /// <summary>
        /// Gets or sets the INameTransformer instance.
        /// </summary>
        public INameTransformer DesignDocumentNameTransformer { get; set; }

        public IHttpClientFactory HttpClientFactory { get; set; }

        INameTransformer ICouchbaseClientConfiguration.CreateDesignDocumentNameTransformer()
        {
            return this.DesignDocumentNameTransformer ??
                (this.DesignDocumentNameTransformer = new ProductionModeNameTransformer());
        }

        IHttpClient ICouchbaseClientConfiguration.CreateHttpClient(Uri baseUri)
        {
            return this.HttpClientFactory.Create(baseUri, Bucket, BucketPassword, HttpClient.Timeout, HttpClient.InitializeConnection);
        }

        #endregion

        /// <summary>
        /// Gets or sets the name of the bucket to be used. Can be overriden at the pool's constructor, and if not specified the "default" bucket will be used.
        /// </summary>
        public string Bucket { get; set; }

        /// <summary>
        /// Gets or sets the password used to connect to the bucket.
        /// </summary>
        /// <remarks> If null, the bucket name will be used. Set to String.Empty to use an empty password.</remarks>
        public string BucketPassword { get; set; }

        /// <summary>
        /// Gets or sets the admin username
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the admin password
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets a list of <see cref="T:IPEndPoint"/> each representing a Memcached server in the pool.
        /// </summary>
        public IList<Uri> Urls { get; private set; }

        [Obsolete("Please use the bucket name&password for specifying credentials. This property has no use now, and will be completely removed in the next version.", true)]
        public NetworkCredential Credentials { get; set; }

        /// <summary>
        /// Gets the configuration of the socket pool.
        /// </summary>
        public ISocketPoolConfiguration SocketPool { get; private set; }

        /// <summary>
        /// Gets or sets the configuration of the heartbeat monitor.
        /// </summary>
        public IHeartbeatMonitorConfiguration HeartbeatMonitor { get; set; }

        /// <summary>
        /// Gets or sets the configuration of the http client.
        /// </summary>
        public IHttpClientConfiguration HttpClient { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="T:Enyim.Caching.Memcached.IMemcachedKeyTransformer"/> which will be used to convert item keys for Memcached.
        /// </summary>
        public IMemcachedKeyTransformer KeyTransformer
        {
            get { return this.keyTransformer ?? (this.keyTransformer = new DefaultKeyTransformer()); }
            set { this.keyTransformer = value; }
        }

        /// <summary>
        /// Gets or sets the Type of the <see cref="T:Enyim.Caching.Memcached.IMemcachedNodeLocator"/> which will be used to assign items to Memcached nodes.
        /// </summary>
        /// <remarks>If both <see cref="M:NodeLocator"/> and  <see cref="M:NodeLocatorFactory"/> are assigned then the latter takes precedence.</remarks>
        public Type NodeLocator
        {
            get { return this.nodeLocator; }
            set
            {
                ConfigurationHelper.CheckForInterface(value, typeof(IMemcachedNodeLocator));
                this.nodeLocator = value;
            }
        }

        /// <summary>
        /// Gets or sets the NodeLocatorFactory instance which will be used to create a new IMemcachedNodeLocator instances.
        /// </summary>
        /// <remarks>If both <see cref="M:NodeLocator"/> and  <see cref="M:NodeLocatorFactory"/> are assigned then the latter takes precedence.</remarks>
        public IProviderFactory<IMemcachedNodeLocator> NodeLocatorFactory { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="T:Enyim.Caching.Memcached.ITranscoder"/> which will be used serialzie or deserialize items.
        /// </summary>
        public ITranscoder Transcoder
        {
            get { return this.transcoder ?? (this.transcoder = new DefaultTranscoder()); }
            set { this.transcoder = value; }
        }

        /// <summary>
        /// Gets or sets the <see cref="T:Enyim.Caching.Memcached.IPerformanceMonitor"/> instance which will be used monitor the performance of the client.
        /// </summary>
        public ICouchbasePerformanceMonitorFactory PerformanceMonitorFactory { get; set; }

        public int RetryCount { get; set; }

        public TimeSpan RetryTimeout { get; set; }

        public TimeSpan ObserveTimeout
        {
            get { return defaultObserveTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException("ObserveTimeout must be a positive TimeSpan");

                defaultObserveTimeout = value;
            }
        }

        public TimeSpan HttpRequestTimeout
        {
            get { return defaultHttpRequestTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException("HTTPRequestTimeout must be a positive TimeSpan");

                defaultHttpRequestTimeout = value;
            }
        }

        #region [ interface                     ]

        IList<Uri> ICouchbaseClientConfiguration.Urls
        {
            get { return this.Urls; }
        }

        ISocketPoolConfiguration ICouchbaseClientConfiguration.SocketPool
        {
            get { return this.SocketPool; }
        }

        IMemcachedKeyTransformer ICouchbaseClientConfiguration.CreateKeyTransformer()
        {
            return this.KeyTransformer;
        }

        IMemcachedNodeLocator ICouchbaseClientConfiguration.CreateNodeLocator()
        {
            var f = this.NodeLocatorFactory;
            if (f != null) return f.Create();

            return this.NodeLocator == null
                    ? new KetamaNodeLocator()
                    : (IMemcachedNodeLocator)FastActivator.Create(this.NodeLocator);
        }

        ITranscoder ICouchbaseClientConfiguration.CreateTranscoder()
        {
            return this.Transcoder;
        }

        string ICouchbaseClientConfiguration.Bucket
        {
            get { return this.Bucket; }
        }

        int ICouchbaseClientConfiguration.RetryCount
        {
            get { return this.RetryCount; }
        }

        TimeSpan ICouchbaseClientConfiguration.RetryTimeout
        {
            get { return this.RetryTimeout; }
        }

        string ICouchbaseClientConfiguration.BucketPassword
        {
            get { return this.BucketPassword; }
        }

        IPerformanceMonitor ICouchbaseClientConfiguration.CreatePerformanceMonitor()
        {
            return this.PerformanceMonitorFactory == null
                    ? null
                    : this.PerformanceMonitorFactory.Create(this.Bucket);
        }

        #endregion

        public int VBucketRetryCount { get { return _vBucketRetryCount; }}

        public int ViewRetryCount
        {
            get { return _viewRetryCount; }
            set
            {
                if (value < 0 || value > 10)
                {
                    const string msg = "Must be greater than 0 and less than or equal to 10.";
                    throw new ArgumentOutOfRangeException("value", msg);
                }
                _viewRetryCount = value;
            }
        }
    }

    internal class ReadOnlyConfig : ICouchbaseClientConfiguration
    {
        private string bucket;
        private string bucketPassword;
        private string username;
        private string password;
        private Uri[] urls;
        private TimeSpan retryTimeout;
        private int retryCount;
        private TimeSpan observeTimeout;
        private TimeSpan httpRequestTimeout;
        private ISocketPoolConfiguration spc;
        private IHeartbeatMonitorConfiguration hbm;
        private IHttpClientConfiguration hcc;
        private int _vBucketRetryCount = 2;
        private int _viewRetryCount = 2;

        private ICouchbaseClientConfiguration original;

        public ReadOnlyConfig(ICouchbaseClientConfiguration original)
        {
            this.bucket = original.Bucket;
            this.bucketPassword = original.BucketPassword;
            this.username = original.Username;
            this.password = original.Password;
            this.urls = original.Urls.ToArray();

            this.retryCount = original.RetryCount;
            this.retryTimeout = original.RetryTimeout;
            this.observeTimeout = original.ObserveTimeout;
            this.httpRequestTimeout = original.HttpRequestTimeout;

            this.spc = new SPC(original.SocketPool);
            this.hbm = new HBM(original.HeartbeatMonitor);
            this.hcc = new HCC(original.HttpClient);

            this.original = original;
            _vBucketRetryCount = original.VBucketRetryCount;
        }

        public void OverrideBucket(string bucketName, string bucketPassword)
        {
            this.bucket = bucketName;
            this.bucketPassword = bucketPassword;
        }

        string ICouchbaseClientConfiguration.Bucket
        {
            get { return this.bucket; }
        }

        string ICouchbaseClientConfiguration.BucketPassword
        {
            get { return this.bucketPassword; }
        }

        string ICouchbaseClientConfiguration.Username
        {
            get { return this.username; }
        }

        string ICouchbaseClientConfiguration.Password
        {
            get { return this.password; }
        }

        IList<Uri> ICouchbaseClientConfiguration.Urls
        {
            get { return this.urls; }
        }

        ISocketPoolConfiguration ICouchbaseClientConfiguration.SocketPool
        {
            get { return this.spc; }
        }

        IHeartbeatMonitorConfiguration ICouchbaseClientConfiguration.HeartbeatMonitor
        {
            get { return this.hbm; }
        }

        IHttpClientConfiguration ICouchbaseClientConfiguration.HttpClient
        {
            get { return this.hcc; }
        }

        IMemcachedKeyTransformer ICouchbaseClientConfiguration.CreateKeyTransformer()
        {
            return this.original.CreateKeyTransformer();
        }

        IMemcachedNodeLocator ICouchbaseClientConfiguration.CreateNodeLocator()
        {
            return this.original.CreateNodeLocator();
        }

        ITranscoder ICouchbaseClientConfiguration.CreateTranscoder()
        {
            return this.original.CreateTranscoder();
        }

        IPerformanceMonitor ICouchbaseClientConfiguration.CreatePerformanceMonitor()
        {
            return this.original.CreatePerformanceMonitor();
        }

        TimeSpan ICouchbaseClientConfiguration.ObserveTimeout
        {
            get { return this.observeTimeout; }
        }

        TimeSpan ICouchbaseClientConfiguration.HttpRequestTimeout
        {
            get { return this.httpRequestTimeout; }
        }

        TimeSpan ICouchbaseClientConfiguration.RetryTimeout
        {
            get { return this.retryTimeout; }
        }

        int ICouchbaseClientConfiguration.RetryCount
        {
            get { return this.retryCount; }
        }

        INameTransformer ICouchbaseClientConfiguration.CreateDesignDocumentNameTransformer() {
            return this.original.CreateDesignDocumentNameTransformer();
        }

        IHttpClient ICouchbaseClientConfiguration.CreateHttpClient(Uri baseUri) {
            return this.original.CreateHttpClient(baseUri);
        }

        /// <summary>
        /// Gets or sets the INameTransformer instance.
        /// </summary>
        public INameTransformer DesignDocumentNameTransformer { get; set; }

        public IHttpClientFactory HttpClientFactory { get; set; }

        private class SPC : ISocketPoolConfiguration
        {
            private TimeSpan connectionTimeout;
            private TimeSpan deadTimeout;
            private int maxPoolSize;
            private int minPoolSize;
            private TimeSpan queueTimeout;
            private TimeSpan receiveTimeout;
            private INodeFailurePolicyFactory fpf;
            private TimeSpan _lingerTime;
            private bool _lingerEnabled;

            public SPC(ISocketPoolConfiguration original)
            {
                this.connectionTimeout = original.ConnectionTimeout;
                this.deadTimeout = original.DeadTimeout;
                this.maxPoolSize = original.MaxPoolSize;
                this.minPoolSize = original.MinPoolSize;
                this.queueTimeout = original.QueueTimeout;
                this.receiveTimeout = original.ReceiveTimeout;
                this.fpf = original.FailurePolicyFactory;
                _lingerTime = original.LingerTime;
                _lingerEnabled = original.LingerEnabled;
                EnableTcpKeepAlives = original.EnableTcpKeepAlives;
                TcpKeepAliveInterval = original.TcpKeepAliveInterval;
                TcpKeepAliveTime = original.TcpKeepAliveTime;
            }

            int ISocketPoolConfiguration.MinPoolSize { get { return this.minPoolSize; } set { } }

            int ISocketPoolConfiguration.MaxPoolSize { get { return this.maxPoolSize; } set { } }

            TimeSpan ISocketPoolConfiguration.ConnectionTimeout { get { return this.connectionTimeout; } set { } }

            TimeSpan ISocketPoolConfiguration.QueueTimeout { get { return this.queueTimeout; } set { } }

            TimeSpan ISocketPoolConfiguration.ReceiveTimeout { get { return this.receiveTimeout; } set { } }

            TimeSpan ISocketPoolConfiguration.DeadTimeout { get { return this.deadTimeout; } set { } }

            INodeFailurePolicyFactory ISocketPoolConfiguration.FailurePolicyFactory { get { return this.fpf; } set { } }


            public TimeSpan LingerTime
            {
                get { return _lingerTime; }
                set { _lingerTime = value; }
            }

            public bool LingerEnabled
            {
                get { return _lingerEnabled; }
                set { _lingerEnabled = value; }
            }

            /// <summary>
            /// Gets or sets a value indicating whether enable TCP keep alives.
            /// </summary>
            /// <value>
            /// <c>true</c> to enable TCP keep alives; otherwise, <c>false</c>.
            /// </value>
            public bool EnableTcpKeepAlives { get; set; }

            /// <summary>
            /// Specifies the timeout, in milliseconds, with no activity until the first keep-alive packet is sent.
            /// </summary>
            /// <value>
            /// The TCP keep alive time in milliseconds.
            /// </value>
            /// <remarks>The default is 2hrs.</remarks>
            public uint TcpKeepAliveTime { get; set; }

            /// <summary>
            /// Specifies the interval, in milliseconds, between when successive keep-alive packets are sent if no acknowledgement is received.
            /// </summary>
            /// <value>
            /// The TCP keep alive interval in milliseconds..
            /// </value>
            /// <remarks>The default is 1 second.</remarks>
            public uint TcpKeepAliveInterval { get; set; }
        }

        private class HBM : IHeartbeatMonitorConfiguration
        {
            private string uri;
            private int interval;
            private bool enabled;

            public HBM(IHeartbeatMonitorConfiguration original)
            {
                this.interval = original.Interval;
                this.uri = original.Uri;
                this.enabled = original.Enabled;
            }

            string IHeartbeatMonitorConfiguration.Uri { get { return this.uri; } set { } }

            int IHeartbeatMonitorConfiguration.Interval { get { return this.interval; } set { } }

            bool IHeartbeatMonitorConfiguration.Enabled { get { return this.enabled; } set { } }
        }

        private class HCC : IHttpClientConfiguration
        {
            private bool initializeConnection;
            private TimeSpan timeout;

            public HCC(IHttpClientConfiguration original)
            {
                this.initializeConnection = original.InitializeConnection;
                this.timeout = original.Timeout;
            }

            bool IHttpClientConfiguration.InitializeConnection { get { return this.initializeConnection; } set { } }

            TimeSpan IHttpClientConfiguration.Timeout { get { return this.timeout; } set { } }
        }

        public int VBucketRetryCount
        {
            get { return _vBucketRetryCount; }
        }

        public int ViewRetryCount
        {
            get { return _viewRetryCount; }
            set
            {
                if (value < 0 || value > 10)
                {
                    const string msg = "Must be greater than 0 and less than or equal to 10.";
                    throw new ArgumentOutOfRangeException("value", msg);
                }
                _viewRetryCount = value;
            }
        }
    }
}

#region [ License information          ]
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
 *    @copyright 2010 Attila Kiskó, enyim.com
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
#endregion
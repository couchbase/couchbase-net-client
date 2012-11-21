using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Net;
using System.Web.Configuration;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using System.Diagnostics;

namespace Couchbase.Configuration
{
	/// <summary>
	/// Configures the <see cref="T:CouchbaseClient"/>. This class cannot be inherited.
	/// </summary>
	public class CouchbaseClientSection : ConfigurationSection, ICouchbaseClientConfiguration
	{
		[ConfigurationProperty("servers", IsRequired = true)]
		public ServersElement Servers
		{
			get { return (ServersElement)base["servers"]; }
			set { base["servers"] = value; }
		}

		/// <summary>
		/// Gets or sets the configuration of the socket pool.
		/// </summary>
		[ConfigurationProperty("socketPool", IsRequired = false)]
		public SocketPoolElement SocketPool
		{
			get { return (SocketPoolElement)base["socketPool"]; }
			set { base["socketPool"] = value; }
		}

		/// <summary>
		/// Gets or sets the configuration of the socket pool.
		/// </summary>
		[ConfigurationProperty("heartbeatMonitor", IsRequired = false)]
		public HeartbeatMonitorElement HeartbeatMonitor
		{
			get { return (HeartbeatMonitorElement)base["heartbeatMonitor"]; }
			set { base["heartbeatMonitor"] = value; }
		}

		/// <summary>
		/// Gets or sets the configuration of the http client.
		/// </summary>
		[ConfigurationProperty("httpClient", IsRequired = false)]
		public HttpClientElement HttpClient
		{
			get { return (HttpClientElement)base["httpClient"]; }
			set { base["httpClient"] = value; }
		}

		/// <summary>
		/// Gets or sets the <see cref="T:Enyim.Caching.Memcached.IMemcachedNodeLocator"/> which will be used to assign items to Memcached nodes.
		/// </summary>
		[ConfigurationProperty("locator", IsRequired = false)]
		public ProviderElement<IMemcachedNodeLocator> NodeLocator
		{
			get { return (ProviderElement<IMemcachedNodeLocator>)base["locator"]; }
			set { base["locator"] = value; }
		}

		/// <summary>
		/// Gets or sets the <see cref="T:Enyim.Caching.Memcached.IMemcachedKeyTransformer"/> which will be used to convert item keys for Memcached.
		/// </summary>
		[ConfigurationProperty("keyTransformer", IsRequired = false)]
		public ProviderElement<IMemcachedKeyTransformer> KeyTransformer
		{
			get { return (ProviderElement<IMemcachedKeyTransformer>)base["keyTransformer"]; }
			set { base["keyTransformer"] = value; }
		}

		/// <summary>
		/// Gets or sets the <see cref="T:Enyim.Caching.Memcached.ITranscoder"/> which will be used serialize or deserialize items.
		/// </summary>
		[ConfigurationProperty("transcoder", IsRequired = false)]
		public ProviderElement<ITranscoder> Transcoder
		{
			get { return (ProviderElement<ITranscoder>)base["transcoder"]; }
			set { base["transcoder"] = value; }
		}

		/// <summary>
		/// Gets or sets the <see cref="T:Enyim.Caching.Memcached.IPerformanceMonitor"/> which will be used monitor the performance of the client.
		/// </summary>
		[ConfigurationProperty("performanceMonitor", IsRequired = false)]
		public FactoryElement<ICouchbasePerformanceMonitorFactory> PerformanceMonitorFactory
		{
			get { return (FactoryElement<ICouchbasePerformanceMonitorFactory>)base["performanceMonitor"]; }
			set { base["performanceMonitor"] = value; }
		}

		/// <summary>
		/// Called after deserialization.
		/// </summary>
		protected override void PostDeserialize()
		{
			WebContext hostingContext = base.EvaluationContext.HostingContext as WebContext;

			if (hostingContext != null && hostingContext.ApplicationLevel == WebApplicationLevel.BelowApplication)
			{
				throw new InvalidOperationException("The " + this.SectionInformation.SectionName + " section cannot be defined below the application level.");
			}
		}

        /// <summary>
        /// Gets or sets the <see cref="T:Enyim.Caching.Memcached.IMemcachedNodeLocator"/> which will be used to assign items to Memcached nodes.
        /// </summary>
        [ConfigurationProperty("documentNameTransformer", IsRequired = false)]
        public ProviderElement<INameTransformer> DocumentNameTransformer {
            get
			{
				var provider = base["documentNameTransformer"] as ProviderElement<INameTransformer>;
				if (provider != null && provider.Type == null)
				{
					return new ProviderElement<INameTransformer>()
					{
						Type = typeof(ProductionModeNameTransformer)
					};
				}
				return (ProviderElement<INameTransformer>)base["documentNameTransformer"];
			}
            set { base["documentNameTransformer"] = value; }
        }

        [ConfigurationProperty("httpClientFactory", IsRequired = false)]
        public ProviderElement<IHttpClientFactory> HttpClientFactory {
			get
			{
				var provider = base["httpClientFactory"] as ProviderElement<IHttpClientFactory>;
				if (provider != null && provider.Type == null)
				{
					return new ProviderElement<IHttpClientFactory>()
					{
						Type = typeof(HammockHttpClientFactory)
					};
				}
				return (ProviderElement<IHttpClientFactory>)provider;
			}
            set { base["httpClientFactory"] = value; }
        }

        #region [ interface                     ]

        INameTransformer ICouchbaseClientConfiguration.CreateDesignDocumentNameTransformer() {
            return this.DocumentNameTransformer == null
                    ? null
                    : this.DocumentNameTransformer.CreateInstance();
        }

        IHttpClientFactory clientFactory;

        IHttpClient ICouchbaseClientConfiguration.CreateHttpClient(Uri baseUri) {
            if (this.clientFactory == null) {
                var tmp = this.HttpClientFactory;

                this.clientFactory = tmp == null ? HammockHttpClientFactory.Instance : tmp.CreateInstance();
            }

            Debug.Assert(this.clientFactory != null);

            return this.clientFactory.Create(baseUri, Servers.Bucket, Servers.BucketPassword, HttpClient.Timeout, HttpClient.InitializeConnection);
        }

        #endregion

		#region [ interface                     ]

		IList<Uri> ICouchbaseClientConfiguration.Urls
		{
			get { return this.Servers.Urls.ToUriCollection(); }
		}

		ISocketPoolConfiguration ICouchbaseClientConfiguration.SocketPool
		{
			get { return this.SocketPool; }
		}

		IHeartbeatMonitorConfiguration ICouchbaseClientConfiguration.HeartbeatMonitor
		{
			get { return this.HeartbeatMonitor; }
		}

		IHttpClientConfiguration ICouchbaseClientConfiguration.HttpClient
		{
			get { return this.HttpClient; }
		}

		IMemcachedKeyTransformer ICouchbaseClientConfiguration.CreateKeyTransformer()
		{
			return this.KeyTransformer.CreateInstance() ?? new DefaultKeyTransformer();
		}

		IMemcachedNodeLocator ICouchbaseClientConfiguration.CreateNodeLocator()
		{
			return this.NodeLocator.CreateInstance() ?? new KetamaNodeLocator();
		}

		ITranscoder ICouchbaseClientConfiguration.CreateTranscoder()
		{
			return this.Transcoder.CreateInstance() ?? new DefaultTranscoder();
		}

		IPerformanceMonitor ICouchbaseClientConfiguration.CreatePerformanceMonitor()
		{
			var pmf = this.PerformanceMonitorFactory;
			if (pmf.ElementInformation.IsPresent)
			{
				var f = pmf.CreateInstance();
				if (f != null)
					return f.Create(this.Servers.Bucket);
			}

			return null;
		}

		string ICouchbaseClientConfiguration.Bucket
		{
			get { return this.Servers.Bucket; }
		}

		string ICouchbaseClientConfiguration.BucketPassword
		{
			get { return this.Servers.BucketPassword; }
		}

		string ICouchbaseClientConfiguration.Username
		{
			get { return this.Servers.Username; }
		}

		string ICouchbaseClientConfiguration.Password
		{
			get { return this.Servers.Password; }
		}

		int ICouchbaseClientConfiguration.RetryCount
		{
			get { return this.Servers.RetryCount; }
		}

		TimeSpan ICouchbaseClientConfiguration.RetryTimeout
		{
			get { return this.Servers.RetryTimeout; }
		}

		TimeSpan ICouchbaseClientConfiguration.ObserveTimeout
		{
			get { return this.Servers.ObserveTimeout; }
		}

		TimeSpan ICouchbaseClientConfiguration.HttpRequestTimeout
		{
			get { return this.Servers.HttpRequestTimeout; }
		}

		#endregion
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

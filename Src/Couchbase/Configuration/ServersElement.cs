using System;
using System.ComponentModel;
using System.Configuration;

namespace Couchbase.Configuration
{
    /// <summary>
    /// Configures the <see cref="T:MemcachedClient"/>. This class cannot be inherited.
    /// </summary>
    public sealed class ServersElement : ConfigurationElement
    {
        private static readonly object NullObject = new object();

        protected override void Init()
        {
            base.Init();

            base["bucketPassword"] = NullObject;
        }

        /// <summary>
        /// Gets or sets the name of the bucket to be used. Can be overriden at the pool's constructor, and if not specified the "default" bucket will be used.
        /// </summary>
        [ConfigurationProperty("bucket", IsRequired = false)]
        public string Bucket
        {
            get { return (string)base["bucket"]; }
            set { base["bucket"] = value; }
        }

        /// <summary>
        /// Gets or sets the pasword used to connect to the bucket.
        /// </summary>
        /// <remarks> If null, the bucket name will be used. Set to String.Empty to use an empty password.</remarks>
        [ConfigurationProperty("bucketPassword", IsRequired = false)]
        public string BucketPassword
        {
            get { var v = base["bucketPassword"]; return v == NullObject ? null : v as string; }
            set { base["bucketPassword"] = value; }
        }

        /// <summary>
        /// Gets or sets the username used to connect to a secured cluster
        /// </summary>
        [ConfigurationProperty("username", IsRequired = false)]
        public string Username
        {
            get { return (string)base["username"]; }
            set { base["username"] = value; }
        }

        /// <summary>
        /// Gets or sets the password used to connect to a secured cluster
        /// </summary>
        [ConfigurationProperty("password", IsRequired = false)]
        public string Password
        {
            get { return (string)base["password"]; }
            set { base["password"] = value; }
        }

        /// <summary>
        /// Returns a collection of nodes in the cluster the client should use to retrieve the Memcached nodes.
        /// </summary>
        [ConfigurationProperty("", IsRequired = true, IsDefaultCollection = true)]
        public UriElementCollection Urls
        {
            get { return (UriElementCollection)base[""]; }
        }

        /// <summary>
        /// Determines which port the client should use to connect to the nodes
        /// </summary>
        [ConfigurationProperty("port", IsRequired = false, DefaultValue = BucketPortType.Direct)]
        [Obsolete]
        public BucketPortType Port
        {
            get { return (BucketPortType)base["port"]; }
            set { base["port"] = value; }
        }

        [ConfigurationProperty("retryCount", IsRequired = false, DefaultValue = 0)]
        public int RetryCount
        {
            get { return (int)base["retryCount"]; }
            set { base["retryCount"] = value; }
        }

        [ConfigurationProperty("retryTimeout", IsRequired = false, DefaultValue = "00:00:02"), PositiveTimeSpanValidator]
        [TypeConverter(typeof(TimeSpanConverter))]
        public TimeSpan RetryTimeout
        {
            get { return (TimeSpan)base["retryTimeout"]; }
            set { base["retryTimeout"] = value; }
        }

        [ConfigurationProperty("observeTimeout", IsRequired = false, DefaultValue = "00:01:00"), PositiveTimeSpanValidator]
        [TypeConverter(typeof(TimeSpanConverter))]
        public TimeSpan ObserveTimeout
        {
            get { return (TimeSpan)base["observeTimeout"]; }
            set { base["observeTimeout"] = value; }
        }

        [ConfigurationProperty("httpRequestTimeout", IsRequired = false, DefaultValue = "00:01:00"), PositiveTimeSpanValidator]
        [TypeConverter(typeof(TimeSpanConverter))]
        public TimeSpan HttpRequestTimeout
        {
            get { return (TimeSpan)base["httpRequestTimeout"]; }
            set { base["httpRequestTimeout"] = value; }
        }

        [ConfigurationProperty("vBucketRetryCount", IsRequired = false, DefaultValue = 2)]
        public int VBucketRetryCount
        {
            get { return (int)base["vBucketRetryCount"]; }
            set { base["vBucketRetryCount"] = value; }
        }

        [ConfigurationProperty("ViewRetryCount", IsRequired = false, DefaultValue = 2)]
        public int ViewRetryCount
        {
            get { return (int)base["ViewRetryCount"]; }
            set
            {
                if (value < 0 || value > 10)
                {
                    const string msg = "Must be greater than 0 and less than or equal to 10.";
                    throw new ArgumentOutOfRangeException("value", msg);
                }
                base["ViewRetryCount"] = value;
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
 *    @copyright 2010 Attila Kisk�, enyim.com
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
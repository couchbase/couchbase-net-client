using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Collections;
using Couchbase.Exceptions;

namespace Couchbase.Configuration
{
#pragma warning disable 649

    internal class ClusterInfo
    {
        public string name;
        public ClusterNode[] nodes;
        public ClusterBucketInfo buckets;
    }

    internal class BootstrapInfo
    {
        public string Name { get; set; }

        public string Uri { get; set; }

        public string StreamingUri { get; set; }
    }

    internal class ClusterBucketInfo
    {
        public string uri;
    }

    internal class ClusterConfig
    {
        public string name;

        public string uri;
        public string streamingUri;

        public ClusterNode[] nodes;

        public VBucketConfig vBucketServerMap;
        public VBucketConfig vBucketForwardServerMap;

        // mecached|Couchbase
        public string bucketType;

        // sasl
        public string authType;

        // password for the bucket
        public string saslPassword;

        public override int GetHashCode()
        {
            var cnehc = new Enyim.HashCodeCombiner();
            foreach (var node in nodes.OrderBy(x => x.HostName))
            {
                cnehc.Add(node.GetHashCode());
            }

            if (vBucketForwardServerMap != null)
                cnehc.Add(vBucketForwardServerMap.GetHashCode());

            if (vBucketServerMap != null)
                cnehc.Add(vBucketServerMap.GetHashCode());

            cnehc.Add(this.name.GetHashCode());
            cnehc.Add(this.streamingUri.GetHashCode());

            return cnehc.CurrentHash;
        }
    }

    internal class VBucketConfig
    {
        public string hashAlgorithm;
        public int numReplicas;
        public string[] serverList;
        public int[][] vBucketMap;

        public override int GetHashCode()
        {
            var ehc = new Enyim.HashCodeCombiner(this.hashAlgorithm.GetHashCode());
            ehc.Add(this.numReplicas);

            for (var i = 0; i < this.serverList.Length; i++)
                ehc.Add(this.serverList[i].GetHashCode());

            for (var i = 0; i < vBucketMap.Length; i++)
            {
                var ehc2 = new Enyim.HashCodeCombiner();
                var tmp = vBucketMap[i];

                for (var j = 0; j < tmp.Length; j++)
                    ehc2.Add(tmp[j]);

                ehc.Add(ehc2.CurrentHash);
            }

            return ehc.CurrentHash;
        }
    }

    internal class ClusterNode
    {
        private static readonly Type[] SupportedTypes = { typeof(ClusterNode) };

        internal static readonly System.Web.Script.Serialization.JavaScriptConverter PoolsConfigConverterInstance = new PoolsConfigConverter();
        internal static readonly System.Web.Script.Serialization.JavaScriptConverter BootstrapConfigConverterInstance = new BootstrapConfigConverter();
        internal static readonly IEqualityComparer<ClusterNode> ComparerInstance = new Comparer();

        private string hostNname;

        public ClusterNode()
        {
        }

        public string HostName
        {
            get { return this.hostNname; }
            set
            {
                var tmp = value;

                // strip the management port (mc server 1.0.3> & Couchbase 1.6>)
                if (!String.IsNullOrEmpty(tmp))
                {
                    var index = tmp.IndexOf(':');
                    if (index > 0)
                        tmp = tmp.Substring(0, index);
                }

                this.hostNname = tmp;
            }
        }

        public int Port { get; private set; }

        public string Status { get; private set; }

        public string Version { get; private set; }

        public Dictionary<string, object> ConfigurationData { get; private set; }

        public override int GetHashCode()
        {
            return Enyim.HashCodeCombiner.Combine(
                    this.hostNname == null ? -1 : this.hostNname.GetHashCode(),
                    this.Status == null ? -1 : this.Status.GetHashCode(),
                    Port);
        }

        #region [ Comparer                     ]

        private class Comparer : IEqualityComparer<ClusterNode>
        {
            bool IEqualityComparer<ClusterNode>.Equals(ClusterNode x, ClusterNode y)
            {
                return x.HostName == y.HostName
                        && x.Port == y.Port
                        && x.Status == y.Status;
            }

            int IEqualityComparer<ClusterNode>.GetHashCode(ClusterNode obj)
            {
                return obj.GetHashCode();
            }
        }

        #endregion

        #region [ JSON Converter               ]

        private class PoolsConfigConverter : System.Web.Script.Serialization.JavaScriptConverter
        {
            public override object Deserialize(IDictionary<string, object> dictionary, Type type, System.Web.Script.Serialization.JavaScriptSerializer serializer)
            {
                var retval = new ClusterNode();

                retval.HostName = GetRequired<string>(dictionary, "hostname");
                retval.Status = GetRequired<string>(dictionary, "status");
                retval.Version = GetRequired<string>(dictionary, "version");

                var ports = GetRequired<IDictionary<string, object>>(dictionary, "ports");
                if (ports != null)
                    retval.Port = GetRequired<int>(ports, "direct");

                retval.ConfigurationData = new Dictionary<string, object>(dictionary);

                return retval;
            }

            private static TResult GetRequired<TResult>(IDictionary<string, object> dict, string key)
            {
                object tmp;

                if (!dict.TryGetValue(key, out tmp))
                    throw new InvalidOperationException(String.Format("Key '{0}' was not found in the cluster node info.", key));

                return (TResult)tmp;
            }

            public override IDictionary<string, object> Serialize(object obj, System.Web.Script.Serialization.JavaScriptSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override IEnumerable<Type> SupportedTypes
            {
                get { return ClusterNode.SupportedTypes; }
            }
        }

        private class BootstrapConfigConverter : System.Web.Script.Serialization.JavaScriptConverter
        {
            public override object Deserialize(IDictionary<string, object> dictionary, Type type, System.Web.Script.Serialization.JavaScriptSerializer serializer)
            {
                var info = new BootstrapInfo();

                object poolsConfig;
                if (dictionary.TryGetValue("pools", out poolsConfig))
                {
                    var pools = poolsConfig as ArrayList;
                    if (pools != null && pools.Count > 0)
                    {
                        var dict = pools[0] as Dictionary<string, object>;
                        info.Name = dict["name"] as string;
                        info.StreamingUri = dict["streamingUri"] as string;
                        info.Uri = dict["uri"] as string;
                    }
                    else
                    {
                        throw new BootstrapConfigurationException("Pools element was empty in the bootstrap response.");
                    }
                }
                else
                {
                    throw new BootstrapConfigurationException("Pools element was not found in the bootstrap response.");
                }

                return info;
            }

            public override IDictionary<string, object> Serialize(object obj, System.Web.Script.Serialization.JavaScriptSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override IEnumerable<Type> SupportedTypes
            {
                get { return new Type[] { typeof(BootstrapInfo) }; }
            }
        }

        #endregion
    }

#pragma warning restore 649
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase.Management
{
    public class Bucket
    {
        private const int MIN_RAM_QUOTA = 100;

        public string Name { get; set; }

        public BucketTypes BucketType { get; set; }

        public AuthTypes AuthType { get; set; }

        public FlushOptions FlushOption { get; set; }

        public int ProxyPort { get; set; }

        public string Password { get; set; }

        public string SaslPassword { get; set; }

        public IDictionary<string, string> ValidationErrors { get; set; }

        public IList<Node> Nodes { get; set; }

        public BasicStats BasicStats { get; set; }

        public bool ReplicaIndex { get; set; }

        public string Uri { get; set; }

        public string StreamingUri { get; set; }

        public string LocalRandomKeyUri { get; set; }

        public Controllers Controllers { get; set; }

        public Stats Stats { get; set; }

        public DDocs DDocs { get; set; }

        public string NodeLocator { get; set; }

        public bool AutoCompactionSettings { get; set; }

        public bool FastWarmupSettings { get; set; }

        public string UUID { get; set; }

        public ReplicaNumbers ReplicaNumber { get; set; }

        public Quota Quota { get; set; }

        public string BucketCapabilitiesVer { get; set; }

        public IList<string> BucketCapabilities { get; set; }

        public VBucketServerMap VBucketServerMap { get; set; }

        public bool IsValid()
        {
            ValidationErrors = new Dictionary<string, string>();

            if (AuthType == AuthTypes.Empty)
                ValidationErrors["AuthType"] = "AuthType cannot be Empty";

            if (BucketType == BucketTypes.Empty)
                ValidationErrors["BucketType"] = "BucketType cannot be Empty";

            if (ReplicaNumber == ReplicaNumbers.Empty)
                ValidationErrors["ReplicaNumber"] = "ReplicaNumber cannot be Empty";

            if (string.IsNullOrEmpty(Name))
                ValidationErrors["Name"] = "Name must be specified";

            if (Quota == null || Quota.RAM < MIN_RAM_QUOTA)
                ValidationErrors["RamQuotaMB"] = "RamQuotaMB must be at least " + MIN_RAM_QUOTA;

            if (ProxyPort < 0)
                ValidationErrors["ProxyPort"] = "ProxyPort must be a greater than or equal to 0";

            if (AuthType == AuthTypes.None && ProxyPort == 0)
                ValidationErrors["ProxyPort"] = "ProxyPort is required when AuthType is 'none'";

            if (AuthType == AuthTypes.Sasl && ProxyPort > 0)
                ValidationErrors["ProxyPort"] = "ProxyPort may not be used with AuthType 'sasl'";

            return ValidationErrors.Keys.Count == 0;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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
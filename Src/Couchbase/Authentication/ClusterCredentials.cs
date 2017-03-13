using System;
using System.Collections.Generic;
using System.Security.Authentication;
using Couchbase.Utils;

namespace Couchbase.Authentication
{
    [Obsolete("Please use an implementation of IAuthenticator instead.")]
    public class ClusterCredentials : IClusterCredentials
    {
        public ClusterCredentials()
        {
            BucketCredentials = new Dictionary<string, string>();
        }

        public string ClusterPassword { get; set; }

        public string ClusterUsername { get; set; }

        public Dictionary<string, string> BucketCredentials { get; set; }

        public void AddBucket(string bucketPassword, string bucketName)
        {
           BucketCredentials.Add(bucketName, bucketPassword);
        }

        public Dictionary<string, string> GetCredentials(AuthContext context, string bucketName)
        {
            switch (context)
            {
                case AuthContext.BucketKv:
                case AuthContext.BucketN1Ql:
                    string bucketPassword;
                    if (BucketCredentials.TryGetValue(bucketName, out bucketPassword))
                    {
                        return new Dictionary<string, string> {{bucketName, bucketPassword}};
                    }
                    throw new AuthenticationException(ExceptionUtil.GetMessage(ExceptionUtil.BucketCredentialsMissingMsg, bucketName));
                case AuthContext.ClusterCbft:
                case AuthContext.ClusterN1Ql:
                    return new Dictionary<string, string>(BucketCredentials);
                case AuthContext.ClusterMgmt:
                    return new Dictionary<string, string> { { ClusterUsername, ClusterPassword } };
                default:
                    throw new ArgumentOutOfRangeException("context", context, null);
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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

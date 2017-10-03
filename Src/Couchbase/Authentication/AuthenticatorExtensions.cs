using System;
using System.Collections.Generic;
using System.Security.Authentication;
using Couchbase.Utils;

namespace Couchbase.Authentication
{
    internal static class AuthenticatorExtensions
    {
        public static Dictionary<string, string> GetCredentials(this IAuthenticator authenticator, AuthContext context, string bucketName = null)
        {
            if (authenticator.AuthenticatorType == AuthenticatorType.Password)
            {
                var passwordAuthenticator = (PasswordAuthenticator) authenticator;
                return new Dictionary<string, string>
                {
                    {passwordAuthenticator.Username, passwordAuthenticator.Password}
                };
            }

            var classicAuthenticator = (ClassicAuthenticator) authenticator;
            switch (context)
            {
                case AuthContext.BucketKv:
                case AuthContext.BucketN1Ql:
                    string bucketPassword;
                    if (classicAuthenticator.BucketCredentials.TryGetValue(bucketName, out bucketPassword))
                    {
                        return new Dictionary<string, string>
                        {
                            {bucketName, bucketPassword}
                        };
                    }
                    throw new AuthenticationException(ExceptionUtil.GetMessage(ExceptionUtil.BucketCredentialsMissingMsg, bucketName));
                case AuthContext.ClusterCbft:
                case AuthContext.ClusterN1Ql:
                case AuthContext.ClusterAnalytics:
                    return new Dictionary<string, string>(classicAuthenticator.BucketCredentials);
                case AuthContext.ClusterMgmt:
                    return new Dictionary<string, string>
                    {
                        {classicAuthenticator.ClusterUsername, classicAuthenticator.ClusterPassword}
                    };
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
 *    @copyright 2017 Couchbase, Inc.
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

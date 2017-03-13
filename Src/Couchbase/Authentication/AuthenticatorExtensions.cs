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
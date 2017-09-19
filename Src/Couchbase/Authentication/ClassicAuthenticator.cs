using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using Couchbase.Utils;

namespace Couchbase.Authentication
{
    /// <summary>
    /// The classic authenticator uses a bucket name and password to authenticate.
    /// </summary>
    /// <seealso cref="IAuthenticator" />
    public class ClassicAuthenticator : IAuthenticator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClassicAuthenticator"/> class.
        /// </summary>
        public ClassicAuthenticator()
        {
            BucketCredentials = new Dictionary<string, string>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClassicAuthenticator"/> class.
        /// </summary>
        /// <param name="password">The password.</param>
        /// <exception cref="System.ArgumentException">password cannot be null or empty</exception>
        public ClassicAuthenticator(string password)
            : this()
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("password cannot be null or empty");
            }

            ClusterPassword = password;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClassicAuthenticator"/> class.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <exception cref="System.ArgumentException">username cannot be null or empty</exception>
        public ClassicAuthenticator(string username, string password)
            : this(password)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("username cannot be null or empty");
            }

            ClusterUsername = username;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClassicAuthenticator"/> class using a legacy <see cref="IClusterCredentials"/>.
        /// </summary>
        /// <param name="credentials">The cluster credentials.</param>
        internal ClassicAuthenticator(IClusterCredentials credentials)
        {
            var classicCredentials = (ClusterCredentials) credentials;
            ClusterUsername = classicCredentials.ClusterUsername;
            ClusterPassword = classicCredentials.ClusterPassword;
            BucketCredentials = credentials.BucketCredentials;
        }

        /// <summary>
        /// Gets the cluster username.
        /// </summary>
        public string ClusterUsername { get; internal set; }

        /// <summary>
        /// Gets the cluster password.
        /// </summary>
        public string ClusterPassword { get; private set; }

        /// <summary>
        /// Gets the stored bucket credentials.
        /// </summary>
        public Dictionary<string, string> BucketCredentials { get; private set; }

        /// <summary>
        /// Adds a bucket name and password combination to the authenticator.
        /// </summary>
        /// <param name="bucketName">Name of the bucket.</param>
        /// <param name="bucketPassword">The bucket password.</param>
        public void AddBucketCredential(string bucketName, string bucketPassword)
        {
            BucketCredentials.Add(bucketName, bucketPassword);
        }

        /// <summary>
        /// Gets the type of the authenticator.
        /// </summary>
        public AuthenticatorType AuthenticatorType => AuthenticatorType.Classic;

        /// <inheritdoc />
        public void Validate()
        {
            if (!BucketCredentials.Any())
            {
                throw new ArgumentException(ExceptionUtil.NoBucketCredentialsDefined);
            }
        }
    }
}

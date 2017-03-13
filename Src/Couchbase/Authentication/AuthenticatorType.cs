namespace Couchbase.Authentication
{
    /// <summary>
    /// The authenticator mechanism to authenticate with the cluster.
    /// </summary>
    public enum AuthenticatorType
    {
        /// <summary>
        /// Classic credentials with a bucket name and password.
        /// </summary>
        Classic,

        /// <summary>
        /// Role Based Access Control (RBAC) with a username and password.
        /// </summary>
        Password
    }
}
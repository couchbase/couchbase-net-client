using System;
using Couchbase.Utils;

namespace Couchbase.Authentication
{
    /// <summary>
    /// The PasswordAuthenticator uses an application level uesrname and password to authenticate.
    /// </summary>
    /// <seealso cref="Couchbase.Authentication.IAuthenticator" />
    public class PasswordAuthenticator : IAuthenticator
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="PasswordAuthenticator"/> class.
        /// </summary>
        /// <param name="password">The password.</param>
        /// <exception cref="System.ArgumentException">password cannot be null or empty</exception>
        public PasswordAuthenticator(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("password cannot be null or empty");
            }

            Password = password;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PasswordAuthenticator"/> class.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <exception cref="System.ArgumentException">username cannot be null or empty</exception>
        public PasswordAuthenticator(string username, string password)
            : this(password)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("username cannot be null or empty");
            }

            Username = username;
        }
        /// <summary>
        /// Gets the username.
        /// </summary>
        public string Username { get; internal set; }

        /// <summary>
        /// Gets the password.
        /// </summary>
        public string Password { get; private set; }

        /// <summary>
        /// Gets the type of the authenticator.
        /// </summary>
        public AuthenticatorType AuthenticatorType => AuthenticatorType.Password;

        /// <inheritdoc />
        /// <remarks>Duplicates password and username logic within constructors.</remarks>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Password))
            {
                throw new ArgumentException(ExceptionUtil.NoPasswordDefined);
            }
            if (string.IsNullOrWhiteSpace(Username))
            {
                throw new ArgumentException(ExceptionUtil.NoUsernameDefined);
            }
        }
    }
}

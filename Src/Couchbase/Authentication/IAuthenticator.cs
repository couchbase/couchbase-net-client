namespace Couchbase.Authentication
{
    /// <summary>
    /// Interface for all Authenticators.
    /// </summary>
    public interface IAuthenticator
    {
        /// <summary>
        /// Gets the type of the authenticator.
        /// </summary>
        AuthenticatorType AuthenticatorType { get; }

        /// <summary>
        /// Used internally to validate the <see cref="IAuthenticator"/> state; i.e. password exists, etc
        /// </summary>
        void Validate();
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

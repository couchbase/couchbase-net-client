using Couchbase.Core.Compatibility;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Query.Conversational
{
    /// <summary>
    /// The credential Query uses to call the model. It is separate from the Couchbase credential.
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public sealed class ModelCredential
    {
        internal enum CredentialKind
        {
            Capella,
            CredentialStore,
            ApiKey
        }

        private ModelCredential(CredentialKind kind, string secret, string? organizationId = null)
        {
            Kind = kind;
            Secret = secret;
            OrganizationId = organizationId;
        }

        internal CredentialKind Kind { get; }

        // "email:password", the credential store name or the API key, depending on Kind.
        internal string Secret { get; }

        internal string? OrganizationId { get; }

        /// <summary>
        /// Uses the Capella iQ model with a Capella user and organization.
        /// </summary>
        /// <param name="email">The email of the Capella user.</param>
        /// <param name="password">The password of the Capella user.</param>
        /// <param name="organizationId">The Capella organization ID.</param>
        /// <returns>A new <see cref="ModelCredential"/>.</returns>
        public static ModelCredential Capella(string email, string password, string organizationId)
        {
            ThrowIfNullOrEmpty(email, nameof(email));
            ThrowIfNullOrEmpty(password, nameof(password));
            ThrowIfNullOrEmpty(organizationId, nameof(organizationId));
            return new ModelCredential(CredentialKind.Capella, email + ":" + password, organizationId);
        }

        /// <summary>
        /// Uses a credential that is stored on the Query service under a name.
        /// </summary>
        /// <param name="credentialName">The name of the stored credential.</param>
        /// <returns>A new <see cref="ModelCredential"/>.</returns>
        public static ModelCredential CredentialStore(string credentialName)
        {
            ThrowIfNullOrEmpty(credentialName, nameof(credentialName));
            return new ModelCredential(CredentialKind.CredentialStore, credentialName);
        }

        /// <summary>
        /// Uses an API key of the model provider.
        /// </summary>
        /// <param name="key">The API key.</param>
        /// <returns>A new <see cref="ModelCredential"/>.</returns>
        public static ModelCredential ApiKey(string key)
        {
            ThrowIfNullOrEmpty(key, nameof(key));
            return new ModelCredential(CredentialKind.ApiKey, key);
        }

        /// <summary>
        /// Returns the kind of credential. It never contains secrets.
        /// </summary>
        public override string ToString() => $"ModelCredential {{ Kind = {Kind} }}";

        private static void ThrowIfNullOrEmpty(string value, string name)
        {
            if (string.IsNullOrEmpty(value))
            {
                ThrowHelper.ThrowInvalidArgumentException($"{name} cannot be null or empty.");
            }
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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

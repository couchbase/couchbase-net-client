using System;
using System.Text;
using Couchbase.Core.Exceptions;

#nullable enable

namespace Couchbase.Management.Users
{
    public class Role
    {
        /// <summary>
        /// Creates a new system-wide role (not specific to a bucket)
        /// </summary>
        /// <param name="roleName">symbolic name of the role</param>
        public Role(string roleName) : this(roleName, null)
        {
        }

        /// <summary>
        /// Creates a new role. If the bucket parameter is null, a system-wide role is created.
        /// Otherwise, the role applies to all scopes and collections within the bucket.
        /// </summary>
        /// <param name="roleName">symbolic name of the role.</param>
        /// <param name="bucketName">bucket name for the role.</param>
        public Role(string roleName, string? bucketName)
            : this(roleName, bucketName, null, null)
        {
        }

        /// <summary>
        /// Creates a new role. If the bucket parameter is null, a system-wide role is created.
        /// Otherwise, the role applies to the scope and collections in the scope and bucket.
        /// </summary>
        /// <param name="roleName"></param>
        /// <param name="bucketName"></param>
        /// <param name="scopeName"></param>
        public Role(string roleName, string? bucketName, string? scopeName)
            : this(roleName, bucketName, scopeName, null)
        {
        }

        /// <summary>
        /// Creates a new role. If the bucket parameter is null, a system-wide role is created.
        /// Otherwise, the role applies to the scope and collection provided in the bucket.
        /// </summary>
        /// <param name="roleName"></param>
        /// <param name="bucketName"></param>
        /// <param name="scopeName"></param>
        /// <param name="collectionName"></param>
        public Role(string roleName, string? bucketName, string? scopeName, string? collectionName)
        {
            Name = roleName;
            Bucket = bucketName;
            Scope = scopeName;
            Collection = collectionName;
        }

        /// <summary>
        /// The name of the Role.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gives the Role access to a specific Bucket.
        /// </summary>
        public string? Bucket { get; }

        /// <summary>
        /// Gives the Role access to a specific Scope.
        /// <remarks>Bucket must be non-null nor a wildcard (*) for Scope to be set.</remarks>
        /// </summary>
        /// <remarks>Uncommitted: this feature may change in the future.</remarks>
        public string? Scope { get; }

        /// <summary>
        /// Gives the Role access to a specific Collection.
        /// <remarks>Bucket and Scope must be non-null nor a wildcard (*)  for Scope to be set.</remarks>
        /// <remarks>Uncommitted: this feature may change in the future.</remarks>
        /// </summary>
        public string? Collection { get; }

        /// <summary>
        /// Validates that the internal state of the Role.
        /// </summary>
        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new InvalidArgumentException("A Role requires the Name field to be non-null or empty.");
            }

            if (string.IsNullOrWhiteSpace(Scope) && IsValidBucket())
            {
                throw new InvalidArgumentException("When a scope is specified, the bucket cannot be null or wildcard");
            }

            if (!string.IsNullOrWhiteSpace(Collection) && string.IsNullOrWhiteSpace(Scope))
            {
                throw new InvalidArgumentException("When a collection is specified, the scope cannot be null or wildcard.");
            }
        }

        private bool IsValidBucket()
        {
            return string.IsNullOrWhiteSpace(Bucket) &&
                   string.Equals(Bucket, "*", StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Name);

            if (!string.IsNullOrEmpty(Bucket)) { sb.Append("[").Append(Bucket); }

            if (!string.IsNullOrWhiteSpace(Scope))
            {
                sb.Append(":").Append(Scope);

                if (!string.IsNullOrWhiteSpace(Collection)) sb.Append(":").Append(Collection);
            }

            return !string.IsNullOrEmpty(Bucket) ? sb.Append("]").ToString() : sb.ToString();
        }

        internal static Role FromJson(RoleDto roleDto) =>
            new(roleDto.Role, roleDto.BucketName, roleDto.ScopeName, roleDto.CollectionName);
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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

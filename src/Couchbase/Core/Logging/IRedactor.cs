using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace Couchbase.Core.Logging
{
    /// <summary>
    /// Wraps a log argument in redaction tags according to the configured
    /// <see cref="Couchbase.ClusterOptions.RedactionLevel"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is redaction as seen from outside the SDK. Obtain the cluster's redactor with
    /// <c>cluster.ClusterServices.GetRequiredService&lt;IRedactor&gt;()</c> to redact your own log
    /// arguments the way the SDK does, so that one tool can strip both.
    /// </para>
    /// <para>
    /// The SDK does not redact through this interface. It uses an internal type whose equivalent methods
    /// are generic, so that log arguments are not boxed and redaction can be inlined; this interface
    /// exists for callers outside the assembly, which cannot name that type. A call through it therefore
    /// boxes the result and, for value-type arguments, the argument before any log level is consulted. Consequently, registering an
    /// implementation of <see cref="IRedactor"/> as a cluster service does not change how the SDK redacts
    /// — the registration is discarded and a warning is logged. Use
    /// <see cref="Couchbase.ClusterOptions.RedactionLevel"/> to control redaction.
    /// </para>
    /// </remarks>
    public interface IRedactor
    {
        /// <summary>
        /// Redact user data, such as document keys, usernames and query statements.
        /// </summary>
        /// <param name="message">The value to redact, or null.</param>
        /// <returns>
        /// A value that applies the redaction when it is converted to a string, or null if
        /// <paramref name="message"/> was null. The redacted string is not built until then.
        /// </returns>
        [return: NotNullIfNotNull("message")]
        object? UserData(object? message);

        /// <summary>
        /// Redact metadata, such as bucket, scope and collection names.
        /// </summary>
        /// <param name="message">The value to redact, or null.</param>
        /// <returns>
        /// A value that applies the redaction when it is converted to a string, or null if
        /// <paramref name="message"/> was null. Not redacted at
        /// <see cref="RedactionLevel.Partial"/>.
        /// </returns>
        [return: NotNullIfNotNull("message")]
        object? MetaData(object? message);

        /// <summary>
        /// Redact system data, such as hostnames and ports.
        /// </summary>
        /// <param name="message">The value to redact, or null.</param>
        /// <returns>
        /// A value that applies the redaction when it is converted to a string, or null if
        /// <paramref name="message"/> was null. Not redacted at
        /// <see cref="RedactionLevel.Partial"/>.
        /// </returns>
        [return: NotNullIfNotNull("message")]
        object? SystemData(object? message);
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

using Couchbase.Core.IO.Operations;

#nullable enable

namespace Couchbase.Core.Logging
{
    /// <summary>
    /// Redaction helpers for error contexts, which store plain strings rather than
    /// <see cref="Redacted{T}"/>. These mirror the like-named methods on <see cref="TypedRedactor"/>
    /// so that every error-context assignment reads the same way regardless of which redactor
    /// abstraction the enclosing class holds.
    /// </summary>
    internal static class RedactorExtensions
    {
        public static string? UserDataString(this IRedactor redactor, string? value) =>
            string.IsNullOrEmpty(value) ? value : redactor.UserData(value)?.ToString();

        public static string? MetaDataString(this IRedactor redactor, string? value) =>
            string.IsNullOrEmpty(value) ? value : redactor.MetaData(value)?.ToString();

        public static string? SystemDataString(this IRedactor redactor, string? value) =>
            string.IsNullOrEmpty(value) ? value : redactor.SystemData(value)?.ToString();

        /// <summary>
        /// Redacts an operation's key with the classification that key actually carries.
        /// </summary>
        /// <remarks>
        /// <see cref="OpCode.SelectBucket"/> is the only operation whose <see cref="IOperation.Key"/>
        /// is not a document key - it holds the bucket name. Bucket names are metadata, so tagging
        /// it as user data would have it stripped at <see cref="RedactionLevel.Partial"/>, losing a
        /// diagnostic that Couchbase treats as safe at that level. Every site that redacts an
        /// operation key routes through here so that the log line, the exception message and the
        /// error context cannot classify the same value differently.
        /// <para>
        /// Unlike the <c>*String</c> helpers this does not special-case an empty key: internal
        /// operations leave <see cref="IOperation.Key"/> at <see cref="string.Empty"/> and have
        /// always logged as an empty tag.
        /// </para>
        /// </remarks>
        public static Redacted<string> OperationKey(this TypedRedactor redactor, IOperation op) =>
            op.OpCode == OpCode.SelectBucket
                ? redactor.MetaData(op.Key)
                : redactor.UserData(op.Key);

        /// <summary>
        /// <see cref="OperationKey"/> for error contexts, which store plain strings.
        /// </summary>
        public static string? OperationKeyString(this TypedRedactor redactor, IOperation op) =>
            op.OpCode == OpCode.SelectBucket
                ? redactor.MetaDataString(op.Key)
                : redactor.UserDataString(op.Key);
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

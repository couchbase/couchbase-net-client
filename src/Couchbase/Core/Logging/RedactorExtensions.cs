using Couchbase.Core.IO.Operations;

#nullable enable

namespace Couchbase.Core.Logging
{
    /// <summary>
    /// Redaction helpers that need <see cref="Couchbase.Core.IO.Operations"/>. They live here as
    /// extensions rather than on <see cref="Redactor"/> itself so that
    /// <see cref="Couchbase.Core.Logging"/> takes no dependency on the operations namespace.
    /// </summary>
    internal static class RedactorExtensions
    {
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
        public static Redacted<string> OperationKey(this Redactor redactor, IOperation op) =>
            op.OpCode == OpCode.SelectBucket
                ? redactor.MetaData(op.Key)
                : redactor.UserData(op.Key);

        /// <summary>
        /// <see cref="OperationKey"/> for error contexts, which store plain strings.
        /// </summary>
        public static string? OperationKeyString(this Redactor redactor, IOperation op) =>
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

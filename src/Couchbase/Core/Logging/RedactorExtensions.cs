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
        /// True when <see cref="IOperation.Key"/> holds something other than a document key, and so
        /// is metadata rather than user data.
        /// </summary>
        /// <remarks>
        /// Derived by sweeping every assignment to <see cref="IOperation.Key"/> in the SDK, rather
        /// than by listing the ones a reviewer happened to notice - the previous form of this
        /// classified everything except <see cref="OpCode.SelectBucket"/> as a document key, and so
        /// tagged three other operations as user data.
        /// <list type="bullet">
        /// <item><description><see cref="OpCode.SelectBucket"/> - the bucket name.</description></item>
        /// <item><description><see cref="OpCode.Helo"/> - the connection id and client description,
        /// from <c>Hello.BuildHelloKey</c>. This one runs on every connection, so at
        /// <see cref="RedactionLevel.Partial"/> the old classification stripped the SDK identifier
        /// out of every negotiation log line.</description></item>
        /// <item><description><see cref="OpCode.GetCidByName"/> - the fully qualified
        /// <c>scope.collection</c> name, on the pre-7.0 fallback where
        /// <c>CouchbaseCollection.GetCidWithFallbackAsync</c> retries with the name in the key
        /// instead of the body.</description></item>
        /// <item><description><see cref="OpCode.GetSidByName"/> - the scope name. Nothing
        /// constructs a <c>GetSid</c> today; it is classified so that the first caller to do so
        /// does not have to remember.</description></item>
        /// <item><description><see cref="OpCode.SaslList"/>, <see cref="OpCode.SaslStart"/> and
        /// <see cref="OpCode.SaslStep"/> - the mechanism name. These authenticate over their own
        /// send path rather than <c>ClusterNode.ExecuteOp</c>, so they do not reach this helper
        /// today; they are here for the same reason as <see cref="OpCode.GetSidByName"/>.</description></item>
        /// </list>
        /// </remarks>
        private static bool KeyIsMetaData(OpCode opCode) => opCode switch
        {
            OpCode.SelectBucket => true,
            OpCode.Helo => true,
            OpCode.GetCidByName => true,
            OpCode.GetSidByName => true,
            OpCode.SaslList => true,
            OpCode.SaslStart => true,
            OpCode.SaslStep => true,
            _ => false
        };

        /// <summary>
        /// Redacts an operation's key with the classification that key actually carries.
        /// </summary>
        /// <remarks>
        /// Bucket names, collection names and connection ids are metadata, so tagging them as user
        /// data would have them stripped at <see cref="RedactionLevel.Partial"/>, losing a
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
            KeyIsMetaData(op.OpCode)
                ? redactor.MetaData(op.Key)
                : redactor.UserData(op.Key);

        /// <summary>
        /// <see cref="OperationKey"/> for error contexts, which store plain strings.
        /// </summary>
        public static string? OperationKeyString(this Redactor redactor, IOperation op) =>
            KeyIsMetaData(op.OpCode)
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

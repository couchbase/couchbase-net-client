using System;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Client.Transactions
{
    internal static class ProtocolVersion
    {
        public static readonly decimal SupportedVersion = 2.1m;

        public static IEnumerable<ExtensionName> ExtensionsSupported()
        {
            // these will be the stringified version of the enum generated off of the GRPC proto files.
            // For example, EXT_DEFERRED_COMMIT becomes ExtDeferredCommit
            yield return new ExtensionName("ExtTransactionId", "EXT_TRANSACTION_ID", "TI");
            yield return new ExtensionName("ExtRemoveCompleted", "EXT_REMOVE_COMPLETED", "RC");
            yield return new ExtensionName("ExtQuery", "EXT_QUERY", "QU");
            yield return new ExtensionName("ExtSingleQuery", "EXT_SINGLE_QUERY", "SQ");
            yield return new ExtensionName("ExtBinaryMetadata", "EXT_BINARY_METADATA", "BM");
            yield return new ExtensionName("ExtCustomMetadataCollection", "EXT_CUSTOM_METADATA_COLLECTION", "CM");
            yield return new ExtensionName("BfCbd3787", "BF_CBD_3787", "BF3787");
            yield return new ExtensionName("BfCbd3794", "BF_CBD_3794", "BF3794");
            yield return new ExtensionName("ExtAllKvCombinations", "EXT_ALL_KV_COMBINATIONS", "CO");
            yield return new ExtensionName("BfCbd3791", "BF_CBD_3791", "BF3791");
            yield return new ExtensionName("ExtStoreDurability", "EXT_STORE_DURABILITY", "SD");
            yield return new ExtensionName("BfCbd3705", "BF_CBD_3705", "BF3705");
            yield return new ExtensionName("BfCbd3838", "BF_CBD_33838", "BF3838");
            yield return new ExtensionName("ExtUnknownAtrStates", "EXT_UNKNOWN_ATR_STATES", "UA");
            yield return new ExtensionName("ExtSdkIntegration", "EXT_SDK_INTEGRATION", "SI");
            yield return new ExtensionName("ExtSerialization", "EXT_SERIALIZATION", "SZ");
            yield return new ExtensionName("ExtQueryContext", "EXT_QUERY_CONTEXT", "QC");
            yield return new ExtensionName("ExtThreadSafe", "EXT_THREAD_SAFE", "TS");
            yield return new ExtensionName("ExtInsertExisting", "EXT_INSERT_EXISTING", "IX");
            yield return new ExtensionName("ExtReplaceBodyWithXattr", "EXT_REPLACE_BODY_WITH_XATTR","RX");
            yield return new ExtensionName("ExtParallelUnstaging", "EXT_PARALLEL_UNSTAGING", "PU");
            yield return new ExtensionName("ExtBinarySupport", "EXT_BINARY_SUPPORT", "BS");
            yield return new ExtensionName("ExtMemoryOptUnstaging", "EXT_MEMORY_OPT_UNSTAGING", "MO");
        }

        internal static bool Supported(string shortCode) => SupportedShortCodes.Value.Contains(shortCode);

        private static Lazy<HashSet<string>> SupportedShortCodes => new Lazy<HashSet<string>>(() => new HashSet<string>(ExtensionsSupported().Select(ext => ext.ShortCode).ToList()));

        internal record ExtensionName(string PascalCase, string ConstantStyle, string ShortCode);
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

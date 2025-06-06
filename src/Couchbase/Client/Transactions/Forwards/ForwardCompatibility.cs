#nullable enable
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Couchbase.Client.Transactions.Error;
using Couchbase.Client.Transactions.Error.External;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Client.Transactions.Forwards
{
    internal class ForwardCompatibility
    {
        public const string WriteWriteConflictReadingAtr = "WW_R";
        public const string WriteWriteConflictReplacing = "WW_RP";
        public const string WriteWriteConflictRemoving = "WW_RM";
        public const string WriteWriteConflictInserting = "WW_I";
        public const string WriteWriteConflictInsertingGet = "WW_IG";
        public const string Gets = "G";
        public const string GetsReadingAtr = "G_A";
        public const string CleanupEntry = "CL_E";

        public static readonly JArray extBinSupportActions = new(new JObject()
        {
            ["b"] = "f",
            ["e"] = "BS",
        });

        public static readonly JObject extBinSupport = new JObject()
        {
            [CleanupEntry] = extBinSupportActions,
            [Gets] = extBinSupportActions,
            [WriteWriteConflictInserting] = extBinSupportActions,
            [WriteWriteConflictInsertingGet] = extBinSupportActions,
        };


        public static async Task Check(AttemptContext? ctx, string interactionPoint, JObject? fc)
        {
            if (fc == null)
            {
                return;
            }

            try
            {
                foreach (var prop in fc.Children<JProperty>())
                {
                    if (interactionPoint != prop.Name)
                    {
                        continue;
                    }

                    var checks = prop.Value.ToObject<CompatibilityCheck[]>();
                    foreach (var check in checks ?? Enumerable.Empty<CompatibilityCheck>())
                    {
                        string? failureMessage = null;
                        if (check.ProtocolVersion != null)
                        {
                            if (ProtocolVersion.SupportedVersion < check.ProtocolVersion.Value)
                            {
                                failureMessage = $"SupportedVersion {ProtocolVersion.SupportedVersion} is less than required {check.ProtocolVersion} @{interactionPoint}";
                            }
                        }
                        else if (check.ExtensionCheck != null)
                        {
                            if (!ProtocolVersion.Supported(check.ExtensionCheck))
                            {
                                failureMessage = $"Extension '{check.ExtensionCheck}' is not supported @{interactionPoint}.";
                            }
                        }

                        if (failureMessage != null)
                        {
                            if (check.Behavior == CompatibilityCheck.CheckBehaviorRetry)
                            {
                                if (check.RetryDelay != null)
                                {
                                    await Task.Delay(check.RetryDelay.Value).CAF();
                                }

                                var fcf = new ForwardCompatibilityFailureRequiresRetryException(failureMessage);
                                throw ErrorBuilder.CreateError(ctx, ErrorClass.FailOther, fcf)
                                    .RetryTransaction()
                                    .Build();
                            }
                            else
                            {
                                var fcf = new ForwardCompatibilityFailureException(failureMessage);
                                throw ErrorBuilder.CreateError(ctx, ErrorClass.FailOther, fcf)
                                    .Build();
                            }
                        }
                    }
                }
            }
            catch (JsonSerializationException ex)
            {
                var fcf = new ForwardCompatibilityFailureException("Check failed", ex);
                throw ErrorBuilder.CreateError(ctx, ErrorClass.FailOther, fcf)
                    .Build();
            }
        }

    }

    internal class CompatibilityCheck
    {
        public const char CheckBehaviorRetry = 'r';

        [JsonProperty("p")]
        [JsonPropertyName("p")]
        public decimal? ProtocolVersion { get; set; } = null;

        [JsonProperty("b")]
        [JsonPropertyName("b")]
        public char? Behavior { get; set; } = null;

        [JsonProperty("e")]
        [JsonPropertyName("e")]
        public string? ExtensionCheck { get; set; } = null;

        [JsonProperty("ra")]
        [JsonPropertyName("ra")]
        public int? RetryDelay { get; set; } = null;
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

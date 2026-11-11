using System.Text.Json.Nodes;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Query.Conversational
{
    // A frozen copy of the model and credential, mapped to the natural_* request parameters.
    internal sealed class ModelParameters
    {
        private readonly ModelCredential.CredentialKind? _credentialKind;
        private readonly string? _provider;
        private readonly string? _name;

        private ModelParameters(ModelCredential.CredentialKind? credentialKind, string? provider, string? name,
            string? cred = null, string? orgId = null, string? vendor = null, string? model = null,
            JsonObject? config = null)
        {
            _credentialKind = credentialKind;
            _provider = provider;
            _name = name;
            Cred = cred;
            OrgId = orgId;
            Vendor = vendor;
            Model = model;
            Config = config;
        }

        public string? Cred { get; }

        public string? OrgId { get; }

        public string? Vendor { get; }

        public string? Model { get; }

        public JsonObject? Config { get; }

        public static ModelParameters Create(ModelOptions? model, ModelCredential? credential)
        {
            var provider = model?.ProviderValue;
            var name = model?.NameValue;

            if (credential?.Kind == ModelCredential.CredentialKind.Capella)
            {
                if (model is not null && (model.EndpointValue is not null || model.OutputTokenLimitValue is not null ||
                                          model.ModerationValue is not null || model.RawValues.Count > 0))
                {
                    ThrowHelper.ThrowInvalidArgumentException(
                        "A Capella model credential supports only the model provider and name. Endpoint, OutputTokenLimit, Moderation and Raw are not supported.");
                }

                return new ModelParameters(credential.Kind, provider, name,
                    cred: credential.Secret, orgId: credential.OrganizationId, vendor: provider, model: name);
            }

            var config = new JsonObject();
            if (model is not null)
            {
                foreach (var pair in model.RawValues)
                {
                    config[pair.Key] = pair.Value?.DeepClone();
                }

                // Typed options overwrite raw entries with the same key, unset ones leave them in place.
                SetIfNotNull(config, "provider", provider);
                SetIfNotNull(config, "model", name);
                SetIfNotNull(config, "endpoint", model.EndpointValue);
                SetIfNotNull(config, "output_token_limit", model.OutputTokenLimitValue);
                SetIfNotNull(config, "moderation", model.ModerationValue);
            }

            switch (credential?.Kind)
            {
                case ModelCredential.CredentialKind.CredentialStore:
                    config["cred_id"] = credential.Secret;
                    break;
                case ModelCredential.CredentialKind.ApiKey:
                    config["api_key"] = credential.Secret;
                    break;
            }

            return new ModelParameters(credential?.Kind, provider, name, config: config.Count == 0 ? null : config);
        }

        public override string ToString() =>
            $"ModelParameters {{ Credential = {_credentialKind?.ToString() ?? "None"}, Provider = {_provider}, Name = {_name} }}";

        private static void SetIfNotNull(JsonObject config, string key, JsonNode? value)
        {
            if (value is not null)
            {
                config[key] = value;
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

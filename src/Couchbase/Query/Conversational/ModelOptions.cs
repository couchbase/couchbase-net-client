using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Couchbase.Core.Compatibility;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Query.Conversational;

/// <summary>
/// The model that Query calls to turn a prompt into a statement.
/// </summary>
[InterfaceStability(Level.Volatile)]
public sealed class ModelOptions
{
    private readonly Dictionary<string, JsonNode?> _raw = new(StringComparer.Ordinal);

    internal string? ProviderValue { get; private set; }

    internal string? NameValue { get; private set; }

    internal string? EndpointValue { get; private set; }

    internal int? OutputTokenLimitValue { get; private set; }

    internal bool? ModerationValue { get; private set; }

    internal IReadOnlyDictionary<string, JsonNode?> RawValues => _raw;

    /// <summary>
    /// Sets the model provider, for example "openai". An empty value leaves it unset.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <returns>A <see cref="ModelOptions"/> instance for chaining.</returns>
    public ModelOptions Provider(string provider)
    {
        ProviderValue = NullIfEmpty(provider);
        return this;
    }

    /// <summary>
    /// Sets the model name. An empty value leaves it unset.
    /// </summary>
    /// <param name="name">The model name.</param>
    /// <returns>A <see cref="ModelOptions"/> instance for chaining.</returns>
    public ModelOptions Name(string name)
    {
        NameValue = NullIfEmpty(name);
        return this;
    }

    /// <summary>
    /// Sets the URL of the model endpoint. An empty value leaves it unset.
    /// </summary>
    /// <param name="endpoint">The endpoint URL.</param>
    /// <returns>A <see cref="ModelOptions"/> instance for chaining.</returns>
    public ModelOptions Endpoint(string endpoint)
    {
        EndpointValue = NullIfEmpty(endpoint);
        return this;
    }

    /// <summary>
    /// Sets the maximum number of tokens the model can generate. Zero leaves it unset.
    /// </summary>
    /// <param name="limit">The token limit. It cannot be negative.</param>
    /// <returns>A <see cref="ModelOptions"/> instance for chaining.</returns>
    public ModelOptions OutputTokenLimit(int limit)
    {
        if (limit < 0)
        {
            ThrowHelper.ThrowInvalidArgumentException("The output token limit cannot be negative.");
        }

        OutputTokenLimitValue = limit == 0 ? null : limit;
        return this;
    }

    /// <summary>
    /// Turns content moderation of the model on or off.
    /// </summary>
    /// <param name="enabled">True to turn moderation on.</param>
    /// <returns>A <see cref="ModelOptions"/> instance for chaining.</returns>
    public ModelOptions Moderation(bool enabled)
    {
        ModerationValue = enabled;
        return this;
    }

    /// <summary>
    /// Adds a provider setting that has no typed option. A typed option with the same key takes precedence.
    /// Credentials cannot be set here, use <see cref="ModelCredential"/>.
    /// </summary>
    /// <param name="key">The setting name.</param>
    /// <param name="value">The setting value.</param>
    /// <returns>A <see cref="ModelOptions"/> instance for chaining.</returns>
    public ModelOptions Raw(string key, JsonNode? value)
    {
        if (string.IsNullOrEmpty(key))
        {
            ThrowHelper.ThrowInvalidArgumentException("The raw key cannot be null or empty.");
        }

        if (string.Equals(key, "cred_id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "api_key", StringComparison.OrdinalIgnoreCase))
        {
            ThrowHelper.ThrowInvalidArgumentException($"Set {key} through ModelCredential, not as a raw setting.");
        }

        _raw[key] = value;
        return this;
    }

    /// <summary>
    /// Returns the provider and the model name. It never contains raw values.
    /// </summary>
    public override string ToString() => $"ModelOptions {{ Provider = {ProviderValue}, Name = {NameValue} }}";

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
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

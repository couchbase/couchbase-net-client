using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Query.Conversational;

/// <summary>
/// Optional arguments for <see cref="IConversationalQuery.AttachChat"/>.
/// </summary>
[InterfaceStability(Level.Volatile)]
public sealed class AttachChatOptions
{
    internal ModelOptions? ModelValue { get; private set; }

    internal ModelCredential? CredentialValue { get; private set; }

    /// <summary>
    /// Sets the model Query calls. The chat keeps it and sends it with each prompt.
    /// </summary>
    /// <param name="model">The model options.</param>
    /// <returns>An <see cref="AttachChatOptions"/> instance for chaining.</returns>
    public AttachChatOptions Model(ModelOptions model)
    {
        ModelValue = model;
        return this;
    }

    /// <summary>
    /// Sets the credential Query uses to call the model. The chat keeps it and sends it with each prompt.
    /// </summary>
    /// <param name="credential">The model credential.</param>
    /// <returns>An <see cref="AttachChatOptions"/> instance for chaining.</returns>
    public AttachChatOptions Credential(ModelCredential credential)
    {
        CredentialValue = credential;
        return this;
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

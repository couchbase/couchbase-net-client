using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

#nullable enable

namespace Couchbase.Core
{
    /// <summary>
    /// Serialization settings for error contexts, whose field values may carry log-redaction tags.
    /// </summary>
    /// <remarks>
    /// System.Text.Json escapes '&lt;' and '&gt;' by default, to keep JSON safe to embed in HTML.
    /// That turns every redaction tag into an escape sequence, so cblogredaction - which finds the
    /// tags textually - cannot match them, and a redaction pass that appears to have run leaves the
    /// value in place. The tags are then semantically correct and operationally useless.
    /// <para>
    /// <see cref="Create"/> therefore uses the relaxed encoder, which escapes only what JSON
    /// requires. The source-generated resolver is carried over from the context it is created from,
    /// so serialization stays trim- and AOT-safe. Note this also stops non-ASCII being escaped,
    /// which was happening regardless of redaction.
    /// </para>
    /// <para>
    /// Only error contexts should use these settings. Anything that might be rendered into a page
    /// needs the default HTML-safe encoder.
    /// </para>
    /// </remarks>
    internal static class RedactionSafeJson
    {
        /// <summary>
        /// The settings of <paramref name="source"/>, but with the relaxed encoder.
        /// </summary>
        /// <remarks>
        /// Callers hold the result in a static field, created on demand rather than in a field
        /// initializer: a serializer context's <c>Default</c> is not yet constructed while that
        /// context is running its own static initialization.
        /// </remarks>
        public static JsonSerializerOptions Create(JsonSerializerOptions source) =>
            new(source)
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

        /// <summary>
        /// Binds <typeparamref name="T"/>'s type info from redaction-safe options, so that a
        /// context's <c>ToString()</c> emits literal tags rather than escape sequences.
        /// </summary>
        /// <remarks>
        /// Resolving type info is a dictionary lookup and <c>ToString()</c> can be called on any
        /// error path, so callers should cache the result in a static field rather than calling
        /// this per serialization.
        /// </remarks>
        public static JsonTypeInfo<T> TypeInfo<T>(JsonSerializerOptions redactionSafeOptions) =>
            (JsonTypeInfo<T>)redactionSafeOptions.GetTypeInfo(typeof(T));
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

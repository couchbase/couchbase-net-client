using System;
using System.Linq;
using System.Text.Encodings.Web;

#nullable enable

namespace Couchbase.Core
{
    /// <summary>
    /// Post-processing for error-context JSON, whose field values may carry log-redaction tags.
    /// </summary>
    /// <remarks>
    /// System.Text.Json escapes '&lt;' and '&gt;' by default, to keep JSON safe to embed in HTML.
    /// That turns every redaction tag into an escape sequence, so cblogredaction - which finds the
    /// tags textually - cannot match them, and a redaction pass that appears to have run leaves the
    /// value in place. The tags are then semantically correct and operationally useless.
    /// <para>
    /// The obvious fix is the relaxed encoder, and it is the wrong one. It unescapes every value in
    /// the document, not just the tags the SDK added: <see cref="Exceptions.ManagementErrorContext"/>
    /// carries the management endpoint's response body, which ns_server can return as HTML, and the
    /// query and search contexts carry server-authored error text. An application that embeds a
    /// rendered context in a diagnostics page would gain an XSS sink, and it would gain it whether
    /// or not redaction is enabled, since none of that content is redacted at any level.
    /// </para>
    /// <para>
    /// <see cref="RestoreTags"/> therefore serializes with the default HTML-safe encoder and then
    /// restores only the six tokens the redactor itself emits. Everything else - server text, HTML,
    /// non-ASCII - keeps the escaping it has always had, so this changes nothing for a caller who
    /// has not enabled redaction.
    /// </para>
    /// </remarks>
    internal static class RedactionSafeJson
    {
        // The tags the redactor emits, each paired with the form JavaScriptEncoder.Default
        // escapes it to. Derived from the encoder rather than hard-coded, so the pairing cannot
        // drift if System.Text.Json ever changes how it escapes an angle bracket.
        private static readonly (string Escaped, string Literal)[] Tags =
            new[] { "<ud>", "</ud>", "<md>", "</md>", "<sd>", "</sd>" }
                .Select(tag => (JavaScriptEncoder.Default.Encode(tag), tag))
                .ToArray();

        /// <summary>
        /// The escaped form of a bare '&lt;'. Its absence means the document holds no tag, which
        /// is every context when redaction is off - so the default configuration pays one scan.
        /// </summary>
        private static readonly string EscapedLessThan = JavaScriptEncoder.Default.Encode("<");

        /// <summary>
        /// Rewrites the escaped redaction tags in serialized error-context JSON back to literal
        /// angle brackets, leaving every other escape in place.
        /// </summary>
        /// <remarks>
        /// A value that itself contained the text "&lt;ud&gt;" would be unescaped too. That is
        /// exactly what the relaxed encoder would have emitted, so this is no worse - and a server
        /// echoing a redaction tag back at us has already lost the distinction.
        /// </remarks>
        public static string RestoreTags(string json)
        {
            if (json.IndexOf(EscapedLessThan, StringComparison.Ordinal) < 0)
            {
                return json;
            }

            foreach (var (escaped, literal) in Tags)
            {
                json = json.Replace(escaped, literal);
            }

            return json;
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

using System;
using System.Linq;
using System.Text;
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
    /// non-ASCII - keeps the escaping it has always had.
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
        /// Only a backslash that actually introduces an escape can begin a tag, which is why this
        /// walks the document rather than calling <see cref="string.Replace(string,string)"/>. A
        /// value holding the literal text <c>\u003Cud&gt;</c> serializes as
        /// <c>\\u003Cud\u003E</c>, where a textual replace matches from the second backslash
        /// and leaves <c>\&lt;ud&gt;</c> - not a valid JSON escape, so the context stops
        /// parsing. Backslashes pair off inside a JSON string, so an odd-length run is the
        /// only one that ends in an escape introducer.
        /// <para>
        /// What this cannot do is tell a tag the redactor added from the same six characters
        /// arriving in server text: a management or query response containing a literal "&lt;ud&gt;"
        /// has it unescaped here too, at every level including
        /// <see cref="Couchbase.Core.Logging.RedactionLevel.None"/>. Distinguishing them needs a
        /// marker the redactor emits only for this path, and the tags are also rendered straight
        /// into log messages, so there is no single representation to key on. A server echoing a
        /// redaction tag back at us has already lost the distinction.
        /// </para>
        /// </remarks>
        public static string RestoreTags(string json)
        {
            if (json.IndexOf(EscapedLessThan, StringComparison.Ordinal) < 0)
            {
                return json;
            }

            StringBuilder? restored = null;
            var copied = 0;
            var i = 0;

            while (i < json.Length)
            {
                if (json[i] != '\\')
                {
                    i++;
                    continue;
                }

                //Consume the run whole: each pair of backslashes is one literal backslash in the
                //value, so only an odd-length run leaves a trailing escape introducer.
                var runStart = i;
                do
                {
                    i++;
                } while (i < json.Length && json[i] == '\\');

                if (((i - runStart) & 1) == 0)
                {
                    continue;
                }

                var escapeStart = i - 1;
                foreach (var (escaped, literal) in Tags)
                {
                    if (escapeStart + escaped.Length > json.Length ||
                        string.CompareOrdinal(json, escapeStart, escaped, 0, escaped.Length) != 0)
                    {
                        continue;
                    }

                    restored ??= new StringBuilder(json.Length);
                    restored.Append(json, copied, escapeStart - copied);
                    restored.Append(literal);
                    i = copied = escapeStart + escaped.Length;
                    break;
                }
            }

            if (restored is null)
            {
                return json;
            }

            restored.Append(json, copied, json.Length - copied);
            return restored.ToString();
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

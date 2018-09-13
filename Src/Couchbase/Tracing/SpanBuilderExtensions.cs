using Couchbase.Utils;
using OpenTracing;
using OpenTracing.Tag;

namespace Couchbase.Tracing
{
    internal static class SpanBuilderExtensions
    {
        internal static ISpanBuilder WithIgnoreTag(this ISpanBuilder builder)
        {
            return builder.WithTag(CouchbaseTags.Ignore, true);
        }

        internal static ISpanBuilder AddDefaultTags(this ISpanBuilder builder)
        {
            return builder
                .WithTag(Tags.Component, ClientIdentifier.GetClientDescription())
                .WithTag(Tags.DbType, CouchbaseTags.DbTypeCouchbase)
                .WithTag(Tags.SpanKind, Tags.SpanKindClient);
        }
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2018 Couchbase, Inc.
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

#endregion

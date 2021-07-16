#nullable enable
using System;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// A NOOP implementation of <see cref="IRequestSpan"/>. Calling any method will do nothing as it's a NOOP.
    /// </summary>
    public class NoopRequestSpan : IRequestSpan
    {
        public static IRequestSpan Instance = new NoopRequestSpan();

        /// <inheritdoc />
        public void Dispose()
        {
        }

        /// <inheritdoc />
        public IRequestSpan SetAttribute(string key, bool value)
        {
            return this;
        }

        /// <inheritdoc />
        public IRequestSpan SetAttribute(string key, string value)
        {
            return this;
        }

        /// <inheritdoc />
        public IRequestSpan SetAttribute(string key, uint value)
        {
            return this;
        }

        /// <inheritdoc />
        public IRequestSpan AddEvent(string name, DateTimeOffset? timestamp = null)
        {
            return this;
        }

        /// <inheritdoc />
        public void End()
        {
        }

        /// <inheritdoc />
        public IRequestSpan? Parent { get; set; }

        /// <inheritdoc />
        public IRequestSpan ChildSpan(string name)
        {
            return Instance;
        }

        /// <inheritdoc />
        public bool CanWrite { get; } = false;

        /// <inheritdoc />
        public string? Id { get; }

        /// <inheritdoc />
        public uint? Duration { get; }
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

using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Wrapper for a <see cref="ILookupInResult"/> which adds a known document type.
    /// </summary>
    internal sealed class LookupInResult<TDocument> : ILookupInResult<TDocument>
    {
        private readonly ILookupInResult _inner;

        public LookupInResult(ILookupInResult inner)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (inner == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(inner));
            }
            if (inner is not ITypeSerializerProvider)
            {
                ThrowHelper.ThrowNotSupportedException("The class implementing ILookupInResult must also implement ITypeSerializerProvider.");
            }

            _inner = inner;
        }

        /// <inheritdoc />
        public ulong Cas => _inner.Cas;

        /// <inheritdoc />
        public bool Exists(int index) => _inner.Exists(index);

        /// <inheritdoc />
        public bool IsDeleted => _inner.IsDeleted;

        /// <inheritdoc />
        public T? ContentAs<T>(int index) => _inner.ContentAs<T>(index);

        /// <inheritdoc />
        public int IndexOf(string path) => _inner.IndexOf(path);

        public ITypeSerializer Serializer => ((ITypeSerializerProvider) _inner).Serializer;

        /// <inheritdoc />
        public void Dispose() => _inner.Dispose();
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

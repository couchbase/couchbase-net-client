using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue
{
    internal class LookupInResult : ILookupInReplicaResult, ITypeSerializerProvider
    {
        private readonly IList<LookupInSpec> _specs;

        /// <inheritdoc />
        public ITypeSerializer Serializer { get; }

        internal LookupInResult(IList<LookupInSpec> specs, ulong cas, TimeSpan? expiry, ITypeSerializer typeSerializer, bool isDeleted = false, bool? isReplica = false)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (specs == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(specs));
            }
            if (typeSerializer == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(typeSerializer));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            _specs = specs.OrderBy(spec => spec.OriginalIndex).ToList();
            Cas = cas;
            Expiry = expiry;
            Serializer = typeSerializer;
            IsDeleted = isDeleted;
            IsReplica = isReplica;
        }

        public ulong Cas { get; }

        public TimeSpan? Expiry { get; }

        public bool IsDeleted { get; }

        public bool? IsReplica { get; } = null;

        public T? ContentAs<T>(int index)
        {
            if (index < 0 || index >= _specs.Count)
            {
                throw new InvalidIndexException($"The index provided is out of range: {index}.");
            }

            var spec = _specs[index];

            switch (spec.Status)
            {
                case ResponseStatus.Success:
                    return Serializer.Deserialize<T>(spec.Bytes);
                case ResponseStatus.SubDocPathNotFound:
                    throw CreateSubDocException<PathNotFoundException>(spec, index);
                case ResponseStatus.SubDocPathMismatch:
                    throw CreateSubDocException<PathMismatchException>(spec, index);
                case ResponseStatus.SubDocPathInvalid:
                    throw CreateSubDocException<PathInvalidException>(spec, index);
                case ResponseStatus.SubDocPathTooBig:
                    throw CreateSubDocException<PathTooBigException>(spec, index);
                case ResponseStatus.SubDocDocTooDeep:
                    throw new DocumentTooDeepException();
                case ResponseStatus.SubDocValueTooDeep:
                    throw new ValueTooDeepException();
                case ResponseStatus.SubDocDocNotJson:
                    throw new DocumentNotJsonException();
                case ResponseStatus.SubdocXattrUnknownMacro:
                    throw new XattrUnknownMacroException();
                default:
                    throw CreateSubDocException<SubdocExceptionException>(spec, index);
            }
        }

        public bool Exists(int index)
        {
            if (index < 0 || index >= _specs.Count)
            {
                throw new InvalidIndexException($"The index provided is out of range: {index}.");
            }

            var spec = _specs[index];
            return spec.Status == ResponseStatus.Success;
        }

        /// <inheritdoc />
        public int IndexOf(string path)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }

            for (var i = 0; i < _specs.Count; i++)
            {
                if (_specs[i].Path == path)
                {
                    return i;
                }
            }

            return -1;
        }

        private T CreateSubDocException<T>(LookupInSpec spec, int index)
            where T : SubdocExceptionException, new() =>
            new()
            {
                SubDocumentErrorIndex = index,
                SubDocumentStatus = spec.Status
            };
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

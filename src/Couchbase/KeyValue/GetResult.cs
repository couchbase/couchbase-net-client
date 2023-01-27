using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Couchbase.KeyValue
{
    internal class GetResult : IGetResult
    {
        private SlicedMemoryOwner<byte> _contentBytes;
        private readonly IList<LookupInSpec> _specs;
        private readonly IReadOnlyCollection<string>? _projectList;
        private readonly ITypeTranscoder _transcoder;
        private readonly ITypeSerializer? _serializer;
        private readonly ILogger<GetResult> _logger;
        private bool _isParsed;
        private TimeSpan? _expiry;
        private DateTime? _expiryTime;

        internal GetResult(in SlicedMemoryOwner<byte> contentBytes, ITypeTranscoder transcoder, ILogger<GetResult> logger,
            List<LookupInSpec>? specs = null, IReadOnlyCollection<string>? projectList = null)
        {
            if (transcoder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(transcoder));
            }
            if (logger == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(logger));
            }

            _contentBytes = contentBytes;
            _transcoder = transcoder;
            _serializer = transcoder.Serializer;
            _logger = logger;
            _specs = specs ?? (IList<LookupInSpec>) Array.Empty<LookupInSpec>();
            _projectList = projectList;
        }

        ResponseStatus IGetResult.Status { get; set; }

        internal OperationHeader Header { get; set; }

        internal OpCode OpCode { get; set; }

        internal Flags Flags { get; set; }

        public string? Id { get; internal set; }

        public ulong Cas { get; internal set; }

        public TimeSpan? Expiry
        {
            get
            {
                ParseSpecs();
                if (_expiry.HasValue)
                {
                    return _expiry;
                }

                var spec = _specs.FirstOrDefault(x => x.Path == VirtualXttrs.DocExpiryTime);
                if (spec != null)
                {
                    // Always use our default serializer, it provides consistent behavior and is guaranteed to not be null
                    var ms = DefaultSerializer.Instance.Deserialize<long>(spec.Bytes);
                    _expiry = TimeSpan.FromMilliseconds(ms);
                }

                return _expiry;
            }
        }

        public DateTime? ExpiryTime
        {
            get
            {
                //If this is a GET et al then no sub doc specs to parse
                if (_specs.Count > 0)
                {
                    ParseSpecs();
                }

                if (_expiryTime.HasValue)
                {
                    return _expiryTime;
                }

                var spec = _specs.FirstOrDefault(x => x.Path == VirtualXttrs.DocExpiryTime);
                if (spec != null)
                {
                    // Always use our default serializer, it provides consistent behavior and is guaranteed to not be null
                    var secondsUntilExpiry = DefaultSerializer.Instance.Deserialize<long>(spec.Bytes);
                    if (secondsUntilExpiry == 0)
                    {
                        return DateTime.MaxValue;
                    }
#if NETSTANDARD2_1
                    _expiryTime = DateTime.UnixEpoch.AddSeconds(secondsUntilExpiry).ToLocalTime();
#else
                    var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    _expiryTime = unixEpoch.AddSeconds(secondsUntilExpiry).ToLocalTime();
#endif
                }

                return _expiryTime;
            }
        }

        internal uint Opaque { get; set; }

        public T? ContentAs<T>()
        {
            EnsureNotDisposed();

            //basic GET or other non-projection operation
            if (OpCode == OpCode.Get || OpCode == OpCode.ReplicaRead || OpCode == OpCode.GetL || OpCode == OpCode.GAT)
            {
                _logger.LogDebug("using the {transcoder} Transcoder.", _transcoder.GetType());
                return _transcoder.Decode<T>(_contentBytes.Memory, Flags, OpCode);
            }

            //oh mai, its a projection
            ParseSpecs();

            // normal GET
            if (_specs.Count == 1 && _projectList?.Count == 0)
            {
                var spec = _specs[0];
                return _transcoder.Decode<T>(spec.Bytes, Flags, OpCode.Get);
            }

            if (_specs.Count <= 2)
            {
                // A full document GET will either be the only spec, or it may be one of two specs (the other is DocExpiryTime)
                var getSpec = _specs[0].OpCode == OpCode.Get ? _specs[0]
                    : _specs.Count == 2 && _specs[1].OpCode == OpCode.Get ? _specs[1]
                    : null;

                if (getSpec != null)
                {
                    if (_projectList?.Count > 0)
                    {
                        //a full doc is returned if the projection count exceeds the server limit
                        //so we remove any non-requested fields from the content returned.

                        var fullDocBuilder = GetProjectionBuilder();
                        fullDocBuilder.AddChildren(_projectList, getSpec.Bytes);

                        //root projection for empty path
                        return fullDocBuilder.ToObject<T>();
                    }
                    else
                    {
                        // A full doc was requested
                        // We should use the transcoder for the full doc, it may not be JSON

                        _logger.LogDebug("using the {transcoder} Transcoder.", _transcoder.GetType());
                        return _transcoder.Decode<T>(getSpec.Bytes, Flags, OpCode);
                    }
                }
            }

            var builder = GetProjectionBuilder();
            foreach (var spec in _specs)
            {
                //skip the expiry if it was included; it must fetched from this.Expiry
                if (spec.Path == VirtualXttrs.DocExpiryTime)
                {
                    continue;
                }

                builder.AddPath(spec.Path, spec.Bytes);
            }

            if (typeof(T).IsPrimitive)
            {
                return builder.ToPrimitive<T>();
            }
            return builder.ToObject<T>();
        }

        private void ParseSpecs()
        {
            //we already parsed the response from the server but not each element or nothing to parse
            if(_isParsed || _specs.Count == 0) return;

            var response = _contentBytes.Memory;
            var commandIndex = 0;

            for (;;)
            {
                var bodyLength = ByteConverter.ToInt32(response.Span.Slice(2));
                var payLoad = response.Slice(6, bodyLength);

                var command = _specs[commandIndex++];
                command.Status = (ResponseStatus)ByteConverter.ToUInt16(response.Span);
                command.ValueIsJson = payLoad.Span.IsJson();
                command.Bytes = payLoad;

                response = response.Slice(6 + bodyLength);

                if (response.Length <= 0) break;
            }

            _isParsed = true;
        }

        private IProjectionBuilder GetProjectionBuilder()
        {
            if (_serializer == null)
            {
                ThrowHelper.ThrowInvalidOperationException("Transcoder must have a serializer for projections.");
            }

            // Fallback to default if the custom deserializer doesn't implement IProjectableTypeDeserializer
            return _serializer is IProjectableTypeDeserializer projectableTypeDeserializer
                ? projectableTypeDeserializer.CreateProjectionBuilder(_logger)
                : DefaultSerializer.Instance.CreateProjectionBuilder(_logger);
        }

#region Finalization and Dispose

        ~GetResult()
        {
            Dispose(false);
        }

        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            _disposed = true;
            _contentBytes.Dispose();
            _contentBytes = SlicedMemoryOwner<byte>.Empty;
        }

        protected void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

#endregion
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

using System;
using System.Buffers;
using System.Collections.Generic;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Utils;
using Newtonsoft.Json.Linq;

namespace Couchbase
{
    public class GetResult : IGetResult
    {
        private readonly IMemoryOwner<byte> _contentBytes;
        private readonly List<OperationSpec> _specs;
        private readonly ITypeTranscoder _transcoder;
        private readonly ITypeSerializer _serializer;
        private readonly IByteConverter _converter;
        private bool _isParsed;
        private TimeSpan? _expiry;

        internal GetResult(IMemoryOwner<byte> contentBytes, ITypeTranscoder transcoder, List<OperationSpec> specs = null)
        {
            _contentBytes = contentBytes;
            _transcoder = transcoder;
            _serializer = transcoder.Serializer;
            _converter = transcoder.Converter;
            _specs = specs;
        }

        internal OperationHeader Header { get; set; }
        internal OpCode OpCode { get; set; }
        internal Flags Flags { get; set; }

        public string Id { get; internal set; }
        public ulong Cas { get; internal set; }

        public TimeSpan? Expiration
        {
            get
            {
                ParseSpecs();
                if (_expiry.HasValue)
                {
                    return _expiry;
                }

                var spec = _specs.Find(x => x.Path == VirtualXttrs.DocExpiryTime);
                if (spec != null)
                {
                    var ms = _serializer.Deserialize<long>(spec.Bytes);
                    _expiry = TimeSpan.FromMilliseconds(ms);
                }

                return _expiry;
            }
        }

        public T ContentAs<T>()
        {
            EnsureNotDisposed();

            //basic GET operation
            if (OpCode == OpCode.Get)
            {
                return _transcoder.Decode<T>(_contentBytes.Memory.Slice(Header.BodyOffset), Flags, OpCode);
            }

            //oh mai, its a projection
            ParseSpecs();

            var root = new JObject();
            foreach (var spec in _specs)
            {
                var content = _serializer.Deserialize<JToken>(spec.Bytes);
                var projection = CreateProjection(spec.Path, content);

                try
                {
                    root.Add(projection.First); //hacky should be improved later
                }
                catch (Exception)
                {
                    //ignore for now - these are cases where a root attribute is already mapped
                    //for example "attributes" and "attributes.hair" will cause exceptions
                }
            }
            return root.ToObject<T>();
        }

        public T ContentAs<T>(ITypeSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public bool HasValue => _contentBytes.Memory.Length > 24;

        private void ParseSpecs()
        {
            //we already parsed the response from the server but not each element
            if(_isParsed) return;

            var response = _contentBytes.Memory.Slice(Header.BodyOffset);
            var commandIndex = 0;

            for (;;)
            {
                var bodyLength = _converter.ToInt32(response.Span.Slice(2));
                var payLoad = response.Slice(6, bodyLength);

                var command = _specs[commandIndex++];
                command.Status = (ResponseStatus)_converter.ToUInt16(response.Span);
                command.ValueIsJson = payLoad.Span.IsJson();
                command.Bytes = payLoad;

                response = response.Slice(6 + bodyLength);

                if (response.Length <= 0) break;
            }

            _isParsed = true;
        }

        void BuildPath(JToken token, string name, JToken content =  null)
        {
            foreach (var child in token.Children())
            {
                if (child is JValue)
                {
                    var value = child as JValue;
                    value.Replace(new JObject(new JProperty(name, content)));
                    break;
                }
                BuildPath(child, name, content);
            }
        }

        JObject CreateProjection(string path, JToken content)
        {
            var elements = path.Split('.');
            var projection = new JObject();
            if (elements.Length == 1)
            {
                projection.Add(new JProperty(elements[0], content));
            }
            else
            {
                for (var i = 0; i < elements.Length; i++)
                {
                    if (projection.Last != null)
                    {
                        if (i == elements.Length - 1)
                        {
                            BuildPath(projection, elements[i], content);
                            continue;
                        }

                        BuildPath(projection, elements[i]);
                        continue;
                    }

                    projection.Add(new JProperty(elements[i], null));
                }
            }

            return projection;
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
            _contentBytes?.Dispose();
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

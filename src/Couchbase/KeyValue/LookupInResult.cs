using System;
using System.Buffers;
using System.Collections.Generic;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

namespace Couchbase.KeyValue
{
    public class LookupInResult : ILookupInResult
    {
        private readonly IMemoryOwner<byte> _bytes;
        private IByteConverter _converter = new DefaultConverter();
        private ITypeSerializer _serializer = new DefaultSerializer();

        internal LookupInResult(IMemoryOwner<byte> bytes, ulong cas, TimeSpan? expiry)
        {
            _bytes = bytes;
            Cas = cas;
            Expiry = expiry;
        }

        public ulong Cas { get; }
        public TimeSpan? Expiry { get; }

        public T ContentAs<T>(int index)
        {
            EnsureNotDisposed();

            var response = _bytes.Memory.Slice(HeaderOffsets.HeaderLength);

            var operationSpecs = new List<OperationSpec>();
            for (;;)
            {
                var bodyLength = _converter.ToInt32(response.Span.Slice(2));
                var payLoad = response.Slice(6, bodyLength);

                var command = new OperationSpec
                {
                    Status = (ResponseStatus) _converter.ToUInt16(response.Span),
                    ValueIsJson = payLoad.Span.IsJson(),
                    Bytes = payLoad
                };
                operationSpecs.Add(command);

                response = response.Slice(6 + bodyLength);

                if (response.Length <= 0) break;
            }

            var spec = operationSpecs[index];
            return _serializer.Deserialize<T>(spec.Bytes);
        }

        public T ContentAs<T>(int index, ITypeSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public bool Exists(int index)
        {
            throw new NotImplementedException();
        }

        public ResponseStatus OpCode(int index)
        {
            throw new NotImplementedException();
        }

        #region Finalization and Dispose

        ~LookupInResult()
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
            _bytes?.Dispose();
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

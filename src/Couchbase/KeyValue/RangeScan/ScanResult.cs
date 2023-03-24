using Couchbase.Utils;
using System;
using System.Text;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;

#nullable enable
namespace Couchbase.KeyValue.RangeScan
{
    /// <inheritdoc />
    public class ScanResult : IScanResult
    {
        private readonly SlicedMemoryOwner<byte> _body;
        private readonly string _id;
        private readonly DateTime? _expiryTime;
        private readonly int _seqno;
        private readonly ulong _cas;
        private readonly bool _idOnly;
        private readonly ITypeTranscoder _transcoder;
        private readonly Flags? _flags;
        private readonly OpCode _opCode;


        internal ScanResult(SlicedMemoryOwner<byte> body, string id, bool idOnly, DateTime? expiryTime, int seqno, ulong cas, OpCode opCode, ITypeTranscoder transcoder, Flags? flags = default)
        {
            _body = body;
            _id = id;
            _idOnly = idOnly;
            _expiryTime = expiryTime;
            _seqno = seqno;
            _cas = cas;
            _flags = flags;
            _transcoder = transcoder;
            _opCode = opCode;
        }

        private void RequireBody() {
            if (_idOnly) {
                throw new UnsupportedException("This result came from a scan configured to return only document IDs.");
            }
        }

        public bool IsEmpty()
        {
            if (this.Id == String.Empty)
            {
                return true;
            }

            return false;
        }

        public bool IdOnly => _idOnly;

        /// <inheritdoc />
        public string Id => _id;

        internal ReadOnlyMemory<byte> Body => _body.Memory;

        /// <inheritdoc />
        public DateTime? ExpiryTime => _expiryTime;

        /// <inheritdoc />
        public ulong Cas => _cas;

        public byte[] ContentAsBytes()
        {
            RequireBody();
            return Body.Span.ToArray();
        }

        public string ContentAsString()
        {
            return Encoding.UTF8.GetString(Body.Span.ToArray());
        }

        /// <inheritdoc />
        public T? ContentAs<T>()
        {
            RequireBody();
            return _transcoder.Decode<T>(Body, _flags ?? default, _opCode);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("ScanResult{");
            sb.Append("id = ").Append(Id);
            sb.Append(", content = ").Append(ContentAsString());
            return sb.ToString();
        }
    }
}

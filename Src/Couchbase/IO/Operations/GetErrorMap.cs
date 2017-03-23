using System.Collections.Generic;
using Couchbase.Core.Transcoders;
using Couchbase.Logging;

namespace Couchbase.IO.Operations
{
    internal class GetErrorMap : OperationBase<ErrorMap>
    {
        private const int DefaultVersion = 1; // will be configurable at some point

        public ErrorMap ErrorMap { get; set; }

        public GetErrorMap(ErrorMap value, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(null, value, null, transcoder, opaque, timeout)
        {
            ErrorMap = value;
        }

        public GetErrorMap(ITypeTranscoder transcoder, uint timeout)
            : base(null, null, transcoder, timeout)
        { }

        public override byte[] CreateKey()
        {
            return new byte[0];
        }

        public override byte[] CreateExtras()
        {
            return new byte[0];
        }

        public override byte[] CreateBody()
        {
            var body = new byte[2];
            Converter.FromInt16(DefaultVersion, body, 0);
            return body;
        }

        public override void ReadExtras(byte[] buffer)
        {
            // no extras to read
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.GetErrorMap; }
        }

        public override bool RequiresKey
        {
            get { return false; }
        }
    }

    public class ErrorMap
    {
        private static readonly ILog Log = LogManager.GetLogger<ErrorMap>();

        public int version { get; set; }
        public int revision { get; set; }
        public Dictionary<string, ErrorCode> errors { get; set; }

        public bool TryGetGetErrorCode(short code, out ErrorCode errorCode)
        {
            if (errors.TryGetValue(code.ToString("X"), out errorCode))
            {
                return true;
            }

            Log.Warn("Unexpected ResponseStatus for KeyValue operation not found in Error Map: 0x{0}", code.ToString("X4"));
            return false;
        }
    }

    public class ErrorCode
    {
        public string name { get; set; }
        public string desc { get; set; }
        public IEnumerable<string> attrs { get; set; }

        public override string ToString()
        {
            return string.Format("KV Error: {{Name=\"{0}\", Description=\"{1}\", Attributes=\"{2}\"}}",
                name,
                desc ?? string.Empty,
                string.Join(",", attrs ?? new string[0])
            );
        }
    }
}

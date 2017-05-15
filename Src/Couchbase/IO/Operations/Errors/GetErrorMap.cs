using Couchbase.Core.Transcoders;

namespace Couchbase.IO.Operations.Errors
{
    internal class GetErrorMap : OperationBase<ErrorMap>
    {
        private const int DefaultVersion = 1; // will be configurable at some point

        public ErrorMap ErrorMap { get; set; }

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

        public override IOperation Clone()
        {
            return new GetErrorMap(Transcoder, Timeout)
            {
                ErrorCode = ErrorCode
            };
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
}
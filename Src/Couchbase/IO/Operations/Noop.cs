using Couchbase.IO.Converters;

namespace Couchbase.IO.Operations
{
    internal class Noop : OperationBase<object>
    {
        public Noop(IByteConverter converter, uint timeout) : base(converter, timeout)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.NoOp; }
        }

        public override byte[] Write()
        {
            return CreateHeader(new byte[0], new byte[0], null);
        }
    }
}

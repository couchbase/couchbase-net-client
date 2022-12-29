namespace Couchbase.Core.IO.Operations
{
    internal static class ResponseStatusExtensions
    {
        internal static bool SuccessOrContinue(this ResponseStatus status) =>
            status == ResponseStatus.Success ||
            status == ResponseStatus.RangeScanMore ||
            status == ResponseStatus.RangeScanComplete;

        internal static bool Failure(this ResponseStatus status, OpCode opcode)
        {
            switch (status)
            {
                case ResponseStatus.RangeScanComplete:
                case ResponseStatus.RangeScanMore:
                case ResponseStatus.Success: return false;
                case ResponseStatus.SubDocMultiPathFailure:
                    return opcode != OpCode.MultiLookup;
                default:
                    return true;
            }
        }
    }
}

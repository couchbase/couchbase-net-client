namespace Couchbase.Core.IO.Operations
{
    internal static class ResponseStatusExtensions
    {
        internal static bool SuccessOrContinue(this ResponseStatus status) =>
            status == ResponseStatus.Success ||
            status == ResponseStatus.RangeScanMore ||
            status == ResponseStatus.RangeScanComplete;

        internal static bool Failure(this ResponseStatus status)
        {
            switch (status)
            {
                case ResponseStatus.RangeScanComplete:
                case ResponseStatus.RangeScanMore:
                case ResponseStatus.Success: return false;
                default:
                    return true;
            }
        }
    }
}


namespace Couchbase.IO.Operations
{
    interface IOperationRequest
    {
        OperationCode OperationCode { get; }

        int CorrelationId { get; }

        string Key { get; }

        ulong Cas { get; }

        ushort Reserved { get; }
    }
}

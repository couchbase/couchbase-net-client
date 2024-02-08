#if NET5_0_OR_GREATER
#nullable enable
namespace Couchbase.Integrated.Transactions.DataModel
{
    internal record QueryBeginWorkResponse(string? txid);
}
#endif

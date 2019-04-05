namespace Couchbase.Core.IO.Operations
{
    internal enum Magic : byte
    {
        Request = 0x80,
        Response = 0x81,
        AltResponse = 0x18,
        AltRequest = 0x08
    }
}

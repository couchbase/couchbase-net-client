namespace Couchbase.KeyValue;

/// <summary>
/// An interface indicating the operation has a VBucketId mapped before being sent, and may have an empty Key.
/// </summary>
/// <remarks>Currently only RangeScan operations.</remarks>
internal interface IPreMappedVBucketOperation
{
    short? VBucketId { get; }
}

using Couchbase.Core.IO.Transcoders;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Applied to key/value options which may override the default <see cref="ITypeTranscoder"/>.
    /// </summary>
    internal interface ITranscoderOverrideOptions : IKeyValueOptions
    {
        ITypeTranscoder? Transcoder { get; }
    }
}

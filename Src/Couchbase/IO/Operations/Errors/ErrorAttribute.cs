namespace Couchbase.IO.Operations.Errors
{
    public enum ErrorAttribute
    {
        /// <summary>
        /// This attribute means that the error is related to a constraint failure regarding the item itself,
        /// i.e. the item does not exist, already exists, or its current value makes the current operation impossible.
        /// Retrying the operation when the item's value or status has changed may succeed.
        /// </summary>
        [EnumDescription("item-only")]
        ItemOnly,

        /// <summary>
        /// This attribute means that a user's input was invalid because it violates the semantics of the operation,
        /// or exceeds some predefined limit.
        /// </summary>
        [EnumDescription("invalid-input")]
        InvalidInput,

        /// <summary>
        /// The client's cluster map may be outdated and requires updating. The client should obtain a newer
        /// configuration.
        /// </summary>
        [EnumDescription("fetch-config")]
        FetchConfig,

        /// <summary>
        /// The current connection is no longer valid. The client must reconnect to the server. Note that the presence
        /// of other attributes may indicate an alternate remedy to fixing the connection without a disconnect, but
        /// without special remedial action a disconnect is needed.
        /// </summary>
        [EnumDescription("conn-state-invalidated")]
        ConnStateInvalid,

        /// <summary>
        /// The operation failed because the client failed to authenticate or is not authorized to perform this operation.
        /// Note that this error in itself does not mean the connection is invalid, unless conn-state-invalidated is also present.
        /// </summary>
        [EnumDescription("auth")]
        Auth,

        /// <summary>
        /// This error code must be handled specially. If it is not handled, the connection must be dropped.
        /// </summary>
        [EnumDescription("special-handling")]
        SpecialHandling,

        /// <summary>
        /// The operation is not supported, possibly because the of server version, bucket type, or current user.
        /// </summary>
        [EnumDescription("support")]
        Support,

        /// <summary>
        /// This error is transient. Note that this does not mean the error is retriable.
        /// </summary>
        [EnumDescription("temp")]
        Temp,

        /// <summary>
        /// This is an internal error in the server.
        /// </summary>
        [EnumDescription("internal")]
        Internal,

        /// <summary>
        /// The operation may be retried immediately.
        /// </summary>
        [EnumDescription("retry-now")]
        RetryNow,

        /// <summary>
        /// The operation may be retried after some time.
        /// </summary>
        [EnumDescription("retry-later")]
        RetryLater,

        /// <summary>
        /// The error is related to the subdocument subsystem.
        /// </summary>
        [EnumDescription("subdoc")]
        SubDoc,

        /// <summary>
        /// The error is related to the DCP subsystem.
        /// </summary>
        [EnumDescription("dcp")]
        // ReSharper disable once InconsistentNaming
        DCP
    }
}
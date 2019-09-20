namespace Couchbase.Services.KeyValue
{
    /// <summary>
    /// Perform a macro expansion for the SeqNo value on the server.
    /// </summary>
    internal class MutationMacroSeqNo : IMutationMacro
    {
        public override string ToString()
        {
            return "${Mutation.seqno}";
        }
    }
}

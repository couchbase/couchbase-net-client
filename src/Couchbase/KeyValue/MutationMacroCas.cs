namespace Couchbase.KeyValue
{
    /// <summary>
    /// Perform a macro expansion for the CAS value on the server.
    /// </summary>
    internal class MutationMacroCas : IMutationMacro
    {
        public override string ToString()
        {
            return "${Mutation.CAS}";
        }
    }
}

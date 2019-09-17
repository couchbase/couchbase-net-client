namespace Couchbase
{
    /// <summary>
    /// Perform a macro expansion for the Value_Crc32c value on the server.
    /// </summary>
    internal class MutationMacroValueCrc32C : IMutationMacro
    {
        public override string ToString()
        {
            return "${Mutation.value_crc32c}";
        }
    }
}

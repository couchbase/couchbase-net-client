namespace Couchbase.Core.IO.Operations
{
    internal static class BufferExtensions
    {
        /// <summary>
        /// Converts a <see cref="byte"/> to an <see cref="OpCode"/>
        /// </summary>
        /// <param name="value"></param> enumeration value.
        /// <returns>A <see cref="OpCode"/> enumeration value.</returns>
        /// <remarks><see cref="OpCode"/> are the available operations supported by Couchbase.</remarks>
        public static OpCode ToOpCode(this byte value)
        {
            return (OpCode)value;
        }
    }
}

namespace Couchbase.Core.IO.Operations.Legacy
{
    public static class BufferExtensions
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

        /// <summary>
        /// Gets the length of a buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns>0 if the buffer is null, otherwise the length of the buffer.</returns>
        public static int GetLengthSafe(this byte[] buffer)
        {
            var length = 0;
            if (buffer != null)
            {
                length = buffer.Length;
            }
            return length;
        }
    }
}

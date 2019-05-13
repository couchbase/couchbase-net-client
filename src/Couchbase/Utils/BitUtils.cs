using System;
using System.Runtime.CompilerServices;

namespace Couchbase.Utils
{
    /// <summary>
    /// Utilities for working with individual bits.
    /// </summary>
    internal static class BitUtils
    {
        /// <summary>
        /// Sets the bit of a <see cref="byte"/> at a given position.
        /// </summary>
        /// <param name="theByte">The byte.</param>
        /// <param name="position">The position.</param>
        /// <param name="value">if set to <c>true</c> [value].</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(ref byte theByte, int position, bool value)
        {
            if (value)
            {
                theByte = (byte)(theByte | (1 << position));
            }
            else
            {
                theByte = (byte)(theByte & ~(1 << position));
            }
        }

        /// <summary>
        /// Gets the bit as a <see cref="bool"/> from a <see cref="byte"/> at a given position.
        /// </summary>
        /// <param name="theByte">The byte.</param>
        /// <param name="position">The position.</param>
        /// <returns>True if the bit is set; otherwise false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetBit(byte theByte, int position)
        {
            return ((theByte & (1 << position)) != 0);
        }
    }
}

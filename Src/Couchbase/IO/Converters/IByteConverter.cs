using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.IO.Converters
{
    /// <summary>
    /// Provides an interface for converting types and arrays before being sent or after being received across the network.
    /// </summary>
    public interface IByteConverter
    {
        /// <summary>
        /// Reads a <see cref="bool"/> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">if set to <c>true</c> [use nbo].</param>
        /// <returns></returns>
        bool ToBoolean(byte[] buffer, int offset, bool useNbo);

        /// <summary>
        /// Reads a <see cref="float"/> from a buffer starting from a given offset..
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">if set to <c>true</c> [use nbo].</param>
        /// <returns></returns>
        float ToSingle(byte[] buffer, int offset, bool useNbo);

        /// <summary>
        /// Reads a <see cref="DateTime"/> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">if set to <c>true</c> [use nbo].</param>
        /// <returns></returns>
        DateTime ToDateTime(byte[] buffer, int offset, bool useNbo);

        /// <summary>
        /// To the double.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">if set to <c>true</c> [use nbo].</param>
        /// <returns></returns>
        double ToDouble(byte[] buffer, int offset, bool useNbo);

        /// <summary>
        /// Reads a <see cref="Byte"/> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        byte ToByte(byte[] buffer, int offset);

        /// <summary>
        ///  Reads a <see cref="Int16"/> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        short ToInt16(byte[] buffer, int offset);

        /// <summary>
        /// Reads a <see cref="UInt16"/> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        ushort ToUInt16(byte[] buffer, int offset);

        /// <summary>
        /// Reads a <see cref="UInt32"/> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        uint ToUInt32(byte[] buffer, int offset);

        /// <summary>
        /// Reads a <see cref="Int64"/> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        long ToInt64(byte[] buffer, int offset);

        /// <summary>
        /// Reads a <see cref="UInt64"/> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        ulong ToUInt64(byte[] buffer, int offset);

        /// <summary>
        /// Returns a <see cref="System.String" /> from the buffer starting at a given offset and length.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        string ToString(byte[] buffer, int offset, int length);

        /// <summary>
        /// Writes a <see cref="UInt16"/> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromUInt16(ushort value, ref byte[] buffer, int offset);

        /// <summary>
        /// Writes a <see cref="UInt16"/> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromUInt16(ushort value, byte[] buffer, int offset);

        /// <summary>
        /// Writes a <see cref="Int32"/> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromInt32(int value, ref byte[] buffer, int offset);

        /// <summary>
        /// Writes a <see cref="UInt32"/> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromUInt32(uint value, ref byte[] buffer, int offset);

        /// <summary>
        /// Writes a <see cref="Int32"/> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromInt32(int value, byte[] buffer, int offset);

        /// <summary>
        /// Writes a <see cref="UInt32"/> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromUInt32(uint value, byte[] buffer, int offset);

        /// <summary>
        /// Writes a <see cref="Int64"/> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromInt64(long value,  ref byte[] buffer, int offset);

        /// <summary>
        /// Writes a <see cref="UInt64"/> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromUInt64(ulong value, ref byte[] buffer, int offset);

        /// <summary>
        /// Writes a <see cref="Int64"/> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromInt64(long value, byte[] buffer, int offset);

        /// <summary>
        /// Writes a <see cref="UInt64"/> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromUInt64(ulong value, byte[] buffer, int offset);

        /// <summary>
        /// Writes a <see cref="System.String"/> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromString(string value, byte[] buffer, int offset);

        /// <summary>
        /// Writes a <see cref="System.String"/> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromString(string value, ref byte[] buffer, int offset);

        /// <summary>
        /// Writes a <see cref="byte"/> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromByte(byte value, ref byte[] buffer, int offset);

        /// <summary>
        /// Writes a <see cref="byte"/> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromByte(byte value, byte[] buffer, int offset);

        /// <summary>
        /// Sets the bit from a <see cref="byte"/> at a given position.
        /// </summary>
        /// <param name="theByte">The byte.</param>
        /// <param name="position">The position.</param>
        /// <param name="value">if set to <c>true</c> [value].</param>
        void SetBit(ref byte theByte, int position, bool value);

        /// <summary>
        /// Gets the bit as a <see cref="bool"/> from a <see cref="byte"/> at a given position.
        /// </summary>
        /// <param name="theByte">The byte.</param>
        /// <param name="position">The position.</param>
        /// <returns>True if the bit is set; otherwise false.</returns>
        bool GetBit(byte theByte, int position);

        /// <summary>
        /// Reads a <see cref="Int16" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        short ToInt16(byte[] buffer, int offset, bool useNbo);

        /// <summary>
        /// Reads a <see cref="UInt16" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        ushort ToUInt16(byte[] buffer, int offset, bool useNbo);

        /// <summary>
        /// Reads a <see cref="Int32" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        int ToInt32(byte[] buffer, int offset);

        /// <summary>
        /// Reads a <see cref="Int32" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        int ToInt32(byte[] buffer, int offset, bool useNbo);

        /// <summary>
        /// Reads a <see cref="UInt32" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        uint ToUInt32(byte[] buffer, int offset, bool useNbo);

        /// <summary>
        /// Reads a <see cref="Int64" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        long ToInt64(byte[] buffer, int offset, bool useNbo);

        /// <summary>
        /// Reads a <see cref="UInt64" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        ulong ToUInt64(byte[] buffer, int offset, bool useNbo);

        /// <summary>
        /// Writes a <see cref="Int16" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <param name="offset">The offset.</param>
        void FromInt16(short value, ref byte[] buffer, int offset, bool useNbo);

        /// <summary>
        /// Writes a <see cref="Int16" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromInt16(short value, ref byte[] buffer, int offset);

        /// <summary>
        /// Writes a <see cref="Int16" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        void FromInt16(short value, byte[] buffer, int offset);

        /// <summary>
        /// Writes a <see cref="UInt16" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <param name="offset">The offset.</param>
        void FromUInt16(ushort value, ref byte[] buffer, int offset, bool useNbo);

        /// <summary>
        /// Writes a <see cref="Int32" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        void FromInt32(int value, ref byte[] buffer, int offset, bool useNbo);

        /// <summary>
        /// Writes a <see cref="UInt32" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        void FromUInt32(uint value, ref byte[] buffer, int offset, bool useNbo);

        /// <summary>
        /// Writes a <see cref="Int64" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        void FromInt64(long value, ref byte[] buffer, int offset, bool useNbo);

        /// <summary>
        /// Writes a <see cref="UInt64" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        void FromUInt64(ulong value, ref byte[] buffer, int offset, bool useNbo);
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion

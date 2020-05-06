using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Couchbase.Core.IO
{
    internal static class SocketExtensions
    {
        /// <summary>
        /// Try to enable TCP keep-alives, the time and interval on a managed Socket.
        /// </summary>
        /// <param name="socket">The socket to enable keep-alives on.</param>
        /// <param name="on">if set to <c>true</c> keep-alives are enabled; false to disable.</param>
        /// <param name="time">The duration between two keepalive transmissions in idle condition.</param>
        /// <param name="interval">The duration between two successive keepalive retransmissions, if acknowledgement to the previous keepalive transmission is not received.</param>
        /// <param name="message">The error message in cases of failure.</param>
        /// <returns>A value indicating success or silent-failure (true) or explicit failure (false).</returns>
        /// <remarks>Credit: <see href="http://blogs.msdn.com/b/lcleeton/archive/2006/09/15/754932.aspx"/></remarks>
        internal static bool TrySetKeepAlives(this Socket socket, bool on, uint time, uint interval, out string message)
        {
            message = string.Empty;

            try
            {
                const uint temp = 0;
                var values = new byte[Marshal.SizeOf(temp) * 3];
                BitConverter.GetBytes((uint)(on ? 1 : 0)).CopyTo(values, 0);
                BitConverter.GetBytes(time).CopyTo(values, Marshal.SizeOf(temp));
                BitConverter.GetBytes(interval).CopyTo(values, Marshal.SizeOf(temp) * 2);
                socket.IOControl(IOControlCode.KeepAliveValues, values, null);
            }
            catch (NotSupportedException e)
            {
                message = $"SetKeepAlive failed (Not Supported): {e.Message}";
                return false;
            }

            return true;
        }
    }
}

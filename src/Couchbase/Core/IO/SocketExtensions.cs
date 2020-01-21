using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.IO
{
    internal static class SocketExtensions
    {
        private static readonly ILogger Logger = LogManager.CreateLogger(typeof(SocketExtensions));

        /// <summary>
        /// Enable TCP keep-alives, the time and interval on a managed Socket.
        /// </summary>
        /// <param name="socket">The socket to enable keep-alives on.</param>
        /// <param name="on">if set to <c>true</c> keep-alives are enabled; false to disable.</param>
        /// <param name="time">The duration between two keepalive transmissions in idle condition.</param>
        /// <param name="interval">The duration between two successive keepalive retransmissions, if acknowledgement to the previous keepalive transmission is not received.</param>
        /// <remarks>Credit: <see href="http://blogs.msdn.com/b/lcleeton/archive/2006/09/15/754932.aspx"/></remarks>
        public static void SetKeepAlives(this Socket socket, bool on, uint time, uint interval)
        {
            try
            {
                const uint temp = 0;
                var values = new byte[Marshal.SizeOf(temp)*3];
                BitConverter.GetBytes((uint) (on ? 1 : 0)).CopyTo(values, 0);
                BitConverter.GetBytes(time).CopyTo(values, Marshal.SizeOf(temp));
                BitConverter.GetBytes(interval).CopyTo(values, Marshal.SizeOf(temp)*2);
                socket.IOControl(IOControlCode.KeepAliveValues, values, null);
            }
            catch (PlatformNotSupportedException)
            {
                // Can't set on non-Windows platforms, ignore error
                Logger.LogDebug("Skipping Socket.IOControl for keep alives, not supported on this platform");
            }
        }
    }
}

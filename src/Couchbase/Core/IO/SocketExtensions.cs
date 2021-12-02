using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Couchbase.Core.IO
{
    internal static class SocketExtensions
    {
#if NET5_0_OR_GREATER
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
        private static void SetKeepAlives(Socket socket, bool on, uint time, uint interval)
        {
            var values = new byte[sizeof(uint) * 3];

            var valueSpan = MemoryMarshal.Cast<byte, uint>(values.AsSpan());
            valueSpan[0] = on ? 1u : 0u;
            valueSpan[1] = time;
            valueSpan[2] = interval;

            socket.IOControl(IOControlCode.KeepAliveValues, values, null);
        }

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

#if NET5_0_OR_GREATER
            if (OperatingSystem.IsWindows())
            {
                SetKeepAlives(socket, on, time, interval);

                return true;
            }
            else
            {
                message = $"SetKeepAlive failed (Not Supported)";
                return false;
            }
#else
            try
            {
                SetKeepAlives(socket, on, time, interval);
                return true;
            }
            catch (PlatformNotSupportedException e)
            {
                message = $"SetKeepAlive failed (Not Supported): {e.Message}";
                return false;
            }
#endif
        }

#if NETCOREAPP3_0_OR_GREATER
        internal static bool TryEnableKeepAlives(this Socket socket, bool enableKeepAlives, int keepAliveTimeInSeconds, int keepAliveIntervalInSeconds, out string message)
        {
            message = string.Empty;

            try
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, enableKeepAlives);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, keepAliveTimeInSeconds);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, keepAliveIntervalInSeconds);
            }
            catch (Exception e)
            {
                message = $"SetKeepAlive failed (Not Supported): {e.Message}";
                return false;
            }

            return true;
        }
#endif
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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

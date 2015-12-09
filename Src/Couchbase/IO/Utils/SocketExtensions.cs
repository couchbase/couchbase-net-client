// http://blogs.msdn.com/b/pfxteam/archive/2011/12/15/10248293.aspx

using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Couchbase.IO.Utils
{
    /// <summary>
    /// Extension methods for using <see cref="Socket"/> instances.
    /// </summary>
    public static class SocketExtensions
    {
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
            // TODO: PlatformNotSupportedException
            
// #if DNXCORE50
//            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.KeepAlive, true);
// #else            
//             const uint temp = 0;
//             var values = new byte[Marshal.SizeOf(temp)*3];
//             BitConverter.GetBytes((uint) (on ? 1 : 0)).CopyTo(values, 0);
//             BitConverter.GetBytes(time).CopyTo(values, Marshal.SizeOf(temp));
//             BitConverter.GetBytes(interval).CopyTo(values, Marshal.SizeOf(temp)*2);
//             socket.IOControl(IOControlCode.KeepAliveValues, values, null);
// #endif
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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
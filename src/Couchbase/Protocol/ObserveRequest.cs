using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached.Protocol.Binary;
using System.Threading;

namespace Couchbase.Protocol
{
	public class ObserveRequest
	{
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(ObserveRequest));

		public readonly int CorrelationId;

		public string Key;
		public ulong Cas;
		public int VBucket;

		public unsafe IList<ArraySegment<byte>> CreateBuffer()
		{
			return CreateBuffer(null);
		}

		public unsafe IList<ArraySegment<byte>> CreateBuffer(IList<ArraySegment<byte>> appendTo)
		{
			// key size
			byte[] keyData = BinaryConverter.EncodeKey(this.Key);
			int keyLength = keyData == null ? 0 : keyData.Length;

			if (keyLength > 0xffff) throw new InvalidOperationException("KeyTooLong");

			// total payload size
			int totalLength = keyLength + 4;

			//build the header
			byte[] header = new byte[24];

			fixed (byte* buffer = header)
			{
				buffer[0x00] = 0x80; // magic
				buffer[0x01] = (byte)CouchbaseOpCode.Observe;

				// key length always 0x00 for observe
				buffer[0x02] = 0x00;
				buffer[0x03] = 0x00;

				// extra length
				buffer[0x04] = 0x00;

				// 5 -- data type, 0 (RAW)
				// 6,7 -- reserved, always 0

				buffer[0x06] = 0x00;
				buffer[0x07] = 0x00;

				// body length
				buffer[0x08] = (byte)(totalLength >> 24);
				buffer[0x09] = (byte)(totalLength >> 16);
				buffer[0x0a] = (byte)(totalLength >> 8);
				buffer[0x0b] = (byte)(totalLength & 255);

				buffer[0x0c] = (byte)(this.CorrelationId >> 24);
				buffer[0x0d] = (byte)(this.CorrelationId >> 16);
				buffer[0x0e] = (byte)(this.CorrelationId >> 8);
				buffer[0x0f] = (byte)(this.CorrelationId & 255);

				ulong cas = this.Cas;
				// CAS
				if (cas > 0)
				{
					// skip this if no cas is specfied
					buffer[0x10] = (byte)(cas >> 56);
					buffer[0x11] = (byte)(cas >> 48);
					buffer[0x12] = (byte)(cas >> 40);
					buffer[0x13] = (byte)(cas >> 32);
					buffer[0x14] = (byte)(cas >> 24);
					buffer[0x15] = (byte)(cas >> 16);
					buffer[0x16] = (byte)(cas >> 8);
					buffer[0x17] = (byte)(cas & 255);
				}
			}

			var retval = appendTo ?? new List<ArraySegment<byte>>(4);

			retval.Add(new ArraySegment<byte>(header));

			var vBucketBytes = new byte[]
			{
				(byte)(VBucket >> 8), (byte)(VBucket & 255)
			};
			retval.Add(new ArraySegment<byte>(vBucketBytes));

			var keyLengthBytes = new byte[]
			{
				(byte)(keyLength >> 8), (byte)(keyLength & 255)
			};
			retval.Add(new ArraySegment<byte>(keyLengthBytes));

			retval.Add(new ArraySegment<byte>(keyData));

			return retval;
		}

		public ushort Reserved;
		public ArraySegment<byte> Extra;
		public ArraySegment<byte> Data;
	}
}

#region [ License information          ]
/* ************************************************************
 * 
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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
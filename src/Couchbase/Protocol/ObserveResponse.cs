using System;
using System.Text;
using System.Diagnostics;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Protocol.Binary;
using Couchbase.Protocol;

namespace Couchbase.Operations
{
	public class ObserveResponse
	{
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(ObserveResponse));

		private const byte MAGIC_VALUE = 0x81;
		private const int HeaderLength = 24;

		private const int HEADER_OPCODE = 1;
		private const int HEADER_KEY = 2; // 2-3
		private const int HEADER_EXTRA = 4;
		private const int HEADER_DATATYPE = 5;
		private const int HEADER_STATUS = 6; // 6-7
		private const int HEADER_BODY = 8; // 8-11
		private const int HEADER_OPAQUE = 12; // 12-15
		private const int HEADER_PERSISTENCE_STATS = 16; // 16-19
		private const int HEADER_REPLICATION_STATS = 20; // 20-23

		private const int KEY_0_START = 24;

		public byte OpCode;
		public int KeyLength;
		public byte DataType;
		public int StatusCode;
		public ulong Cas { get; set; }

		public int PersistenceStats { get; set; }
		public int ReplicationStats { get; set; }
		public string Key { get; set; }
		public ObserveKeyState KeyState { get; set; }

		public int CorrelationId;

		public ArraySegment<byte> Extra;
		public ArraySegment<byte> Data;

		private string responseMessage;

		public ObserveResponse()
		{
			this.StatusCode = -1;
		}

		public string GetStatusMessage()
		{
			return this.Data.Array == null
					? null
					: (this.responseMessage
						?? (this.responseMessage = Encoding.ASCII.GetString(this.Data.Array, this.Data.Offset, this.Data.Count)));
		}

		public unsafe bool Read(PooledSocket socket)
		{
			this.StatusCode = -1;

			if (!socket.IsAlive)
				return false;

			var header = new byte[HeaderLength];
			socket.Read(header, 0, header.Length);

			int dataLength, extraLength;

			DeserializeHeader(header, out dataLength, out extraLength);

			var keyHeader = new byte[4];
			socket.Read(keyHeader, 0, 4);
			var vbucket = BinaryConverter.DecodeUInt16(keyHeader, 0);
			var keylen = BinaryConverter.DecodeUInt16(keyHeader, 2);

			var keyData = new byte[keylen];
			socket.Read(keyData, 0, keylen);
			Key = BinaryConverter.DecodeKey(keyData);

			var keyStateData = new byte[1];
			socket.Read(keyStateData, 0, keyStateData.Length);
			KeyState = (ObserveKeyState)keyStateData[0];

			var casData = new byte[8];
			socket.Read(casData, 0, casData.Length);
			Cas = BinaryConverter.DecodeUInt64(casData, 0);

			return this.StatusCode == 0;
		}

		private unsafe void DeserializeHeader(byte[] header, out int dataLength, out int extraLength)
		{
			fixed (byte* buffer = header)
			{
				if (buffer[0] != MAGIC_VALUE)
					throw new InvalidOperationException("Expected magic value " + MAGIC_VALUE + ", received: " + buffer[0]);

				if (buffer[1] != (byte)CouchbaseOpCode.Observe)
					throw new InvalidOperationException("Expected Observe op code " + CouchbaseOpCode.Observe + ", received: " + buffer[1]);

				this.DataType = buffer[HEADER_DATATYPE];
				this.OpCode = buffer[HEADER_OPCODE];
				this.StatusCode = BinaryConverter.DecodeUInt16(buffer, HEADER_STATUS);

				this.KeyLength = BinaryConverter.DecodeUInt16(buffer, HEADER_KEY);
				this.CorrelationId = BinaryConverter.DecodeInt32(buffer, HEADER_OPAQUE);
				//this.CAS = BinaryConverter.DecodeUInt64(buffer, HEADER_CAS);

				dataLength = BinaryConverter.DecodeInt32(buffer, HEADER_BODY);
				extraLength = buffer[HEADER_EXTRA];

				this.PersistenceStats = BinaryConverter.DecodeInt32(buffer, HEADER_PERSISTENCE_STATS);
				this.ReplicationStats = BinaryConverter.DecodeInt32(buffer, HEADER_REPLICATION_STATS);

			}
		}
	}
}

#region [ License information          ]
/* ************************************************************
 * 
 *    Copyright (c) 2010 Attila Kiskó, enyim.com
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

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Permissions;
using System.Text;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using NUnit.Framework;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbasePooledSocketTests
	{
		private IResourcePool _pool;

		[SetUp]
		public void SetUp()
		{
			var port = int.Parse(ConfigurationManager.AppSettings["port"]);
			var address = ConfigurationManager.AppSettings["address"];

			IPAddress ipAddress;
			if (!IPAddress.TryParse(address, out ipAddress))
			{
				throw new ArgumentException("endpoint");
			}

			//Use defaults
			var endpoint = new IPEndPoint(ipAddress, port);
			var config = new SocketPoolConfiguration();
			var node = new CouchbaseNode(endpoint, config);
			_pool = new SocketPool(node, config);
		}

		[TearDown]
		public void TearDown()
		{
			_pool.Dispose();
		}

		IPooledSocket CreateSocket()
		{
			var info = typeof (SocketPool).GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Instance);
			return info.Invoke(_pool, new object[]{}) as IPooledSocket;
		}

		Socket GetSocket(CouchbasePooledSocket socket)
		{
			var info = typeof(CouchbasePooledSocket).GetField("_socket", BindingFlags.NonPublic | BindingFlags.Instance);
			return info.GetValue(socket) as Socket;
		}

		[Test]
		public void TestCreate()
		{
			using(var socket = CreateSocket())
			{
				Assert.IsNotNull(socket);
			}
		}

		[Test]
		public void Test_That_IsAlive_Is_True_After_Create()
		{
			using (var socket = CreateSocket())
			{
				Assert.IsTrue(socket.IsAlive);
			}
		}

		[Test]
		public void Test_That_IsConnected_Is_True_After_Create()
		{
			using (var socket = CreateSocket())
			{
				Assert.IsTrue(socket.IsConnected);
			}
		}

		[Test]
		public void Test_That_IsAlive_Is_False_After_Close()
		{
			using (var socket = CreateSocket())
			{
				socket.Close();
				Assert.IsFalse(socket.IsAlive);
			}
		}

		[Test]
		public void Test_That_IsConnected_Is_False_After_Close()
		{
			using (var socket = CreateSocket())
			{
				socket.Close();
				Assert.IsFalse(socket.IsConnected);
			}
		}

		[Test]
		[ExpectedException(typeof(ObjectDisposedException))]
		public void When_Disposed_ObjectDisposedException_Is_Thrown_When_Read_Is_Called()
		{
			using (var socket = CreateSocket())
			{
				socket.Close();
				socket.Read(new[] {new byte()}, 0, 1);
			}
		}

		[Test]
		[ExpectedException(typeof(ObjectDisposedException))]
		public void When_Disposed_ObjectDisposedException_Is_Thrown_When_ReadByte_Is_Called()
		{
			using (var socket = CreateSocket())
			{
				socket.Close();
				socket.ReadByte();
			}
		}

		[Test]
		[ExpectedException(typeof(ObjectDisposedException))]
		public void When_Disposed_ObjectDisposedException_Is_Thrown_When_Write_Is_Called()
		{
			using (var socket = CreateSocket())
			{
				socket.Close();
				socket.Write(new List<ArraySegment<byte>>());
			}
		}

		[Test]
		[ExpectedException(typeof(ObjectDisposedException))]
		public void When_Disposed_ObjectDisposedException_Is_Thrown_When_Write_Is_Called2()
		{
			using (var socket = CreateSocket())
			{
				socket.Close();
				socket.Write(new[] {new byte()}, 0, 1);
			}
		}

		[Test]
		[ExpectedException(typeof(NotImplementedException))]
		public void When_Disposed_NotImplementedException_Is_Thrown_When_Reset_Is_Called2()
		{
			using (var socket = CreateSocket())
			{
				socket.Reset();
			}
		}

		[Test]
		[ExpectedException(typeof(NotImplementedException))]
		public void When_Disposed_NotImplementedException_Is_Thrown_When_Release_Is_Called2()
		{
			using (var socket = CreateSocket())
			{
				socket.Release();
			}
		}

		[Test]
		public void When_Close_Called_IsAlive_And_IsConnected_Are_False()
		{
			using (var socket = CreateSocket())
			{
				socket.Close();
				Assert.IsFalse(socket.IsConnected);
				Assert.IsFalse(socket.IsAlive);
			}
		}

		[Test]
		public void When_Exception_Thrown_While_Calling_Read_IsAlive_And_IsConnected_Are_False()
		{
			using (var pooledSocket = CreateSocket())
			{
				var socket = GetSocket(pooledSocket as CouchbasePooledSocket);
				try
				{
					socket.Shutdown(SocketShutdown.Receive);
					pooledSocket.Read(new[] {new byte()}, 0, 1);
				}
				catch (Exception e)
				{
					Console.WriteLine(e.Message);
				}
				finally
				{
					Assert.IsFalse(pooledSocket.IsConnected);
					Assert.IsFalse(pooledSocket.IsAlive);
				}
			}
		}

		[Test]
		public void When_Exception_Thrown_While_Calling_ReadByte_IsAlive_And_IsConnected_Are_False()
		{
			using (var pooledSocket = CreateSocket())
			{
				var socket = GetSocket(pooledSocket as CouchbasePooledSocket);

				try
				{
					socket.Shutdown(SocketShutdown.Receive);
					pooledSocket.ReadByte();
				}
				catch (Exception e)
				{
					Console.WriteLine(e.Message);
				}
				finally
				{
					Assert.IsFalse(pooledSocket.IsConnected);
					Assert.IsFalse(pooledSocket.IsAlive);
				}
			}
		}

		[Test]
		public void When_Exception_Thrown_While_Calling_Write_IsAlive_And_IsConnected_Are_False()
		{
			using (var pooledSocket = CreateSocket())
			{
				var socket = GetSocket(pooledSocket as CouchbasePooledSocket);
				try
				{
					socket.Shutdown(SocketShutdown.Send);
					pooledSocket.Write(new[] {new byte()}, 0, 1);
				}
				catch (Exception e)
				{
					Console.WriteLine(e.Message);
				}
				finally
				{
					Assert.IsFalse(pooledSocket.IsConnected);
					Assert.IsFalse(pooledSocket.IsAlive);
				}
			}
		}

		[Test]
		public void When_Exception_Thrown_While_Calling_Write_IsAlive_And_IsConnected_Are_False2()
		{
			using (var pooledSocket = CreateSocket())
			{
				var socket = GetSocket(pooledSocket as CouchbasePooledSocket);
				try
				{
					socket.Shutdown(SocketShutdown.Send);
					pooledSocket.Write(new List<ArraySegment<byte>> {new ArraySegment<byte>(new[] {new byte()})});
				}
				catch (Exception e)
				{
					Console.WriteLine(e.Message);
				}
				finally
				{
					Assert.IsFalse(pooledSocket.IsConnected);
					Assert.IsFalse(pooledSocket.IsAlive);
				}
			}
		}
	}
}

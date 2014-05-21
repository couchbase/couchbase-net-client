using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace Couchbase.Tests.Authentication.SSL
{
    [TestFixture]
    public class SSLConnectionTests
    {
        [Test]
        public void TestSSLConnect()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect("192.168.56.102", (int)DefaultPorts.SslDirect);
            using (var ns = new NetworkStream(socket))
            {
                var buffer = Encoding.UTF8.GetBytes("hello world!");
                //ns.Write(buffer, 0, buffer.Length);

                using (var ssls = new SslStream(ns))
                {
                    ssls.AuthenticateAsClient("192.168.56.102");
                    Console.WriteLine("Is Encrypted: {0}", ssls.IsEncrypted);
                    //ssls.Write(buffer, 0, buffer.Length);
                   
                    var saea = new SocketAsyncEventArgs {AcceptSocket = socket};
                    saea.Completed += saea_Completed;
                    saea.SetBuffer(buffer, 0, buffer.Length);
                    socket.SendAsync(saea);
                }
            }
            
        }

        void saea_Completed(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.None:
                    break;
                case SocketAsyncOperation.Accept:
                    break;
                case SocketAsyncOperation.Connect:
                    break;
                case SocketAsyncOperation.Disconnect:
                    break;
                case SocketAsyncOperation.Receive:
                    Console.WriteLine("{0} => {1}", e.LastOperation, e.SocketError);
                    break;
                case SocketAsyncOperation.ReceiveFrom:
                    break;
                case SocketAsyncOperation.ReceiveMessageFrom:
                    break;
                case SocketAsyncOperation.Send:
                    Console.WriteLine("{0} => {1}", e.LastOperation, e.SocketError);
                    break;
                case SocketAsyncOperation.SendPackets:
                    break;
                case SocketAsyncOperation.SendTo:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        [Test]
        public void Test_ClientConnection_With_Ssl()
        {
            var config = new ClientConfiguration {EncryptTraffic = true, Servers = new List<Uri>
            {
                new Uri("http://192.168.56.102:8091/pools")
            }};
            config.Initialize();

            CouchbaseCluster.Initialize(config);
            var cluster = CouchbaseCluster.Get();
            using (var bucket = cluster.OpenBucket())
            {
                Assert.IsNotNull(bucket);
            }
        }
    }
}

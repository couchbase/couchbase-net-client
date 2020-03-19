using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Connections
{
    public class SslConnectionTests
    {
        internal void HandleOperationCompleted(IMemoryOwner<byte> data, ResponseStatus status)
        {
            //noop
        }

        [Fact]
        public async Task When_Packet_Exceeds_MaxDocSize_ThrowValueTooLargeException()
        {
            var conn = new SslConnection(new SslStream(new MemoryStream()), new IPEndPoint(0, 0), new IPEndPoint(0, 0),
                new Logger<SslConnection>(new LoggerFactory()),
                new Logger<MultiplexingConnection>(new LoggerFactory()));

            var json = JsonConvert.SerializeObject(new string[1024 * 6145]);
            var bytes = Encoding.UTF8.GetBytes(json);

            await Assert.ThrowsAsync<ValueToolargeException>(() =>
                conn.SendAsync(bytes, HandleOperationCompleted)).ConfigureAwait(false);
        }
    }
}

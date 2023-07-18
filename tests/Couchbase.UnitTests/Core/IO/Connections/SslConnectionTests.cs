using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Connections
{
    public class MultiplexingConnectionTests
    {
        [Fact]
        public async Task When_Packet_Exceeds_MaxDocSize_ThrowValueTooLargeException()
        {
            var conn = new MultiplexingConnection(new MemoryStream(), new IPEndPoint(0, 0), new IPEndPoint(0, 0),
                new Logger<MultiplexingConnection>(new LoggerFactory()));

            var json = JsonConvert.SerializeObject(new string[1024 * 6145]);
            var bytes = Encoding.UTF8.GetBytes(json);

            await Assert.ThrowsAsync<ValueToolargeException>(() =>
                conn.SendAsync(bytes, Mock.Of<IOperation>()).AsTask()).ConfigureAwait(false);
        }
    }
}

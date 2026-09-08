using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Couchbase.UnitTests.Utils
{
    /// <summary>
    /// Embedded JSON fixtures, and queues of canned HTTP responses that serve them, for tests that
    /// drive a service client or a manager against a fixed server reply.
    /// </summary>
    internal static class HttpFixtures
    {
        /// <summary>
        /// Reads an embedded fixture as bytes.
        /// </summary>
        /// <param name="resourcePath">
        /// Path to the fixture, e.g. <c>@"Documents\Search\query-error-400.json"</c>. Note
        /// <see cref="ResourceHelper"/> matches on the file name alone, so the directories are
        /// documentation rather than lookup.
        /// </param>
        public static byte[] Fixture(string resourcePath)
        {
            using var stream = ResourceHelper.ReadResourceAsStream(resourcePath);
            var buffer = new byte[stream.Length];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = stream.Read(buffer, read, buffer.Length - read);
                if (n == 0) break;
                read += n;
            }

            return buffer;
        }

        /// <summary>
        /// A queue of identical responses, deep enough that a retrying call keeps getting the same
        /// reply rather than draining the queue.
        /// </summary>
        public static Queue<Task<HttpResponseMessage>> Responses(byte[] content,
            HttpStatusCode status = HttpStatusCode.NotFound, int count = 20)
        {
            var responses = new Queue<Task<HttpResponseMessage>>();
            for (var i = 0; i < count; i++)
            {
                responses.Enqueue(Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = status,
                    Content = new ByteArrayContent(content)
                }));
            }

            return responses;
        }
    }
}

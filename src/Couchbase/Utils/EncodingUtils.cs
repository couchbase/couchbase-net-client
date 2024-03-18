using System.Text;

namespace Couchbase.Utils
{
    internal static class EncodingUtils
    {
        public static readonly Encoding Utf8NoBomEncoding = new UTF8Encoding(false);
    }
}

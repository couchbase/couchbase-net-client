using System;
using System.Linq;

namespace Couchbase.Configuration.Server.Serialization
{
    public static class NodeArrayExtensions
    {
        public static bool AreEqual(this Node[] nodes, Node[] other)
        {
            if (nodes == null && other == null)
            {
                return true;
            }
            if (nodes != null && nodes.Length != other.Length)
            {
                return false;
            }
            if (nodes == null) return false;
            var ordered = nodes.OrderBy(x => x.Hostname).ToArray();
            var orderedOther = other.OrderBy(x => x.Hostname).ToArray();
            for (var i = 0; i < ordered.Count(); i++)
            {
                if (string.Compare(ordered[i].Hostname,
                    orderedOther[i].Hostname,
                    StringComparison.OrdinalIgnoreCase) != 0)
                {
                    return false;
                }
            }
            return true;
        }
    }
}

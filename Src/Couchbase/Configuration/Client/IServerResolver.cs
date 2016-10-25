using System;
using System.Collections.Generic;

namespace Couchbase.Configuration.Client
{
    /// <summary>
    /// Abstracts the implementation of retriveing a list of URIs used during the client
    /// bootstrap process by <see cref="ServerResolverUtil"/>.
    /// </summary>
    public interface IServerResolver
    {
        List<Uri> GetServers();
    }
}

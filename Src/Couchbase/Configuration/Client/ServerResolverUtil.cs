using System;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using Couchbase.Utils;

namespace Couchbase.Configuration.Client
{
    /// <summary>
    /// This utility class will try to retrive a list of Server URIs to bootstrap the client with
    /// using an implementation of <see cref="IServerResolver"/>.
    /// </summary>
    public static class ServerResolverUtil
    {
        private static readonly ILog Log = LogManager.GetLogger("ServerResolverUtil");

        public static List<Uri> GetServers(string serverResolverType)
        {
            if (string.IsNullOrEmpty(serverResolverType))
            {
                Log.Debug(ExceptionUtil.MissingOrEmptyServerResolverType);
                return null;
            }

            var resolver = GetServerResolver(serverResolverType);

            List<Uri> servers;
            try
            {
                servers = resolver.GetServers();
            }
            catch (Exception exception)
            {
                Log.Error(ExceptionUtil.ErrorRetrievingServersUsingServerResolver.WithParams(serverResolverType));
                Log.Error(exception);
                throw;
            }

            if (servers == null || !servers.Any())
            {
                var message = ExceptionUtil.ServerResolverReturnedNoservers.WithParams(serverResolverType);
                Log.Error(message);
                throw new Exception(message);
            }

            return servers;
        }

        private static IServerResolver GetServerResolver(string serverResolverType)
        {
            var type = Type.GetType(serverResolverType, false);
            if (type == null)
            {
                var message = ExceptionUtil.UnrecognisedServerResolverType.WithParams(serverResolverType);
                Log.Error(message);
                throw new Exception(message);
            }

            var resolver = Activator.CreateInstance(type) as IServerResolver;
            if (resolver == null)
            {
                var message = ExceptionUtil.ServerResolverTypeDoesntImplementInterface.WithParams(serverResolverType, typeof(IServerResolver).Name);
                Log.Error(message);
                throw new Exception(message);
            }

            return resolver;
        }
    }
}
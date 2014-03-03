using System;
using Couchbase.Configuration.Client;

namespace Couchbase.Configuration.Server.Providers.FileSystem
{
    internal class FileSystemConfigProvider : IConfigProvider
    {
        private readonly ClientConfiguration _clientConfig;
        private IConfigInfo _configInfo;
        private IServerConfig _serverConfig;

        public FileSystemConfigProvider(ClientConfiguration clientConfig)
        {
            _clientConfig = clientConfig;
        }

        public FileSystemConfigProvider(IServerConfig serverConfig, ClientConfiguration clientConfig)
        {
            _serverConfig = serverConfig;
            _clientConfig = clientConfig;
        }

        public IConfigInfo GetCached(string bucketName)
        {
            return _configInfo;
        }

        public IConfigInfo GetConfig(string bucketName)
        {
            if (_serverConfig == null)
            {
                _serverConfig = new FileSystemConfig(_clientConfig.BootstrapPath);
            }
            _configInfo = new ConfigInfo(_serverConfig, _clientConfig);
            return _configInfo;
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void RegisterListener(IConfigListener listener)
        {
            throw new NotImplementedException();
        }


        public void UnRegisterListener(IConfigListener listener)
        {
            throw new NotImplementedException();
        }

        public bool ListenerExists(IConfigListener listener)
        {
            throw new NotImplementedException();
        }
    }
}
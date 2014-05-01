using System;
using Couchbase.Configuration.Client;

namespace Couchbase.Configuration.Server.Providers.FileSystem
{
    internal class FileSystemConfigProvider : IConfigProvider
    {
        private readonly ClientConfiguration _clientConfig;
        private IConfigInfo _configInfo;
        private IServerConfig _serverConfig;
        private readonly string _bootstrapPath;

        public FileSystemConfigProvider(ClientConfiguration clientConfig)
        {
            _clientConfig = clientConfig;
        }

        public FileSystemConfigProvider(IServerConfig serverConfig, ClientConfiguration clientConfig, string bootstrapPath)
        {
            _serverConfig = serverConfig;
            _clientConfig = clientConfig;
            _bootstrapPath = bootstrapPath;
        }

        public IConfigInfo GetCached(string bucketName)
        {
            return _configInfo;
        }

        public IConfigInfo GetConfig(string bucketName)
        {
            if (_serverConfig == null)
            {
                _serverConfig = new FileSystemConfig(_bootstrapPath);
            }
            _configInfo = new ConfigInfo(_serverConfig, _clientConfig);
            return _configInfo;
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void RegisterListener(IConfigObserver observer)
        {
            throw new NotImplementedException();
        }


        public void UnRegisterObserver(IConfigObserver observer)
        {
            throw new NotImplementedException();
        }

        public bool ObserverExists(IConfigObserver observer)
        {
            throw new NotImplementedException();
        }


        bool IConfigProvider.RegisterObserver(IConfigObserver observer)
        {
            throw new NotImplementedException();
        }


        public IConfigInfo GetConfig(string name,  string password)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
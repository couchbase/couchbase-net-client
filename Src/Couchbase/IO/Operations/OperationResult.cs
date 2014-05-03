using System;
using System.Text;
using Couchbase.Configuration.Server.Serialization;

namespace Couchbase.IO.Operations
{
    internal class OperationResult<T> : IOperationResult<T>
    {
        private readonly OperationBase<T> _operation;

        public OperationResult(OperationBase<T> operation)
        {
            _operation = operation;
        }

        public virtual bool Success
        {
            get
            {
                var header = _operation.Header;
                return header.Status == ResponseStatus.Success;
            }
        }

        public virtual T Value
        {
            get
            {
                var value = default(T);
                if (Success)
                {
                    var serializer = _operation.Serializer;
                    value = serializer.Deserialize(_operation);
                }
                return value;
            }
        }

        public virtual string Message
        {
            get
            {
                var message = string.Empty;
                if (!Success)
                {
                    if (Status == ResponseStatus.VBucketBelongsToAnotherServer)
                    {
                        message = ResponseStatus.VBucketBelongsToAnotherServer.ToString();
                    }
                    else
                    {
                        try
                        {
                            var serializer = _operation.Serializer;
                            var buffer = _operation.Body.Data;
                            var header = _operation.Header;
                            var offset = OperationBase<T>.HeaderLength + header.ExtrasLength;
                            var length = header.BodyLength - header.ExtrasLength;
                            message = serializer.Deserialize(buffer, offset, length);
                        }
                        catch (Exception e)
                        {
                            message = e.Message;
                        }
                    }
                }
                return message;
            }
        }

        public virtual ResponseStatus Status
        {
            get { return _operation.Header.Status; }
        }

        public virtual ulong Cas
        {
            get { return _operation.Header.Cas; }
        }

        public virtual IBucketConfig GetConfig()
        {
            IBucketConfig config = null;
            if (Status == ResponseStatus.VBucketBelongsToAnotherServer)
            {
                var offset = OperationBase<T>.HeaderLength + _operation.Header.ExtrasLength;
                var length = _operation.Header.BodyLength - _operation.Header.ExtrasLength;

                var serializer = _operation.Serializer;
                config = serializer.Deserialize<BucketConfig>(_operation.Body.Data, offset, length);
            }
            return config;
        }
    }
}

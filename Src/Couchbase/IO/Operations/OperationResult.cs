using System;
using System.Text;

namespace Couchbase.IO.Operations
{
    internal class OperationResult<T> : IOperationResult<T>
    {
        private readonly OperationBase<T> _operation;

        public OperationResult(OperationBase<T> operation)
        {
            _operation = operation;
        }

        public bool Success
        {
            get
            {
                var header = _operation.Header;
                return header.Status == ResponseStatus.Success;
            }
        }

        public T Value
        {
            get
            {
                var serializer = _operation.Serializer;
                return serializer.Deserialize(_operation);
            }
        }

        public string Message
        {
            get
            {
                var message = string.Empty;
                var data = _operation.Body;
                if (!Success)
                {
                    try
                    {
                        message = Encoding.ASCII.GetString(
                            data.Data.Array,
                            data.Data.Offset,
                            data.Data.Array.Length);
                    }
                    catch (Exception e)
                    {
                        message = e.Message;
                    }
                }
                return message;
            }
        }


        public ResponseStatus Status
        {
            get { return _operation.Header.Status; }
        }

        public ulong Cas
        {
            get { return _operation.Header.Cas; }
        }
    }
}

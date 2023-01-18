using System;
using System.Buffers;
using System.ComponentModel;
using System.Data;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.RangeScan;
using Couchbase.Utils;
using Couchbase.Core.IO.Connections;
using System.IO;
using Microsoft.Extensions.Logging;
using ByteConverter = Couchbase.Core.IO.Converters.ByteConverter;

namespace Couchbase.Core.IO
{
    internal class AsyncStateStreaming : AsyncStateBase
    {
        InFlightOperationSet _statesInFlight;
        private ILogger<MultiplexingConnection> _logger;
        public AsyncStateStreaming(IOperation operation, InFlightOperationSet statesInFlight, ILogger<MultiplexingConnection> logger) : base(operation)
        {
            _logger = logger;
            _statesInFlight = statesInFlight;
        }

        public override void Complete(in SlicedMemoryOwner<byte> response)
        {
            var status = (ResponseStatus)ByteConverter.ToInt16(response.Memory.Span.Slice(HeaderOffsets.Status), true);
            var observer = (RangeScanContinue)Operation;

            switch (status)
            {
                case ResponseStatus.RangeScanComplete://scan is complete
                    {
                        _logger.LogDebug("Receiving RangeScan Complete for opaque {Opaque}", Opaque);

                        observer.OnNext(response);
                        observer.OnCompleted();
                        base.Complete(response);
                        break;
                    }
                case ResponseStatus.Success: //scan intermediate response
                    {
                        _logger.LogDebug("Receiving RangeScan Success for opaque {Opaque}", Opaque);
                        observer.OnNext(response);

                        //put the op back on the stack so the next response can be read
                        Operation.Reset();
                        _statesInFlight.Add(this);
                        break;
                    }
                case ResponseStatus.RangeScanMore:
                {
                    observer.OnNext(response);
                    observer.OnCompleted();
                    base.Complete(response);
                    break;
                }
                default:
                {
                    throw new Exception("Woah, didn't expect that!");
                }
            }
        }
    }
}

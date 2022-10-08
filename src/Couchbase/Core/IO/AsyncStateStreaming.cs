using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.RangeScan;
using Couchbase.Utils;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Connections;
using System.IO;

namespace Couchbase.Core.IO
{
    internal class AsyncStateStreaming : AsyncStateBase
    {
        InFlightOperationSet _statesInFlight;
        private volatile bool _firstResponse = true;
        public AsyncStateStreaming(IOperation operation, InFlightOperationSet statesInFlight) : base(operation)
        {
            _statesInFlight = statesInFlight;
        }


        public override void Complete(in SlicedMemoryOwner<byte> response)
        {
            var status = (ResponseStatus)ByteConverter.ToInt16(response.Memory.Span.Slice(HeaderOffsets.Status), true);
            var observer = (RangeScanContinue)Operation;
            _response = response;

            switch (status)
            {
                case ResponseStatus.RangeScanComplete://scan is complete
                    {
                        observer.OnNext(this);
                        observer.OnCompleted();
                        base.Complete(response);
                        break;
                    }
                case ResponseStatus.Success: //scan intermediate response
                    {
                        //hack to make the operation return immediatly so that the iteration can be done
                        //each response note that some of logic from base.Complete is omitted at this time.
                        //That logic must be added back in.
                        if (_firstResponse)
                        {
                            SendResponse(in response);
                            _firstResponse = false;
                        }

                        //Feed the intermediate response to be consumed
                        observer.OnNext(this);

                        //put the op back on the stack so the next response can be read
                        Operation.Reset();
                        _statesInFlight.Add(this);
                        break;
                    }
                    default:
                    {
                        //throw error?
                        return;
                    }
            }
        }
    }
}

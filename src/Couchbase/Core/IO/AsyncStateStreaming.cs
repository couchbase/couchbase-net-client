using System;
using System.Collections.Generic;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.RangeScan;
using Couchbase.Utils;
using Couchbase.Core.IO.Connections;
using Couchbase.KeyValue.RangeScan;
using Microsoft.Extensions.Logging;
using ByteConverter = Couchbase.Core.IO.Converters.ByteConverter;

namespace Couchbase.Core.IO
{
    internal sealed class AsyncStateStreaming : AsyncStateBase
    {
        private readonly ILogger<MultiplexingConnection> _logger;
        private const bool StreamMoreExpected = false;
        private const bool StreamEnded = true;

        public AsyncStateStreaming(IOperation operation, ILogger<MultiplexingConnection> logger) : base(operation)
        {
            _logger = logger;
        }

        public override bool Complete(in SlicedMemoryOwner<byte> response)
        {
            var status = (ResponseStatus)ByteConverter.ToInt16(response.Memory.Span.Slice(HeaderOffsets.Status), true);
            var observer = (RangeScanContinue)Operation;

            if (CompleteEarlyIfErrorsDuringSamplingScan(status, observer, response))
            {
                return StreamEnded;
            }

            switch (status)
            {
                case ResponseStatus.RangeScanComplete://scan is complete
                    {
                        _logger.LogDebug("Receiving RangeScan Complete for opaque {Opaque}", Opaque);

                        observer.OnNext(response);
                        observer.OnCompleted();
                        return base.Complete(response);
                    }
                case ResponseStatus.Success: //scan intermediate response
                    {
                        _logger.LogDebug("Receiving RangeScan Success for opaque {Opaque}", Opaque);
                        observer.OnNext(response);

                        //put the op back on the stack so the next response can be read
                        Operation.Reset();
                        return StreamMoreExpected;
                    }
                case ResponseStatus.RangeScanMore:
                {
                    observer.OnNext(response);
                    observer.OnCompleted();
                    return base.Complete(response);
                }
                case ResponseStatus.VBucketBelongsToAnotherServer:
                {
                    _logger.LogDebug("Received {err} for opaque {Opague}",
                        nameof(ResponseStatus.VBucketBelongsToAnotherServer), Opaque);
                    observer.OnNext(response);
                    Operation.Reset();
                    return StreamMoreExpected;
                }
                // No scan with the given uuid could be found.
                // Since the UUID just came from the create operation, this is an error condition and should fail the substream.
                case ResponseStatus.KeyNotFound:
                    throw new KeyNotFoundException(nameof(RangeScanCreate));

                //The user no longer has the required privilege for range-scans.
                // If the privileges are revoked during a stream there is no real chance to get it going before the timeout hits - fail the substream.
                case ResponseStatus.Eaccess:
                    throw new AuthenticationFailureException(nameof(RangeScan.RangeScan));

                // The collection was dropped.
                // If the collection is dropped mid-stream, fail the substream.
                case ResponseStatus.UnknownCollection:
                    throw new CollectionNotFoundException(nameof(RangeScan.RangeScan));
                case ResponseStatus.RangeScanCanceled:
                    throw new RequestCanceledException(nameof(RangeScan.RangeScan));
                case ResponseStatus.InvalidArguments:
                    throw new InvalidArgumentException(nameof(RangeScan.RangeScan));
                case ResponseStatus.Busy:
                    throw new InvalidOperationException("The specified scan is Busy (a RangeScanContinue is already in progress)");
                default:
                {
                    _logger.LogError("Unexpected RangeScan Response Status: {status}", status);
                    throw new InvalidOperationException($"Unexpected RangeScan Response Status: {status}");
                }
            }
        }

        private bool CompleteEarlyIfErrorsDuringSamplingScan(ResponseStatus status, RangeScanContinue observer,
            SlicedMemoryOwner<byte> response)
        {
            // These status codes result in a fatal error for normal RangeScan requests, but not for
            // SamplingScan requests.
            // See section "Fatal (Unless Sampling Scan) in the RFC"
            if (observer.IsSampling)
            {
                switch (status)
                {
                    case ResponseStatus.KeyNotFound:
                    case ResponseStatus.Eaccess:
                    case ResponseStatus.UnknownCollection:
                    case ResponseStatus.RangeScanCanceled:
                        // we can just treat it as success end the response
                        // stream at the current point.
                        observer.OnCompleted();
                        return base.Complete(response);
                }
            }

            return false;
        }
    }
}

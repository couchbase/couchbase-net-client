using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.RangeScan;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.KeyValue.RangeScan;

internal sealed class PartitionScan
{
    /// <remarks>
    /// <c>createSpan</c> creates a span with the given name and parent, with the attributes of a request on
    /// this collection. <c>scanSpan</c> is the range_scan span of the scan that this partition belongs to.
    /// </remarks>
    public PartitionScan(IOperationConfigurator operationConfigurator, BucketBase bucket, ICouchbaseCollection collection, ILogger<GetResult> getLogger, ScanOptions options, IScanType scanType, short partitionId,
        Func<string, IRequestSpan, IRequestSpan> createSpan, IRequestSpan scanSpan)
    {
        _createSpan = createSpan;
        _scanSpan = scanSpan;
        _operationConfigurator = operationConfigurator;
        _bucket = bucket;
        _collection = collection;
        _getLogger = getLogger;
        _options = options;
        _scanType = scanType;
        _partitionId = partitionId;
    }

    private readonly IOperationConfigurator _operationConfigurator;
    private readonly BucketBase _bucket;
    private readonly ICouchbaseCollection _collection;
    private readonly ILogger<GetResult> _getLogger;
    private readonly ScanOptions _options;
    private readonly IScanType _scanType;
    private readonly short _partitionId;
    private SlicedMemoryOwner<byte>? _uuid;
    private readonly Func<string, IRequestSpan, IRequestSpan> _createSpan;
    private readonly IRequestSpan _scanSpan;
    private IRequestSpan _partitionSpan;
    private string _rangeScanId;

    public MutationToken MutationToken { get; set; }

    public IDictionary<string, IScanResult> Results { get; private set; }

    public ResponseStatus Status { get; set; }

    public short PartitionId => _partitionId;

    /// <summary>
    /// Whether the server holds a scan for this partition that has not completed, so it must be
    /// closed with <see cref="CancelAsync"/> if the caller stops early.
    /// </summary>
    public bool IsOpen => CanBeCanceled && _uuid.HasValue &&
                          Status is not (ResponseStatus.RangeScanComplete or ResponseStatus.KeyNotFound);

    public async Task<PartitionScan> ScanAsync()
    {
        // The request spans are disposed when ScanBatchAsync returns, so the partition span ends after them.
        if (await ScanBatchAsync().ConfigureAwait(false))
        {
            EndSpan();
        }

        return this;
    }

    /// <summary>
    /// Sends the next request for this partition. Returns true when the partition is done.
    /// </summary>
    private async Task<bool> ScanBatchAsync()
    {
        if (_uuid == null)
        {
            var scanCreateOp = new RangeScanCreate
            {
                Content = _scanType as IScanTypeExt, //need to change this
                KeyOnly = _options!.IdsOnlyValue,
                Cid = _collection.Cid,
                CName = _collection.Name,
                SName = _collection.Scope.Name,
                VBucketId = _partitionId,
                MutationToken = MutationToken
            };

            using var createSpan = _createSpan(OuterRequestSpans.ServiceSpan.Kv.RangeScanCreate, PartitionSpan);
            RangeScanSpans.SetScanTypeAttributes(createSpan, _scanType, _options.IdsOnlyValue, isCreateSpan: true);
            scanCreateOp.Span = createSpan;

            _operationConfigurator.Configure(scanCreateOp, _options);
            using var ctp = CreateRetryTimeoutCancellationTokenSource(_options, scanCreateOp);

            var scanCreateStatus = await _bucket.RetryAsync(scanCreateOp, ctp.TokenPair).ConfigureAwait(false);
            switch (scanCreateStatus)
            {
                case ResponseStatus.RangeScanComplete:
                    // The scan has no batch to read. Status must be terminal, or the scan loop sends the create
                    // again, and Results must not be null, because the loop reads it for this status.
                    CanBeCanceled = false;
                    Status = scanCreateStatus;
                    Results = new Dictionary<string, IScanResult>();
                    return true;
                case ResponseStatus.KeyNotFound:
                    Status = scanCreateStatus;
                    return true;
                case ResponseStatus.RangeScanMore:
                case ResponseStatus.Success:
                {
                    // The create is done. End its span now, so that it does not include the first continue.
                    createSpan.Dispose();
                    _uuid = scanCreateOp.ExtractBody();
                    _rangeScanId = "0x" + BitConverter.ToString(_uuid.Value.Memory.ToArray()).Replace("-", "").ToLowerInvariant();
                    var scanContinueOp = new RangeScanContinue
                    {
                        Cid = _collection.Cid,
                        CName = _collection.Name,
                        SName = _collection.Scope.Name,
                        VBucketId = _partitionId,
                        Uuid = _uuid.Value,
                        IdsOnly = _options.IdsOnlyValue,
                        Logger = _getLogger,
                        TimeLimit = _options.BatchTimeLimit,
                        ItemLimit = _options.BatchItemLimit,
                        ByteLimit = _options.BatchByteLimit,
                        IsSampling = scanCreateOp.Content?.IsSampling == true,
                    };
                    _operationConfigurator.Configure(scanContinueOp, _options);
                    using var continueSpan = ContinueSpan(scanContinueOp);

                    using var ctp2 = CreateRetryTimeoutCancellationTokenSource(_options, scanContinueOp);
                    Status = await _bucket.RetryAsync(scanContinueOp, ctp2.TokenPair).ConfigureAwait(false);
                    Results = scanContinueOp.Content;
                    break;
                }
                case ResponseStatus.VBucketBelongsToAnotherServer:
                    Status = await _bucket.RetryAsync(scanCreateOp, ctp.TokenPair).ConfigureAwait(false);
                    break;
            }
        }
        else
        {
            //this would be a range scan more - it would trigger the next loop through the vbuckets fetching those batches
            var scanContinueOp = new RangeScanContinue
            {
                Cid = _collection.Cid,
                CName = _collection.Name,
                SName = _collection.Scope.Name,
                VBucketId = _partitionId,
                Uuid = _uuid.Value,
                IdsOnly = _options.IdsOnlyValue,
                Logger = _getLogger,
                TimeLimit = _options.BatchTimeLimit,
                ItemLimit = _options.BatchItemLimit,
                ByteLimit = _options.BatchByteLimit
            };
            _operationConfigurator.Configure(scanContinueOp, _options);
            using var continueSpan = ContinueSpan(scanContinueOp);

            using var ctp2 = CreateRetryTimeoutCancellationTokenSource(_options, scanContinueOp);
            Status = await _bucket.RetryAsync(scanContinueOp, ctp2.TokenPair).ConfigureAwait(false);
            Results = scanContinueOp.Content;
        }

        return Status is ResponseStatus.RangeScanComplete or ResponseStatus.KeyNotFound;
    }

    /// <summary>
    /// The range_scan_partition span. It is created with the first request for this partition.
    /// </summary>
    private IRequestSpan PartitionSpan
    {
        get
        {
            if (_partitionSpan is null)
            {
                _partitionSpan = _createSpan(OuterRequestSpans.ServiceSpan.Kv.RangeScanPartition, _scanSpan);
                if (_partitionSpan.CanWrite)
                {
                    _partitionSpan.SetAttribute(RangeScanSpans.PartitionId, (uint)_partitionId);
                }
            }

            return _partitionSpan;
        }
    }

    private IRequestSpan ContinueSpan(RangeScanContinue scanContinueOp)
    {
        var span = _createSpan(OuterRequestSpans.ServiceSpan.Kv.RangeScanContinue, PartitionSpan);
        if (span.CanWrite)
        {
            span.SetAttribute(RangeScanSpans.RangeScanId, _rangeScanId ?? string.Empty);
            span.SetAttribute(RangeScanSpans.ItemLimit, scanContinueOp.ItemLimit);
            span.SetAttribute(RangeScanSpans.ByteLimit, scanContinueOp.ByteLimit);
            span.SetAttribute(RangeScanSpans.TimeLimit, scanContinueOp.TimeLimit);
        }

        scanContinueOp.Span = span;
        return span;
    }

    /// <summary>
    /// Ends the range_scan_partition span, if it was created. Safe to call more than once.
    /// </summary>
    public void EndSpan()
    {
        _partitionSpan?.Dispose();
        _partitionSpan = null;
    }

    public bool CanBeCanceled
    {
        get;
        private set;
    } = true;

    public async Task CancelAsync()
    {
        if (CanBeCanceled && _uuid.HasValue)
        {
            _getLogger.LogDebug("Cancelling range scan for VBID {vbid}", _partitionId);
            var scanCancelOp = new RangeScanCancel
            {
                Content = _uuid.Value,
                VBucketId = _partitionId
            };

            using var cancelSpan = _createSpan(OuterRequestSpans.ServiceSpan.Kv.RangeScanCancel, PartitionSpan);
            if (cancelSpan.CanWrite)
            {
                cancelSpan.SetAttribute(RangeScanSpans.RangeScanId, _rangeScanId ?? string.Empty);
            }
            scanCancelOp.Span = cancelSpan;

            _operationConfigurator.Configure(scanCancelOp, _options);

            // Not the options token: the scan is closed because that token was cancelled, and a
            // close with it would fail before it is sent.
            using var ctp2 = CancellationTokenPairSource.FromTimeout(GetTimeout(_options.TimeoutValue, scanCancelOp),
                CancellationToken.None);
            Status = await _bucket.RetryAsync(scanCancelOp, ctp2.TokenPair).ConfigureAwait(false);
        }
    }

    private CancellationTokenPairSource CreateRetryTimeoutCancellationTokenSource(
        ITimeoutOptions options, IOperation op) =>
        CancellationTokenPairSource.FromTimeout(GetTimeout(options.Timeout, op), options.Token);

    private TimeSpan GetTimeout(TimeSpan? optionsTimeout, IOperation op)
    {
        if (optionsTimeout == null || optionsTimeout.Value == TimeSpan.Zero)
        {
            if (op.HasDurability)
            {
                op.Timeout = _bucket.Context.ClusterOptions.KvDurabilityTimeout;
                return op.Timeout;
            }

            optionsTimeout = _bucket.Context.ClusterOptions.KvTimeout;
        }

        return op.Timeout = optionsTimeout.Value;
    }
}

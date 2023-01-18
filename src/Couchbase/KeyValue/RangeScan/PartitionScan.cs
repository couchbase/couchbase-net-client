using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.RangeScan;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.KeyValue.RangeScan;

internal record PartitionScan
{
    public PartitionScan(IOperationConfigurator operationConfigurator, BucketBase bucket, ICouchbaseCollection collection, ILogger<GetResult> getLogger, ScanOptions options, IScanType scanType, short partitionId)
    {
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

    public IDictionary<string, IScanResult> Results { get; private set; }

    public ResponseStatus Status { get; set; }

    public async Task<PartitionScan> ScanAsync()
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
                VBucketId = _partitionId
            };

            _operationConfigurator.Configure(scanCreateOp, _options);
            using var ctp = CreateRetryTimeoutCancellationTokenSource(_options, scanCreateOp);

            var scanCreateStatus = await _bucket.RetryAsync(scanCreateOp, ctp.TokenPair).ConfigureAwait(false);
            switch (scanCreateStatus)
            {
                case ResponseStatus.KeyNotFound:
                    Status = scanCreateStatus;
                    return this;
                case ResponseStatus.RangeScanMore:
                case ResponseStatus.Success:
                {
                    _uuid = scanCreateOp.ExtractBody();
                    var scanContinueOp = new RangeScanContinue
                    {
                        Cid = _collection.Cid,
                        CName = _collection.Name,
                        SName = _collection.Scope.Name,
                        VBucketId = _partitionId,
                        Uuid = _uuid.Value,
                        IdsOnly = _options.IdsOnlyValue,
                        Logger = _getLogger,
                        ItemLimit = _options.BatchItemLimit,
                        ByteLimit = _options.BatchByteLimit
                    };
                    _operationConfigurator.Configure(scanContinueOp, _options);

                    using var ctp2 = CreateRetryTimeoutCancellationTokenSource(_options, scanContinueOp);
                    Status = await _bucket.RetryAsync(scanContinueOp, ctp2.TokenPair).ConfigureAwait(false);
                    Results = scanContinueOp.Content;
                    break;
                }
            }
        }
        else
        {
            Console.WriteLine($"RangeScanContinue started");
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
                ItemLimit = _options.BatchItemLimit,
                ByteLimit = _options.BatchByteLimit
            };
            _operationConfigurator.Configure(scanContinueOp, _options);

            using var ctp2 = CreateRetryTimeoutCancellationTokenSource(_options, scanContinueOp);
            Status = await _bucket.RetryAsync(scanContinueOp, ctp2.TokenPair).ConfigureAwait(false);
            Results = scanContinueOp.Content;
        }

        return this;
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

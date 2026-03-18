using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.KeyValue;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

internal static class AppTelemetryUtils
{
    internal static readonly string Agent = $"sdk/couchbase-net-client v{Assembly.GetAssembly(typeof(Cluster))?.GetName().Version} OS: {RuntimeInformation.OSDescription} Framework: {RuntimeInformation.FrameworkDescription} Architecture: {RuntimeInformation.ProcessArchitecture} CPU Cores: {Environment.ProcessorCount}";

    internal static AppTelemetryRequestType? GetAppTelemetryKvRequestType(IOperation operation)
    {
        if (IsRetrievalOperation(operation.OpCode))
            return AppTelemetryRequestType.KvRetrieval;

        if (IsMutationOperation(operation.OpCode))
        {
            return operation.HasDurability
                ? AppTelemetryRequestType.KvMutationDurable
                : AppTelemetryRequestType.KvMutationNonDurable;
        }

        // Internal operations (Hello, GetCid, SaslAuth, etc.) should not be tracked.
        return null;
    }

    private static bool IsRetrievalOperation(OpCode opCode)
    {
        return opCode switch
        {
            OpCode.Get => true,
            OpCode.GetQ => true,
            OpCode.GetK => true,
            OpCode.GetKQ => true,
            OpCode.GetL => true,
            OpCode.ReplicaRead => true,
            OpCode.GetMeta => true,
            OpCode.SubGet => true,
            OpCode.SubExist => true,
            OpCode.SubGetCount => true,
            OpCode.MultiLookup => true,
            OpCode.RangeScanCreate => true,
            OpCode.RangeScanContinue => true,
            OpCode.RangeScanCancel => true,
            _ => false
        };
    }

    private static bool IsMutationOperation(OpCode opCode)
    {
        return opCode switch
        {
            OpCode.Set => true,
            OpCode.SetQ => true,
            OpCode.Add => true,
            OpCode.AddQ => true,
            OpCode.Replace => true,
            OpCode.ReplaceQ => true,
            OpCode.Delete => true,
            OpCode.DeleteQ => true,
            OpCode.Append => true,
            OpCode.AppendQ => true,
            OpCode.Prepend => true,
            OpCode.PrependQ => true,
            OpCode.Touch => true,
            OpCode.GAT => true,
            OpCode.GATQ => true,
            OpCode.Increment => true,
            OpCode.IncrementQ => true,
            OpCode.Decrement => true,
            OpCode.DecrementQ => true,
            OpCode.SubDictAdd => true,
            OpCode.SubDictUpsert => true,
            OpCode.SubDelete => true,
            OpCode.SubReplace => true,
            OpCode.SubArrayPushLast => true,
            OpCode.SubArrayPushFirst => true,
            OpCode.SubArrayInsert => true,
            OpCode.SubArrayAddUnique => true,
            OpCode.SubCounter => true,
            OpCode.SubMultiMutation => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines the AppTelemetryRequestType based on the given AppTelemetryServiceType.
    /// Only the KV service can have different AppTelemetryRequestTypes, other services have a 1:1 mapping.
    /// This helps remove duplicate code in the calling methods.
    /// </summary>
    /// <param name="serviceType"></param>
    /// <returns></returns>
    /// <exception cref="InvalidArgumentException"></exception>
    internal static AppTelemetryRequestType DetermineAppTelemetryRequestType(AppTelemetryServiceType serviceType)
    {
        return serviceType switch
        {
            AppTelemetryServiceType.Analytics => AppTelemetryRequestType.Analytics,
            AppTelemetryServiceType.Eventing => AppTelemetryRequestType.Eventing,
            AppTelemetryServiceType.Management => AppTelemetryRequestType.Management,
            AppTelemetryServiceType.Query => AppTelemetryRequestType.Query,
            AppTelemetryServiceType.Search => AppTelemetryRequestType.Search,
            _ => throw new InvalidArgumentException(
                $"Invalid AppTelemetryRequestType for given serviceType: {serviceType}.")
        };
    }
}

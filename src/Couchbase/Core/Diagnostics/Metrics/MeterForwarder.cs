using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;

#nullable enable

namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// When using a <see cref="IMeter"/> other than the <see cref="NoopMeter"/>, implements the logic to forward
    /// .NET metrics to the meter implementation. When using the <see cref="NoopMeter"/> this class is not activated
    /// so that .NET doesn't spend CPU cycles collecting the metrics unnecessarily.
    /// </summary>
    /// <remarks>
    /// Note that the metrics may also be consumed without using <see cref="IMeter"/> by using any standard .NET
    /// metric consumer. This may include OpenTelemetry, dotnet-counters, dotnet-monitor, and more.
    /// </remarks>
    internal sealed class MeterForwarder : IDisposable
    {
        private readonly IMeter _meter;
        private readonly MeterListener _listener;

        public MeterForwarder(IMeter meter)
        {
            _meter = meter;

            _listener = new MeterListener();
            _listener.InstrumentPublished = InstrumentPublished;
            _listener.SetMeasurementEventCallback<long>(MeasurementCallback);
            _listener.Start();
        }

        private void InstrumentPublished(Instrument instrument, MeterListener listener)
        {
            if (instrument.Meter.Name == MetricTracker.MeterName && instrument.Name == MetricTracker.Names.Operations)
            {
                // For logging meters, only track the operation duration histogram for now.
                // That's currently the way the IMeter.ValueRecorder API is used, it's passing the service and assuming
                // that the meter is always the request duration. This seems incorrect, but we'll retain for consistency for now.
                listener.EnableMeasurementEvents(instrument);
            }
        }

        private void MeasurementCallback(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            string? service = null;
            KeyValuePair<string, string>? operationTag = null;
            Dictionary<string, string>? additionalTags = null;

            bool isCustomMeter = _meter is not LoggingMeter and not NoopMeter;

            foreach (KeyValuePair<string, object?> tag in tags)
            {
                switch (tag.Key)
                {
                    case OuterRequestSpans.Attributes.Service:
                        service = tag.Value?.ToString();
                        break;

                    case OuterRequestSpans.Attributes.Operation:
                        var operation = tag.Value?.ToString();
                        if (operation is not null)
                        {
                            operationTag = new KeyValuePair<string, string>(
                                OuterRequestSpans.Attributes.Operation, operation);
                        }

                        break;

                    default:
                        // Only custom meters use other tag, only process them for custom meters.
                        // Note that OpenTelemetry doesn't pass through MeterForwarder/IMeter at all, it
                        // subscribes directly to the .NET metrics provider.
                        if (isCustomMeter)
                        {
                            var value = tag.Value?.ToString();
                            if (value is not null)
                            {
                                additionalTags ??= [];
                                additionalTags.Add(tag.Key, value);
                            }
                        }

                        break;
                }
            }

            if (service == null)
            {
                // We need the service tag to find the right IValueRecorder
                return;
            }

            _meter.ValueRecorder(service, additionalTags).RecordValue((uint)measurement, operationTag);
        }

        public void Dispose()
        {
            _listener.Dispose();
        }
    }
}

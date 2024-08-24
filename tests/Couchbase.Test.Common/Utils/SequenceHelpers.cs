using System;
using System.Buffers;
using System.Runtime.CompilerServices;

#nullable enable

namespace Couchbase.Test.Common.Utils
{
    public static class SequenceHelpers
    {
        public static ReadOnlySequence<byte> CreateSequenceFromSplitIndex(ReadOnlyMemory<byte> source, int splitIndex)
        {
            var second = new BufferSegment(
                source.Slice(splitIndex),
                null,
                splitIndex);
            var first = new BufferSegment(
                source.Slice(0, splitIndex),
                second,
                0);

            return new ReadOnlySequence<byte>(first, 0, second, second.Memory.Length);
        }

        public static ReadOnlySequence<byte> CreateSequenceWithMaxSegmentSize(ReadOnlyMemory<byte> source, int maxSegmentSize)
        {
            ReadOnlySequenceSegment<byte>? lastSegment = null;
            ReadOnlySequenceSegment<byte>? currentSegment = null;

            while (source.Length > 0)
            {
                var index = Math.Max(source.Length - maxSegmentSize, 0);
                currentSegment = new BufferSegment(
                    source.Slice(index),
                    currentSegment,
                    index);

                lastSegment ??= currentSegment;
                source = source.Slice(0, index);
            }

            if (currentSegment is null)
            {
                return default;
            }

            return new ReadOnlySequence<byte>(currentSegment, 0, lastSegment!, lastSegment!.Memory.Length);
        }

        public static bool SequenceEqual<T>(this ReadOnlySequence<T> first, ReadOnlySequence<T> second)
            where T : IEquatable<T>
        {
            if (first.Length != second.Length)
            {
                return false;
            }

            foreach (var firstSegment in first)
            {
                var equivalentSequence = second.Slice(0, firstSegment.Length);
                if (equivalentSequence.IsSingleSegment)
                {
                    if (!firstSegment.Span.SequenceEqual(equivalentSequence.GetFirstSpan()))
                    {
                        return false;
                    }
                }
                else
                {
                    var firstSpan = firstSegment.Span;
                    foreach (var secondSegment in equivalentSequence)
                    {
                        if (!firstSegment.Span.Slice(0, secondSegment.Length).SequenceEqual(secondSegment.Span))
                        {
                            return false;
                        }

                        firstSpan = firstSpan.Slice(secondSegment.Length);
                    }
                }

                second = second.Slice(firstSegment.Length);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<T> GetFirstSpan<T>(this ReadOnlySequence<T> sequence) =>
#if NET6_0_OR_GREATER
            sequence.FirstSpan;
#else
            sequence.First.Span;
#endif

        private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
        {
            public BufferSegment(ReadOnlyMemory<byte> memory, ReadOnlySequenceSegment<byte>? next, long runningIndex)
            {
                Memory = memory;
                Next = next;
                RunningIndex = runningIndex;
            }
        }
    }
}

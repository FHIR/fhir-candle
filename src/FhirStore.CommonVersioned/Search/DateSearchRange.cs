// <copyright file="DateSearchRange.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Search;

/// <summary>A nonempty date search interval represented by half-open UTC ticks.</summary>
internal readonly struct DateSearchRange
{
    // The exclusive ceiling is a tick sentinel, not a representable date.
    private static readonly long _utcCeiling = DateTime.MaxValue.Ticks + 1;

    private readonly long _utcStart;
    private readonly long _utcEndExclusive;

    private DateSearchRange(long utcStart, long utcEndExclusive)
    {
        _utcStart = utcStart;
        _utcEndExclusive = utcEndExclusive;
    }

    internal DateTimeOffset Start => new(_utcStart, TimeSpan.Zero);

    internal DateTimeOffset End => new(_utcEndExclusive - 1, TimeSpan.Zero);

    internal static bool TryIntersect(long utcStart, long utcEndExclusive, out DateSearchRange range)
    {
        long start = Math.Max(0, utcStart);
        long end = Math.Min(_utcCeiling, utcEndExclusive);
        if (start >= end)
        {
            range = default;
            return false;
        }

        range = new(start, end);
        return true;
    }

    /// <summary>Expands a nonempty inclusive interval by a nonnegative delta without exceeding the UTC caps.</summary>
    internal static DateSearchRange Expand(DateTimeOffset start, DateTimeOffset end, TimeSpan delta)
    {
        long lo = start.UtcTicks;
        long hi = end.UtcTicks + 1;
        return new(lo - Math.Min(lo, delta.Ticks), hi + Math.Min(_utcCeiling - hi, delta.Ticks));
    }
}

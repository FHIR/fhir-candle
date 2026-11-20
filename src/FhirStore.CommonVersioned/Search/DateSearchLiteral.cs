// <copyright file="DateSearchLiteral.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Search;

/// <summary>Parses a decoded date search literal into its precision interval in UTC.</summary>
internal static class DateSearchLiteral
{
    internal static bool TryParse(string? literal, out DateSearchRange range, out TimeSpan approxDelta)
    {
        range = default;
        approxDelta = TimeSpan.Zero;
        ReadOnlySpan<char> text = literal.AsSpan();
        if (text.Length < 4 || !TryReadNumber(text[..4], out int year) || year == 0)
        {
            return false;
        }

        int month = 1;
        if (text.Length > 4 &&
            (text.Length < 7 || text[4] != '-' || !TryReadNumber(text[5..7], out month) || month is < 1 or > 12))
        {
            return false;
        }

        int day = 1;
        int monthDays = DateTime.DaysInMonth(year, month);
        if (text.Length > 7 &&
            (text.Length < 10 || text[7] != '-' || !TryReadNumber(text[8..10], out day) || day < 1 || day > monthDays))
        {
            return false;
        }

        long timeTicks = 0;
        long offsetTicks = 0;
        (long width, int approxDays) = text.Length switch
        {
            4 => ((DateTime.IsLeapYear(year) ? 366 : 365) * TimeSpan.TicksPerDay, 365),
            // The longest two-month span is 62 days; 65 leaves a margin for time zones and inclusive endpoints.
            // Use the same fixed ap delta on both sides before saturation; AddMonths(2) would vary with
            // month length and make the window asymmetric around February.
            7 => (monthDays * TimeSpan.TicksPerDay, 65),
            10 => (TimeSpan.TicksPerDay, 31),
            _ => (0L, 1),
        };

        if (text.Length > 10 && !TryParseTime(text, out timeTicks, out width, out offsetTicks))
        {
            return false;
        }

        // Keep a fractional carry in ticks so an explicit offset can bring it back into range.
        long utcStart = new DateTime(year, month, day).Ticks + timeTicks - offsetTicks;
        if (!DateSearchRange.TryIntersect(utcStart, utcStart + width, out range))
        {
            return false;
        }

        approxDelta = TimeSpan.FromDays(approxDays);
        return true;
    }

    private static bool TryParseTime(ReadOnlySpan<char> text, out long ticks, out long width, out long offsetTicks)
    {
        ticks = 0;
        width = 0;
        offsetTicks = 0;
        if (text.Length < 16 || text[10] is not ('T' or 't') || text[13] != ':' ||
            !TryReadNumber(text[11..13], out int hour) || hour > 23 ||
            !TryReadNumber(text[14..16], out int minute) || minute > 59)
        {
            return false;
        }

        ticks = (hour * TimeSpan.TicksPerHour) + (minute * TimeSpan.TicksPerMinute);
        ReadOnlySpan<char> suffix = text[16..];
        if (suffix.IsEmpty)
        {
            width = TimeSpan.TicksPerMinute;
            return true;
        }

        // Preserve the effective ISO forms of the original length gate. In particular,
        // accepted offset minutes use the same one-second width as seconds literals.
        if (text.Length is not (19 or 20 or 22 or 23 or 24 or 25 or 27 or 28 or 29 or 30))
        {
            return false;
        }

        if (suffix[0] == ':')
        {
            if (suffix.Length < 3 || !TryReadNumber(suffix[1..3], out int second) || second > 59)
            {
                return false;
            }

            ticks += second * TimeSpan.TicksPerSecond;
            suffix = suffix[3..];
            if (!suffix.IsEmpty && suffix[0] is '.' or ',')
            {
                int i = 1;
                double fraction = 0;
                double place = 0.1;
                while (i < suffix.Length && char.IsAsciiDigit(suffix[i]))
                {
                    // Match DateTime's digit-by-digit rounding, including binary midpoint behavior.
                    fraction += (suffix[i] - '0') * place;
                    place *= 0.1;
                    i++;
                }

                if (i == 1)
                {
                    return false;
                }

                ticks += (long)Math.Round(fraction * TimeSpan.TicksPerSecond);
                suffix = suffix[i..];
            }
        }

        width = TimeSpan.TicksPerSecond;
        return TryParseOffset(suffix, out offsetTicks);
    }

    private static bool TryParseOffset(ReadOnlySpan<char> text, out long ticks)
    {
        ticks = 0;
        if (text.IsEmpty || (text.Length == 1 && text[0] is 'Z' or 'z'))
        {
            return true;
        }

        char sign = text[0];
        if (sign is not ('+' or '-'))
        {
            return false;
        }

        ReadOnlySpan<char> zone = text[1..];
        int separator = zone.IndexOf(':');
        ReadOnlySpan<char> hours = separator >= 0 ? zone[..separator] : zone.Length > 2 ? zone[..^2] : zone;
        ReadOnlySpan<char> minutes = separator >= 0 ? zone[(separator + 1)..] : zone.Length > 2 ? zone[^2..] : default;
        int minute = 0;
        if (hours.Length is < 1 or > 2 || !TryReadNumber(hours, out int hour) || hour > 14 ||
            ((separator >= 0 || !minutes.IsEmpty) && (minutes.Length > 2 || !TryReadNumber(minutes, out minute) || minute > 59)) ||
            (hour == 14 && minute != 0))
        {
            return false;
        }

        ticks = ((hour * TimeSpan.TicksPerHour) + (minute * TimeSpan.TicksPerMinute)) * (sign == '-' ? -1 : 1);
        return true;
    }

    private static bool TryReadNumber(ReadOnlySpan<char> digits, out int value)
    {
        value = 0;
        foreach (char digit in digits)
        {
            if (!char.IsAsciiDigit(digit))
            {
                return false;
            }

            value = (value * 10) + (digit - '0');
        }

        return !digits.IsEmpty;
    }
}

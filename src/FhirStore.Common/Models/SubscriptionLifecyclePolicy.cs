// <copyright file="SubscriptionLifecyclePolicy.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Models;

/// <summary>Pure UTC deadline and notification eligibility rules for subscriptions.</summary>
public static class SubscriptionLifecyclePolicy
{
    /// <summary>The sole expiration value representing an unlimited lifetime. Zero is finite.</summary>
    public const long UnlimitedExpirationTicks = -1;

    /// <summary>Validates the configured lifetime cap. Zero removes the cap.</summary>
    /// <param name="maximumExpirationMinutes">The nonnegative maximum lifetime in minutes.</param>
    /// <exception cref="ArgumentOutOfRangeException">The maximum is negative.</exception>
    public static void ValidateMaximumExpirationMinutes(int maximumExpirationMinutes)
    {
        if (maximumExpirationMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumExpirationMinutes));
        }
    }

    /// <summary>
    /// Calculates an absolute UTC deadline using the earlier explicit end or positive lifetime cap.
    /// Cap addition saturates at the largest representable UTC instant.
    /// </summary>
    /// <param name="utcNow">The acceptance instant, normalized to UTC for arithmetic.</param>
    /// <param name="maximumExpirationMinutes">The lifetime cap in minutes; zero is uncapped.</param>
    /// <param name="explicitEnd">The optional client end, compared as a UTC instant.</param>
    /// <returns>UTC instant ticks, or <see cref="UnlimitedExpirationTicks"/> when neither limit exists.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The maximum is negative.</exception>
    public static long CalculateExpirationTicks(
        DateTimeOffset utcNow,
        int maximumExpirationMinutes,
        DateTimeOffset? explicitEnd = null)
    {
        ValidateMaximumExpirationMinutes(maximumExpirationMinutes);

        if (maximumExpirationMinutes == 0)
        {
            return explicitEnd?.UtcTicks ?? UnlimitedExpirationTicks;
        }

        long maximumTicks = (long)maximumExpirationMinutes * TimeSpan.TicksPerMinute;
        long remainingTicks = DateTimeOffset.MaxValue.UtcTicks - utcNow.UtcTicks;
        long cappedTicks = maximumTicks >= remainingTicks
            ? DateTimeOffset.MaxValue.UtcTicks
            : utcNow.UtcTicks + maximumTicks;

        return explicitEnd is null ? cappedTicks : Math.Min(explicitEnd.Value.UtcTicks, cappedTicks);
    }

    /// <summary>Tests whether ticks represent a finite UTC instant or the sole unlimited sentinel.</summary>
    /// <param name="expirationTicks">The absolute UTC expiration ticks or unlimited sentinel.</param>
    /// <returns>True for -1 or a representable nonnegative UTC instant.</returns>
    public static bool IsValidExpirationTicks(long expirationTicks) =>
        expirationTicks == UnlimitedExpirationTicks ||
        (expirationTicks >= 0 && expirationTicks <= DateTimeOffset.MaxValue.UtcTicks);

    /// <summary>Rejects expiration values that are neither finite UTC instants nor unlimited.</summary>
    /// <param name="expirationTicks">The absolute UTC expiration ticks or unlimited sentinel.</param>
    /// <exception cref="ArgumentOutOfRangeException">The ticks are invalid.</exception>
    public static void ValidateExpirationTicks(long expirationTicks)
    {
        if (!IsValidExpirationTicks(expirationTicks))
        {
            throw new ArgumentOutOfRangeException(nameof(expirationTicks));
        }
    }

    /// <summary>Safely converts an effective deadline to an offset-zero end, omitting unlimited.</summary>
    /// <param name="expirationTicks">The absolute UTC expiration ticks or unlimited sentinel.</param>
    /// <returns>The finite end at offset zero, or null for unlimited.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The ticks are invalid.</exception>
    public static DateTimeOffset? GetExpiration(long expirationTicks)
    {
        ValidateExpirationTicks(expirationTicks);
        return expirationTicks == UnlimitedExpirationTicks
            ? null
            : new DateTimeOffset(expirationTicks, TimeSpan.Zero);
    }

    /// <summary>Tests whether a finite deadline has been reached, including equality.</summary>
    /// <param name="expirationTicks">The absolute UTC expiration ticks or unlimited sentinel.</param>
    /// <param name="utcNow">The observation instant, compared in UTC.</param>
    /// <returns>True when the finite deadline is at or before the observation instant.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The ticks are invalid.</exception>
    public static bool IsExpired(long expirationTicks, DateTimeOffset utcNow)
    {
        ValidateExpirationTicks(expirationTicks);
        return expirationTicks != UnlimitedExpirationTicks && utcNow.UtcTicks >= expirationTicks;
    }

    /// <summary>Tests event-generation eligibility without mutating subscription state.</summary>
    /// <param name="status">The exact FHIR status literal.</param>
    /// <param name="expirationTicks">The absolute UTC expiration ticks or unlimited sentinel.</param>
    /// <param name="utcNow">The observation instant, compared in UTC.</param>
    /// <returns>True only for active, unexpired subscriptions.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The ticks are invalid.</exception>
    public static bool CanGenerateEvents(string? status, long expirationTicks, DateTimeOffset utcNow)
    {
        bool expired = IsExpired(expirationTicks, utcNow);
        return !expired && string.Equals(status, "active", StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests delivery eligibility: requested for handshakes, active for heartbeats/events.
    /// Query inspection is not delivery and is not admitted by this function.
    /// </summary>
    /// <param name="status">The exact FHIR status literal.</param>
    /// <param name="expirationTicks">The absolute UTC expiration ticks or unlimited sentinel.</param>
    /// <param name="notificationType">The kind of delivery to begin.</param>
    /// <param name="utcNow">The observation instant, compared in UTC.</param>
    /// <returns>True only for an unexpired subscription with the required delivery status.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The ticks are invalid.</exception>
    public static bool CanDeliverNotification(
        string? status,
        long expirationTicks,
        ParsedSubscription.NotificationTypeCodes notificationType,
        DateTimeOffset utcNow)
    {
        if (IsExpired(expirationTicks, utcNow))
        {
            return false;
        }

        return notificationType switch
        {
            ParsedSubscription.NotificationTypeCodes.Handshake =>
                string.Equals(status, "requested", StringComparison.Ordinal),
            ParsedSubscription.NotificationTypeCodes.Heartbeat or
            ParsedSubscription.NotificationTypeCodes.EventNotification =>
                string.Equals(status, "active", StringComparison.Ordinal),
            _ => false,
        };
    }
}

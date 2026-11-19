// <copyright file="SubscriptionNotificationOutcome.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Models;

/// <summary>A bounded transport operation's result, committed only to its admitted lifecycle.</summary>
/// <param name="Kind">The operation result.</param>
/// <param name="SerializedContent">Optional notification contents for delivery diagnostics.</param>
/// <param name="Error">Optional failure details.</param>
public readonly record struct SubscriptionNotificationOutcome(
    SubscriptionNotificationOutcome.OutcomeKind Kind,
    string? SerializedContent = null,
    string? Error = null)
{
    /// <summary>The distinction between setup, delivery, simulation, failure, and stale work.</summary>
    public enum OutcomeKind
    {
        /// <summary>A setup operation completed without delivering a notification.</summary>
        SetupComplete,
        /// <summary>An actual transport delivery completed successfully.</summary>
        Delivered,
        /// <summary>The existing reserved-domain shortcut succeeded without transport I/O.</summary>
        Simulated,
        /// <summary>Preparation or transport failed for the admitted revision.</summary>
        Failed,
        /// <summary>The captured lifecycle is no longer eligible.</summary>
        Suppressed,
    }

    /// <summary>Gets whether delivery or the existing reserved-domain simulation succeeded.</summary>
    public bool Succeeded => Kind is OutcomeKind.Delivered or OutcomeKind.Simulated;
}

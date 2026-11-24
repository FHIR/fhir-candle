// <copyright file="SubscriptionChangedEventArgs.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Models;

/// <summary>Additional information for subscription changed events.</summary>
public class SubscriptionChangedEventArgs
{
    /// <summary>Gets or initializes the tenant.</summary>
    public required TenantConfiguration Tenant { get; init; }

    /// <summary>Gets or initializes the subscription.</summary>
    public ParsedSubscription? ChangedSubscription { get; init; } = null;

    /// <summary>Gets the lifecycle revision captured before publication.</summary>
    public long Revision { get; init; }

    /// <summary>Gets or initializes the identifier of the removed subscription.</summary>
    public string? RemovedSubscriptionId { get; init; } = string.Empty;

    /// <summary>Gets or initializes a value indicating whether the send handshake.</summary>
    public bool SendHandshake { get; init; } = false;
}

// <copyright file="SubscriptionSendEventArgs.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Models;

/// <summary>Additional information for subscription events.</summary>
public class SubscriptionSendEventArgs : EventArgs
{
    /// <summary>Gets the tenant.</summary>
    public required TenantConfiguration Tenant { get; init; }

    /// <summary>Gets the parsed subscription.</summary>
    public required ParsedSubscription Subscription { get; init; }

    /// <summary>Gets the lifecycle revision captured when this work was published.</summary>
    public long Revision { get; init; }

    /// <summary>Gets or initializes the notification events.</summary>
    public IEnumerable<SubscriptionEvent> NotificationEvents { get; init; } = Array.Empty<SubscriptionEvent>();

    /// <summary>Gets or initializes the type of the notification.</summary>
    public ParsedSubscription.NotificationTypeCodes NotificationType { get; init; } = ParsedSubscription.NotificationTypeCodes.EventNotification;
}

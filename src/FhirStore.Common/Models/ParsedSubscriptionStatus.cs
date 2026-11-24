// <copyright file="ParsedSubscriptionStatus.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Models;

/// <summary>A parsed subscription status.</summary>
public class ParsedSubscriptionStatus
{
    /*
  - id[0..1]: id *simple*
  - meta[0..1]: Meta
  - implicitRules[0..1]: uri
  - language[0..1]: code
  - text[0..1]: Narrative conditions: dom-6
  - contained[0..*]: Resource conditions: dom-2, dom-3, dom-4, dom-5
  - extension[0..*]: Extension
  - modifierExtension[0..*]: Extension
  - status[0..1]: code conditions: sst-2 (W5: FiveWs.status)
    {requested|active|error|off|entered-in-error}
  - type[1..1]: code conditions: sst-1, sst-2 (W5: FiveWs.what[x])
    {handshake|heartbeat|event-notification|query-status|query-event}
  - eventsSinceSubscriptionStart[0..1]: integer64
  - notificationEvent[0..*]: BackboneElement conditions: sst-1
    - id[0..1]: string *simple* conditions: ele-1
    - extension[0..*]: Extension
    - modifierExtension[0..*]: Extension
    - eventNumber[1..1]: integer64
    - timestamp[0..1]: instant
    - focus[0..1]: Reference(Resource)
    - additionalContext[0..*]: Reference(Resource)
  - subscription[1..1]: Reference(Subscription) (W5: FiveWs.why[x])
  - topic[0..1]: canonical(SubscriptionTopic)
  - error[0..*]: CodeableConcept
 */
    
    /// <summary>A parsed notification event.</summary>
    public class ParsedNotificationEvent
    {
        /// <summary>Gets or initializes the identifier.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets or initializes the event number.</summary>
        public required long? EventNumber { get; init; }

        /// <summary>Gets or initializes the timestamp.</summary>
        public DateTimeOffset? Timestamp { get; init; } = null;

        /// <summary>Gets or initializes the focus reference.</summary>
        public string FocusReference { get; init; } = string.Empty;

        /// <summary>Gets or initializes the additional context references.</summary>
        public IEnumerable<string>? AdditionalContextReferences { get; init; } = Array.Empty<string>();
    }

    /// <summary>Gets or initializes the identifier of the local bundle.</summary>
    public required string BundleId { get; init; }

    /// <summary>Gets or initializes the processed date time.</summary>
    public DateTimeOffset ProcessedDateTime { get; init; } = DateTimeOffset.Now;

    /// <summary>Gets or initializes the subscription reference.</summary>
    public required string SubscriptionReference { get; init; }

    /// <summary>Gets or initializes the subscription topic canonical.</summary>
    public required string SubscriptionTopicCanonical { get; init; }

    /// <summary>Gets or initializes the status.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets or initializes the type of the notification.</summary>
    public required ParsedSubscription.NotificationTypeCodes? NotificationType { get; init; }

    /// <summary>Gets or initializes the events since subscription start.</summary>
    public long? EventsSinceSubscriptionStart { get; init; } = null;

    /// <summary>Gets or initializes the notification events.</summary>
    public IEnumerable<ParsedNotificationEvent> NotificationEvents { get; init; } = Array.Empty<ParsedNotificationEvent>();

    /// <summary>Gets or initializes the errors.</summary>
    public IEnumerable<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>Copies a receipt and freezes its nested sequences before publication.</summary>
    public ParsedSubscriptionStatus DetachedCopy(DateTimeOffset? processedDateTime = null) => new()
    {
        BundleId = BundleId,
        ProcessedDateTime = processedDateTime ?? ProcessedDateTime,
        SubscriptionReference = SubscriptionReference,
        SubscriptionTopicCanonical = SubscriptionTopicCanonical,
        Status = Status,
        NotificationType = NotificationType,
        EventsSinceSubscriptionStart = EventsSinceSubscriptionStart,
        NotificationEvents = Array.AsReadOnly(NotificationEvents.Select(e => new ParsedNotificationEvent
        {
            Id = e.Id,
            EventNumber = e.EventNumber,
            Timestamp = e.Timestamp,
            FocusReference = e.FocusReference,
            AdditionalContextReferences = e.AdditionalContextReferences is null
                ? null : Array.AsReadOnly(e.AdditionalContextReferences.ToArray()),
        }).ToArray()),
        Errors = Array.AsReadOnly(Errors.ToArray()),
    };
}
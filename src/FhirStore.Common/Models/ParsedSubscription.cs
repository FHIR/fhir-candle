// <copyright file="CommonSubscription.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.ComponentModel;
using FhirCandle.Extensions;

namespace FhirCandle.Models;

/// <summary>A common subscription.</summary>
public class ParsedSubscription
{
    private long _currentEventCount = 0;
    private Dictionary<long, SubscriptionEvent> _generatedEvents = new();
    private Dictionary<long, string> _serializedEvents = new();
    private List<string> _notificationErrors = new();

    public enum NotificationTypeCodes
    {
        /// <summary>
        /// The status was generated as part of the setup or verification of a communications channel.
        /// (system: http://hl7.org/fhir/subscription-notification-type)
        /// </summary>
        [FhirLiteral("handshake")]
        Handshake,

        /// <summary>
        /// The status was generated to perform a heartbeat notification to the subscriber.
        /// (system: http://hl7.org/fhir/subscription-notification-type)
        /// </summary>
        [FhirLiteral("heartbeat")]
        Heartbeat,

        /// <summary>
        /// The status was generated for an event to the subscriber.
        /// (system: http://hl7.org/fhir/subscription-notification-type)
        /// </summary>
        [FhirLiteral("event-notification")]
        EventNotification,

        /// <summary>
        /// The status was generated in response to a status query/request.
        /// (system: http://hl7.org/fhir/subscription-notification-type)
        /// </summary>
        [FhirLiteral("query-status")]
        QueryStatus,

        /// <summary>
        /// The status was generated in response to an event query/request.
        /// (system: http://hl7.org/fhir/subscription-notification-type)
        /// </summary>
        [FhirLiteral("query-event")]
        QueryEvent,
    }

    /// <summary>An allowed filter.</summary>
    /// <param name="ResourceType">Type of the resource.</param>
    /// <param name="Name">        The name of the filter parameter.</param>
    /// <param name="Comparator">  The comparator.</param>
    /// <param name="Modifier">    The modifier.</param>
    /// <param name="Value">       The value.</param>
    public readonly record struct SubscriptionFilter(
        string ResourceType,
        string Name,
        string Comparator,
        string Modifier,
        string Value);

    /// <summary>Gets or initializes the identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets or initializes URL of the topic.</summary>
    public required string TopicUrl { get; init; }

    /// <summary>
    /// Tags on this subscription
    /// </summary>
    public HashSet<string> Tags { get; set; } = [];

    /// <summary>Gets or initializes the expressed filters by resource type.</summary>
    public Dictionary<string, List<SubscriptionFilter>> Filters { get; set; } = [];

    /// <summary>Gets or initializes the channel system.</summary>
    public string ChannelSystem { get; set; } = string.Empty;

    /// <summary>Gets or initializes the channel code.</summary>
    public string ChannelCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the endpoint.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets options for controlling the subscription.</summary>
    public Dictionary<string, List<string>> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the heartbeat seconds.</summary>
    public int? HeartbeatSeconds { get; set; } = null;

    /// <summary>Gets or sets the timeout seconds.</summary>
    public int? TimeoutSeconds { get; set; } = null;

    /// <summary>Gets or sets the type of the content.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Gets or sets the content level.</summary>
    public string ContentLevel { get; set; } = string.Empty;

    /// <summary>Gets or sets the maximum events per notification.</summary>
    public int? MaxEventsPerNotification { get; set; } = null;

    /// <summary>Gets or sets the current status of the subscription.</summary>
    public string CurrentStatus { get; set; } = "active";

    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the system tick (time) when the last communication was sent.</summary>
    public long LastCommunicationTicks { get; set; } = 0;

    /// <summary>Gets or sets the expiration ticks.</summary>
    public required long ExpirationTicks { get; set; }

    /// <summary>Gets or sets the number of current events.</summary>
    public long CurrentEventCount => _currentEventCount;

    /// <summary>Increment event count.</summary>
    /// <returns>A long.</returns>
    public long IncrementEventCount()
    {
        return Interlocked.Increment(ref _currentEventCount);
    }

    /// <summary>Gets or sets the generated events.</summary>
    public Dictionary<long, SubscriptionEvent> GeneratedEvents => _generatedEvents;

    /// <summary>Gets the serialized events.</summary>
    public Dictionary<long, string> SerializedEvents => _serializedEvents;

    /// <summary>Registers the event described by subscriptionEvent.</summary>
    /// <param name="subscriptionEvent">The subscription event.</param>
    public void RegisterEvent(SubscriptionEvent subscriptionEvent)
    {
        if (_generatedEvents.ContainsKey(subscriptionEvent.EventNumber))
        {
            // TODO: for now just overwrite, figure out what we want to do later
            _generatedEvents[subscriptionEvent.EventNumber] = subscriptionEvent;
            return;
        }

        _generatedEvents.Add(subscriptionEvent.EventNumber, subscriptionEvent);
    }

    /// <summary>Registers the serialized send.</summary>
    /// <param name="eventNumber">The event number.</param>
    /// <param name="content">    The content.</param>
    public void RegisterSerializedSend(long eventNumber, string content)
    {
        System.Console.WriteLine($"Sending content for event: Subscription/{Id}:{eventNumber}: \n-----\n{content}\n-----");

        if (_serializedEvents.ContainsKey(eventNumber))
        {
            // TODO: for now just overwrite, figure out what we want to do later
            _serializedEvents[eventNumber] = content;
            return;
        }

        _serializedEvents.Add(eventNumber, content);
    }

    /// <summary>Clears the events.</summary>
    public void ClearEvents()
    {
        _generatedEvents.Clear();
    }

    /// <summary>Gets or sets the notification errors.</summary>
    public List<string> NotificationErrors => _notificationErrors;

    /// <summary>Registers the error described by error.</summary>
    /// <param name="error">The error.</param>
    public void RegisterError(string error)
    {
        _notificationErrors.Add(error);
        Console.WriteLine($" <<< Subscription/{Id}: {error}");
    }

    /// <summary>Clears the errors.</summary>
    public void ClearErrors()
    {
        _notificationErrors.Clear();
    }
}

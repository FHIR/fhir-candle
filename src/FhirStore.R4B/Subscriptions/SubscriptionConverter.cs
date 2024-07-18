// <copyright file="SubscriptionConverter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Extensions;
using FhirCandle.Models;
using Hl7.Fhir.Model;
using static Hl7.Fhir.Model.SearchParameter;

namespace FhirCandle.Subscriptions;

/// <summary>A FHIR R4B subscription format converter.</summary>
public class SubscriptionConverter
{
    /// <summary>(Immutable) URL of the filter criteria.</summary>
    private const string _filterCriteriaUrl = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-filter-criteria";

    /// <summary>(Immutable) URL of the heartbeat period.</summary>
    private const string _heartbeatPeriodUrl = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-heartbeat-period";

    /// <summary>(Immutable) URL of the timeout.</summary>
    private const string _timeoutUrl = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-timeout";

    /// <summary>(Immutable) URL of the maximum count.</summary>
    private const string _maxCountUrl = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-max-count";

    /// <summary>(Immutable) URL of the channel type.</summary>
    private const string _channelTypeUrl = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-channel-type";

    /// <summary>(Immutable) URL of the content.</summary>
    private const string _contentUrl = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-payload-content";

    /// <summary>The active code.</summary>
    public static SubscriptionStatusCodes ActiveCode = SubscriptionStatusCodes.Active;
    /// <summary>The off code.</summary>
    public static SubscriptionStatusCodes OffCode = SubscriptionStatusCodes.Off;

    /// <summary>URL of the payload content value set.</summary>
    public static string PayloadContentVsUrl = "http://hl7.org/fhir/uv/subscriptions-backport/ValueSet/backport-content-value-set";

    /// <summary>(Immutable) The maximum subscription ticks.</summary>
    private readonly long _maxSubscriptionTicks;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionConverter"/> class.
    /// </summary>
    /// <param name="MaxSubscriptionExpirationMinutes">The maximum subscription expiration in
    ///  minutes.</param>
    public SubscriptionConverter(int MaxSubscriptionExpirationMinutes)
    {
        _maxSubscriptionTicks = TimeSpan.FromMinutes(MaxSubscriptionExpirationMinutes).Ticks;
    }

    /// <summary>Attempts to parse a ParsedSubscription from the given object.</summary>
    /// <param name="subscription">The subscription.</param>
    /// <param name="common">      [out] The common.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParse(object subscription, out ParsedSubscription common)
    {
        if ((subscription == null) ||
            (subscription is not Hl7.Fhir.Model.Subscription sub) ||
            string.IsNullOrEmpty(sub.Id) ||
            string.IsNullOrEmpty(sub.Criteria) ||
            (!sub.Criteria.StartsWith("http", StringComparison.Ordinal)))
        {
            common = null!;
            return false;
        }

        long expirationTicks = sub.End?.Ticks ?? _maxSubscriptionTicks;

        if (expirationTicks > _maxSubscriptionTicks)
        {
            expirationTicks = _maxSubscriptionTicks;
        }

        common = new()
        {
            Id = sub.Id,
            TopicUrl = sub.Criteria,
            ChannelSystem = string.Empty,
            ChannelCode = sub.Channel.Type == null
                ? string.Empty
                : Hl7.Fhir.Utility.EnumUtility.GetLiteral(sub.Channel.Type)!,
            Endpoint = sub.Channel.Endpoint ?? string.Empty,
            ContentType = sub.Channel.Payload?.ToString() ?? string.Empty,
            ExpirationTicks = expirationTicks,
        };

        // check for extended information
        IEnumerable<Hl7.Fhir.Model.Extension>? extensions;

        extensions = sub.Channel.Extension?.Where(e => e.Url == _heartbeatPeriodUrl);
        if (extensions?.Any() ?? false)
        {
            if (int.TryParse(extensions.First().Value?.ToString(), out int heartbeat))
            {
                common.HeartbeatSeconds = heartbeat;
            }
        }

        extensions = sub.Channel.Extension?.Where(e => e.Url == _timeoutUrl);
        if (extensions?.Any() ?? false)
        {
            if (int.TryParse(extensions.First().Value?.ToString(), out int timeout))
            {
                common.TimeoutSeconds = timeout;
            }
        }

        extensions = sub.Channel.Extension?.Where(e => e.Url == _maxCountUrl);
        if (extensions?.Any() ?? false)
        {
            if (int.TryParse(extensions.First().Value?.ToString(), out int maxCount))
            {
                common.MaxEventsPerNotification = maxCount;
            }
        }

        extensions = sub.Channel.TypeElement?.Extension?.Where(e => e.Url == _channelTypeUrl);
        if (extensions?.Any() ?? false)
        {
            if (extensions.First().Value is Hl7.Fhir.Model.Coding c)
            {
                common.ChannelSystem = c.System;
                common.ChannelCode = c.Code;
            }
        }

        extensions = sub.Channel.PayloadElement?.Extension?.Where(e => e.Url == _contentUrl);
        if (extensions?.Any() ?? false)
        {
            common.ContentLevel = extensions.First().Value?.ToString() ?? string.Empty;
        }

        // add parameters
        if (sub.Channel.Header?.Any() ?? false)
        {
            foreach (string header in sub.Channel.Header)
            {
                int index = header.IndexOf(':');
                if (index == -1)
                {
                    index = header.IndexOf('=');
                }

                if (index == -1)
                {
                    continue;
                }

                string key = header.Substring(0, index).Trim();
                string value = header.Substring(index + 1).Trim();

                if (!common.Parameters.ContainsKey(key))
                {
                    common.Parameters.Add(key, new());
                }

                common.Parameters[key].Add(value.ToString());
            }

        }

        // add filters
        extensions = sub.CriteriaElement?.Extension?.Where(e => e.Url == _filterCriteriaUrl) ?? null;
        if (extensions?.Any() ?? false)
        {
            foreach (string criteria in extensions.Select(e => e.Value?.ToString() ?? string.Empty))
            {
                if (string.IsNullOrEmpty(criteria))
                {
                    continue;
                }

                string key;
                string resourceType;
                string value;

                int index = criteria.IndexOf('?');
                if (index == -1)
                {
                    key = "-";
                    resourceType = string.Empty;
                    value = criteria;
                }
                else
                {
                    key = criteria.Substring(0, index);
                    resourceType = key;
                    value = criteria.Substring(index + 1);
                }

                if (!common.Filters.ContainsKey(key))
                {
                    common.Filters.Add(key, new());
                }

                string[] queryParams = value.Split('&');
                
                foreach (string queryParam in queryParams)
                {
                    string[] components = queryParam.Split('=');

                    if (components.Length != 2)
                    {
                        continue;
                    }

                    string[] keyComponents = components[0].Split(":");

                    common.Filters[key].Add(new(
                        resourceType,
                        keyComponents[0],
                        string.Empty,                               // TODO: figure out prefix based on type info we don't have here
                        (keyComponents.Length > 1) ? keyComponents[1] : string.Empty,
                        components[1]));
                }
            }
        }

        return true;
    }

    /// <summary>Attempts to parse a ParsedSubscription from the given object.</summary>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParse(ParsedSubscription common, out Subscription subscription)
    {
        if ((common == null) ||
            string.IsNullOrEmpty(common.Id) ||
            string.IsNullOrEmpty(common.TopicUrl))
        {
            subscription = null!;
            return false;
        }

        Subscription.SubscriptionChannelType? ct;
        string extendedCt;

        try
        {
            ct = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<Subscription.SubscriptionChannelType>(common.ChannelCode);
            extendedCt = string.Empty;
        }
        catch (Exception)
        {
            ct = Subscription.SubscriptionChannelType.RestHook;
            extendedCt = common.ChannelCode;
        }

        SubscriptionStatusCodes? status;

        try
        {
            status = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<SubscriptionStatusCodes>(common.CurrentStatus);
        }
        catch (Exception)
        {
            status = SubscriptionStatusCodes.Requested;
        }

        subscription = new()
        {
            Id = common.Id,
            Status = status!,
            Reason = string.IsNullOrEmpty(common.Reason) ? "Required" : common.Reason,
            Criteria = common.TopicUrl,
            Channel = new()
            {
                Type = ct,
                Endpoint = common.Endpoint,
                Payload = common.ContentType,
            },
            End = new DateTimeOffset(common.ExpirationTicks, TimeSpan.Zero),
        };

        if (common.HeartbeatSeconds != null)
        {
            subscription.Channel.AddExtension(_heartbeatPeriodUrl, new Integer(common.HeartbeatSeconds));
        }

        if (common.TimeoutSeconds != null)
        {
            subscription.Channel.AddExtension(_timeoutUrl, new Integer(common.TimeoutSeconds));
        }

        if (common.MaxEventsPerNotification != null)
        {
            subscription.Channel.AddExtension(_maxCountUrl, new Integer(common.MaxEventsPerNotification));
        }

        if (!string.IsNullOrEmpty(extendedCt))
        {
            subscription.Channel.AddExtension(_channelTypeUrl, new FhirString(extendedCt));
        }

        subscription.Channel.PayloadElement.AddExtension(_contentUrl, new Code(common.ContentLevel));

        // add parameters
        if (common.Parameters.Any())
        {
            List<string> headers = new();

            foreach ((string key, List<string> values) in common.Parameters)
            {
                headers.AddRange(values.Select(v => key + "=" + v));
            }

            subscription.Channel.Header = headers;
        }

        // add filters
        if (common.Filters.Any())
        {
            foreach (List<ParsedSubscription.SubscriptionFilter> filters in common.Filters.Values)
            {
                foreach (ParsedSubscription.SubscriptionFilter f in filters)
                {
                    string mod = string.IsNullOrEmpty(f.Modifier) ? string.Empty : ":" + f.Modifier;
                    string pre = f.Comparator ?? string.Empty;

                    subscription.CriteriaElement.AddExtension(
                        _filterCriteriaUrl,
                        new FhirString($"{f.ResourceType}?{f.Name}{mod}={pre}{f.Value}"));
                }
            }
        }

        return true;
    }

    /// <summary>Attempts to parse a ParsedSubscriptionStatus from the given object.</summary>
    /// <param name="subscriptionStatus">The subscription.</param>
    /// <param name="bundleId">          Identifier for the bundle.</param>
    /// <param name="common">            [out] The common.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParse(object subscriptionStatus, string bundleId, out ParsedSubscriptionStatus common)
    {
        if ((subscriptionStatus == null) ||
            (subscriptionStatus is not Hl7.Fhir.Model.SubscriptionStatus status))
        {
            common = null!;
            return false;
        }

        List<string> errors = new();
        if (status.Error.Any())
        {
            foreach (CodeableConcept error in status.Error)
            {
                if (!(error.Coding?.Any() ?? false))
                {
                    continue;
                }

                foreach (Coding coding in error.Coding)
                {
                    errors.Add(coding.DebuggerDisplay);
                }
            }
        }

        List<ParsedSubscriptionStatus.ParsedNotificationEvent> notificationEvents = new();
        if (status.NotificationEvent?.Any() ?? false)
        {
            foreach (Hl7.Fhir.Model.SubscriptionStatus.NotificationEventComponent notificationEvent in status.NotificationEvent)
            {
                notificationEvents.Add(new()
                {
                    Id = notificationEvent.ElementId ?? string.Empty,
                    EventNumber = long.TryParse(notificationEvent.EventNumber, out long en)
                        ? en
                        : null,
                    Timestamp = notificationEvent.Timestamp,
                    FocusReference = notificationEvent.Focus?.Reference ?? string.Empty,
                    AdditionalContextReferences = notificationEvent.AdditionalContext?.Select(ac => ac.Reference) ?? Array.Empty<string>(),
                });
            }
        }

        common = new()
        {
            BundleId = bundleId,
            SubscriptionReference = status.Subscription?.Reference ?? string.Empty,
            SubscriptionTopicCanonical = status.Topic ?? string.Empty,
            Status = 
                status.Status != null
                ? Hl7.Fhir.Utility.EnumUtility.GetLiteral(status.Status)!
                : string.Empty,
            NotificationType =
                status.Type != null
                ? Hl7.Fhir.Utility.EnumUtility.GetLiteral(status.Type)!.ToFhirEnum<ParsedSubscription.NotificationTypeCodes>()
                : null,
            EventsSinceSubscriptionStart =
                long.TryParse(status.EventsSinceSubscriptionStart, out long count)
                ? count
                : null,
            NotificationEvents = notificationEvents.ToArray(),
            Errors = errors.ToArray(),
        };

        return true;
    }

    /// <summary>Status for subscription.</summary>
    /// <param name="subscription">    The subscription.</param>
    /// <param name="notificationType">Type of the notification.</param>
    /// <param name="baseUrl">         URL of the base.</param>
    /// <returns>A Hl7.Fhir.Model.Resource.</returns>
    public Hl7.Fhir.Model.Resource StatusForSubscription(
        ParsedSubscription subscription,
        string notificationType,
        /// <summary>Gets the base url)</summary>
        string baseUrl)
    {
        if (!Enum.TryParse(subscription.CurrentStatus, out SubscriptionStatusCodes statusCode))
        {
            statusCode = SubscriptionStatusCodes.Active;
        }

        return new Hl7.Fhir.Model.SubscriptionStatus()
        {
            Id = Guid.NewGuid().ToString(),
            Subscription = new ResourceReference(baseUrl + "/Subscription/" + subscription.Id),
            Topic = subscription.TopicUrl,
            EventsSinceSubscriptionStart = subscription.CurrentEventCount.ToString(),
            Status = statusCode,
            Type = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<SubscriptionStatus.SubscriptionNotificationType>(notificationType),
        };
    }

    /// Build a bundle of subscription events into the desired format and content level.
    /// <param name="subscription">    The subscription.</param>
    /// <param name="eventNumbers">    The event numbers.</param>
    /// <param name="notificationType">Type of the notification.</param>
    /// <param name="baseUrl">         URL of the base.</param>
    /// <param name="contentType">     (Optional) Type of the content.</param>
    /// <param name="contentLevel">    (Optional) The content level.</param>
    /// <returns>A string.</returns>
    public Bundle? BundleForSubscriptionEvents(
        ParsedSubscription subscription,
        IEnumerable<long> eventNumbers,
        string notificationType,
        string baseUrl,
        /// <summary>The content level.</summary>
        string contentLevel = "")
    {
        if (string.IsNullOrEmpty(contentLevel))
        {
            contentLevel = subscription.ContentLevel;
        }

        // create our notification bundle
        Bundle bundle = new()
        {
            Id = Guid.NewGuid().ToString(),
            Type = Bundle.BundleType.History,
            Timestamp = DateTimeOffset.Now,
            Entry = new(),
        };

        if (!Enum.TryParse(subscription.CurrentStatus, out SubscriptionStatusCodes statusCode))
        {
            statusCode = SubscriptionStatusCodes.Active;
        }

        // create our status resource
        SubscriptionStatus status = (SubscriptionStatus)StatusForSubscription(subscription, notificationType, baseUrl);

        // add a status placeholder to the bundle
        bundle.AddResourceEntry(status, $"urn:uuid:{status.Id}");

        HashSet<string> addedResources = new();

        bool isEmpty = contentLevel.Equals("empty", StringComparison.Ordinal);
        bool isFullResource = contentLevel.Equals("full-resource", StringComparison.Ordinal);

        // determine behavior of no event numbers
        if (!eventNumbers.Any())
        {
            // query-event should send all
            if (notificationType.Equals("query-event"))
            {
                eventNumbers = subscription.GeneratedEvents.Keys;
            }
            // others send the most recent if there is one
            else if (subscription.GeneratedEvents.Any())
            {
                eventNumbers = new long[] { subscription.GeneratedEvents.Keys.Last() };
            }
            else
            {
                eventNumbers = Array.Empty<long>();
            }
        }

        // iterate over the events
        foreach (long eventNumber in eventNumbers)
        {
            if (!subscription.GeneratedEvents.ContainsKey(eventNumber))
            {
                continue;
            }

            SubscriptionEvent se = subscription.GeneratedEvents[eventNumber];

            // check for empty notifications
            if (isEmpty)
            {
                // add just this event number to our status
                status.NotificationEvent.Add(new()
                {
                    EventNumber = eventNumber.ToString(),
                    Timestamp = se.Timestamp,
                });

                // empty notifications do not contain bundle entries
                continue;
            }

            Resource r = (Resource)se.Focus;
            string relativeUrl = $"{r.TypeName}/{r.Id}";

            // add this event to our status
            status.NotificationEvent.Add(new()
            {
                EventNumber = eventNumber.ToString(),
                Focus = new ResourceReference(baseUrl + "/" + relativeUrl),
                AdditionalContext = (se.AdditionalContext?.Any() ?? false)
                    ? se.AdditionalContext.Select(o => new ResourceReference($"{baseUrl}/{((Resource)o).TypeName}/{((Resource)o).Id}")).ToList()
                    : new List<ResourceReference>(),
                Timestamp = se.Timestamp,
            });

            // add the focus to our bundle
            if (!addedResources.Contains(relativeUrl))
            {
                addedResources.Add(relativeUrl);

                if (isFullResource)
                {
                    bundle.Entry.Add(new Bundle.EntryComponent()
                    {
                        FullUrl = baseUrl + "/" + relativeUrl,
                        Resource = r,
                    });
                }
            }

            // add any additional context
            if (se.AdditionalContext?.Any() ?? false)
            {
                foreach (object ac in se.AdditionalContext)
                {
                    Resource acr = (Resource)ac;
                    string acrRelative = $"{acr.TypeName}/{acr.Id}";

                    if (!addedResources.Contains(acrRelative))
                    {
                        addedResources.Add(acrRelative);

                        if (isFullResource)
                        {
                            bundle.Entry.Add(new Bundle.EntryComponent()
                            {
                                FullUrl = baseUrl + "/" + acrRelative,
                                Resource = acr,
                            });
                        }
                    }
                }
            }
        }

        // update the status information in our bundle
        bundle.Entry[0].Resource = status;

        return bundle;
    }
}

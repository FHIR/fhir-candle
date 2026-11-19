// <copyright file="SubscriptionConverter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Extensions;
using FhirCandle.Models;
using Hl7.Fhir.Model;

namespace FhirCandle.Subscriptions;

/// <summary>A FHIR R5 subscription format converter.</summary>
public class SubscriptionConverter
{
    /// <summary>The active code.</summary>
    public static SubscriptionStatusCodes ActiveCode = SubscriptionStatusCodes.Active;
    /// <summary>The off code.</summary>
    public static SubscriptionStatusCodes OffCode = SubscriptionStatusCodes.Off;

    /// <summary>URL of the payload content value set.</summary>
    public static string PayloadContentVsUrl = "http://hl7.org/fhir/ValueSet/subscription-payload-content";

    /// <summary>(Immutable) The maximum subscription ticks.</summary>
    private readonly int _maximumExpirationMinutes;
    private readonly TimeProvider _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionConverter"/> class.
    /// </summary>
    /// <param name="MaxSubscriptionExpirationMinutes">The maximum subscription expiration in
    ///  minutes.</param>
    public SubscriptionConverter(int MaxSubscriptionExpirationMinutes)
        : this(MaxSubscriptionExpirationMinutes, TimeProvider.System)
    {
    }

    /// <summary>Creates a tenant-owned converter using an explicit UTC clock.</summary>
    /// <param name="maximumExpirationMinutes">The nonnegative cap; zero removes only the cap.</param>
    /// <param name="clock">The tenant's clock.</param>
    public SubscriptionConverter(int maximumExpirationMinutes, TimeProvider clock)
    {
        SubscriptionLifecyclePolicy.ValidateMaximumExpirationMinutes(maximumExpirationMinutes);
        _maximumExpirationMinutes = maximumExpirationMinutes;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Tests a native status literal before either representation is mutated.</summary>
    /// <param name="literal">The case-sensitive FHIR status literal.</param>
    /// <returns>Whether the SDK supports the literal.</returns>
    public bool IsValidStatus(string literal) =>
        Hl7.Fhir.Utility.EnumUtility.ParseLiteral<SubscriptionStatusCodes>(literal) is not null;

    /// <summary>Calculates the effective deadline at the accepted resource commit instant.</summary>
    /// <param name="subscription">The accepted native resource, retaining its original End.</param>
    /// <param name="utcNow">The commit-time UTC instant.</param>
    /// <returns>The effective UTC deadline ticks, or the unlimited sentinel.</returns>
    public long ExpirationFor(Subscription subscription, DateTimeOffset utcNow) =>
        SubscriptionLifecyclePolicy.CalculateExpirationTicks(utcNow, _maximumExpirationMinutes, subscription.End);

    private static bool HasValidStatus(Subscription subscription)
    {
        try
        {
            return subscription.Status is not null && Enum.IsDefined(subscription.Status.Value);
        }
        catch (Hl7.Fhir.Validation.CodedValidationException)
        {
            return false;
        }
    }
    public void UpdateResourceStatus(object subscription, string statusLiteral)
    {
        if ((subscription is null) ||
            (subscription is not Hl7.Fhir.Model.Subscription sub))
        {
            return;
        }

        SubscriptionStatusCodes? status = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<SubscriptionStatusCodes>(statusLiteral);
        if (status is null)
        {
            return;
        }

        sub.Status = status.Value;
    }

    /// <summary>Attempts to parse a ParsedSubscription from the given object.</summary>
    /// <param name="subscription">The subscription.</param>
    /// <param name="common">      [out] The common.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParse(object subscription, out ParsedSubscription common)
    {
        if ((subscription is null) ||
            (subscription is not Hl7.Fhir.Model.Subscription sub) ||
            string.IsNullOrEmpty(sub.Id) ||
            !HasValidStatus(sub) ||
            string.IsNullOrEmpty(sub.Topic) ||
            (!sub.Topic.StartsWith("http", StringComparison.Ordinal)))
        {
            common = null!;
            return false;
        }

        long expirationTicks = ExpirationFor(sub, _clock.GetUtcNow());

        HashSet<string> tags = (sub.Meta?.Tag?.Any() ?? false)
            ? tags = new(sub.Meta.Tag.Select(t => $"{t.System}|{t.Code}"))
            : [];

        common = new()
        {
            Id = sub.Id,
            TopicUrl = sub.Topic,
            Tags = tags,
            ChannelSystem = sub.ChannelType?.System ?? string.Empty,
            ChannelCode = sub.ChannelType?.Code ?? string.Empty,
            Endpoint = sub.Endpoint ?? string.Empty,
            HeartbeatSeconds = sub.HeartbeatPeriod ?? 0,
            TimeoutSeconds = sub.Timeout ?? 0,
            ContentType = sub.ContentType ?? string.Empty,
            ContentLevel =
                sub.Content is not null
                ? Hl7.Fhir.Utility.EnumUtility.GetLiteral(sub.Content)!
                : string.Empty,
            MaxEventsPerNotification = sub.MaxCount ?? 0,
            ExpirationTicks = expirationTicks,
            CurrentStatus = Hl7.Fhir.Utility.EnumUtility.GetLiteral(sub.Status)!,
        };

        // add parameters
        if (sub.Parameter?.Any() ?? false)
        {
            foreach (Hl7.Fhir.Model.Subscription.ParameterComponent param in sub.Parameter)
            {
                if (param.Name is null)
                {
                    continue;
                }

                if (!common.Parameters.ContainsKey(param.Name))
                {
                    common.Parameters.Add(param.Name, new());
                }

                common.Parameters[param.Name].Add(param.Value?.ToString() ?? string.Empty);
            }
        }

        // add filters
        if (sub.FilterBy?.Any() ?? false)
        {
            foreach (Hl7.Fhir.Model.Subscription.FilterByComponent filter in sub.FilterBy)
            {
                string key = string.IsNullOrEmpty(filter.ResourceType) ? "*" : filter.ResourceType;

                if (!common.Filters.ContainsKey(key))
                {
                    common.Filters.Add(key, new());
                }

                common.Filters[key].Add(new(
                    filter.ResourceType ?? string.Empty,
                    filter.FilterParameter ?? string.Empty,
                    filter.Comparator?.ToString() ?? string.Empty,
                    filter.Modifier?.ToString() ?? string.Empty,
                    filter.Value ?? string.Empty));
            }
        }

        return true;
    }

    /// <summary>Attempts to create a Subscription from a ParsedSubscription object.</summary>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParse(ParsedSubscription common, out Subscription subscription)
    {
        if ((common is null) ||
            string.IsNullOrEmpty(common.Id) ||
            string.IsNullOrEmpty(common.TopicUrl) ||
            !SubscriptionLifecyclePolicy.IsValidExpirationTicks(common.ExpirationTicks) ||
            !IsValidStatus(common.CurrentStatus))
        {
            subscription = null!;
            return false;
        }

        SubscriptionStatusCodes? status;

        try
        {
            status = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<SubscriptionStatusCodes>(common.CurrentStatus);
        }
        catch (Exception)
        {
            subscription = null!;
            return false;
        }

        subscription = new()
        {
            Id = common.Id,
            Topic = common.TopicUrl,
            Status = status!,
            Reason = string.IsNullOrEmpty(common.Reason) ? null : common.Reason,
            ChannelType = new Coding(common.ChannelSystem, common.ChannelCode),
            Endpoint = common.Endpoint,
            HeartbeatPeriod = common.HeartbeatSeconds,
            Timeout = common.TimeoutSeconds,
            ContentType = common.ContentType,
            Content = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<Subscription.SubscriptionPayloadContent>(common.ContentLevel),
            MaxCount = common.MaxEventsPerNotification,
            End = SubscriptionLifecyclePolicy.GetExpiration(common.ExpirationTicks),
        };

        // add tags
        if (common.Tags.Count != 0)
        {
            if (subscription.Meta is null)
            {
                subscription.Meta = new();
            }

            foreach (string tag in common.Tags)
            {
                string[] parts = tag.Split('|');
                subscription.Meta.Tag.Add(new Coding(parts[0], parts[1]));
            }
        }

        // add parameters
        if (common.Parameters.Count != 0)
        {
            subscription.Parameter = new();

            foreach ((string key, List<string> values) in common.Parameters)
            {
                subscription.Parameter.AddRange(values.Select(v => new Subscription.ParameterComponent()
                {
                    Name = key,
                    Value = v,
                }));
            }
        }

        // add filters
        if (common.Filters.Count != 0)
        {
            subscription.FilterBy = new();
            foreach (List<ParsedSubscription.SubscriptionFilter> filters in common.Filters.Values)
            {
                foreach (ParsedSubscription.SubscriptionFilter filter in filters)
                {
                    subscription.FilterBy.Add(new Subscription.FilterByComponent()
                    {
                        ResourceType = filter.ResourceType,
                        FilterParameter = filter.Name,
                        Comparator = string.IsNullOrEmpty(filter.Comparator)
                            ? null
                            : Hl7.Fhir.Utility.EnumUtility.ParseLiteral<SearchComparator>(common.ContentLevel),
                        Modifier = string.IsNullOrEmpty(filter.Modifier)
                            ? null
                            : Hl7.Fhir.Utility.EnumUtility.ParseLiteral<SearchModifierCode>(filter.Modifier),
                        Value = filter.Value,
                    });
                }
            }
        }

        return true;
    }

    /// <summary>Attempts to parse a ParsedSubscriptionStatus from the given object.</summary>
    /// <param name="subscriptionStatus">The subscription.</param>
    /// <param name="bundleId">  Identifier for the bundle.</param>
    /// <param name="common">            [out] The common.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParse(object subscriptionStatus, string bundleId, out ParsedSubscriptionStatus common)
    {
        if ((subscriptionStatus is null) ||
            (subscriptionStatus is not Hl7.Fhir.Model.SubscriptionStatus status))
        {
            common = null!;
            return false;
        }

        List<string> errors = new();
        if (status.Error?.Any() ?? false)
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
                    EventNumber = notificationEvent.EventNumber,
                    Timestamp = notificationEvent.Timestamp,
                    FocusReference = notificationEvent.Focus?.Reference ?? string.Empty,
                    AdditionalContextReferences = notificationEvent.AdditionalContext is null
                        ? Array.Empty<string>()
                        : notificationEvent.AdditionalContext
                            .Where(r => r?.Reference is not null)
                            .Select(ac => ac.Reference!)
                            .ToArray(),
                });
            }
        }

        common = new()
        {
            BundleId = bundleId,
            SubscriptionReference = status.Subscription?.Reference ?? string.Empty,
            SubscriptionTopicCanonical = status.Topic ?? string.Empty,
            Status =
                status.Status is not null
                ? Hl7.Fhir.Utility.EnumUtility.GetLiteral(status.Status)!
                : string.Empty,
            NotificationType =
                status.Type is not null
                ? Hl7.Fhir.Utility.EnumUtility.GetLiteral(status.Type)!.ToFhirEnum<ParsedSubscription.NotificationTypeCodes>()
                : null,
            EventsSinceSubscriptionStart = status.EventsSinceSubscriptionStart,
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
        string baseUrl)
    {
        SubscriptionStatusCodes? statusCode = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<SubscriptionStatusCodes>(subscription.CurrentStatus);

        return new Hl7.Fhir.Model.SubscriptionStatus()
        {
            Id = Guid.NewGuid().ToString(),
            Subscription = new ResourceReference(baseUrl + "/Subscription/" + subscription.Id),
            Topic = subscription.TopicUrl,
            EventsSinceSubscriptionStart = subscription.CurrentEventCount,
            Status = statusCode,
            Type = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<SubscriptionStatus.SubscriptionNotificationType>(notificationType),
        };
    }

    /// <summary>Bundle for subscription events.</summary>
    /// <param name="subscription">    The subscription.</param>
    /// <param name="eventNumbers">    The event numbers.</param>
    /// <param name="notificationType">Type of the notification.</param>
    /// <param name="baseUrl">         URL of the base.</param>
    /// <param name="contentLevel">    (Optional) The content level.</param>
    /// <param name="allowEmptySelectionFallback">
    /// (Optional) Whether an empty selection uses the legacy all-events or last-event default.
    /// </param>
    /// <returns>A Bundle?</returns>
    public Bundle? BundleForSubscriptionEvents(
        ParsedSubscription subscription,
        IEnumerable<long> eventNumbers,
        string notificationType,
        string baseUrl,
        /// <summary>The content level.</summary>
        string contentLevel = "",
        bool allowEmptySelectionFallback = true)
    {
        if (string.IsNullOrEmpty(contentLevel))
        {
            contentLevel = subscription.ContentLevel;
        }

        // create our notification bundle
        Bundle bundle = new()
        {
            Id = Guid.NewGuid().ToString(),
            Type = Bundle.BundleType.SubscriptionNotification,
            Timestamp = DateTimeOffset.Now,
            Entry = new(),
        };

        SubscriptionStatusCodes? statusCode = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<SubscriptionStatusCodes>(subscription.CurrentStatus);

        // create our status resource
        SubscriptionStatus status = (SubscriptionStatus)StatusForSubscription(subscription, notificationType, baseUrl);

        // add our status to the bundle
        bundle.AddResourceEntry(status, $"urn:uuid:{status.Id}");

        HashSet<string> addedResources = new();

        bool isEmpty = contentLevel.Equals("empty", StringComparison.Ordinal);
        bool isFullResource = contentLevel.Equals("full-resource", StringComparison.Ordinal);

        // determine behavior of no event numbers
        if (allowEmptySelectionFallback && !eventNumbers.Any())
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
                    EventNumber = eventNumber,
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
                EventNumber = eventNumber,
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

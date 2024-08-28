// <copyright file="OpSubscriptionEvents.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Extensions;
using FhirCandle.Models;
using FhirCandle.Subscriptions;
using FhirCandle.Versioned;
using Hl7.Fhir.Model;
using System.Net;

namespace FhirCandle.Operations;

/// <summary>The FHIR Subscription $events operation.</summary>
public class OpSubscriptionEvents : IFhirOperation
{
    /// <summary>Gets the name of the operation.</summary>
    public string OperationName => "$events";

    /// <summary>Gets the operation version.</summary>
    public string OperationVersion => "0.0.1";

    /// <summary>Gets the canonical by FHIR version.</summary>
    public Dictionary<FhirCandle.Utils.FhirReleases.FhirSequenceCodes, string> CanonicalByFhirVersion => new()
    {
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R4, "http://hl7.org/fhir/uv/subscriptions-backport/OperationDefinition/backport-subscription-events" },
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R4B, "http://hl7.org/fhir/uv/subscriptions-backport/OperationDefinition/backport-subscription-events" },
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R5, "http://hl7.org/fhir/OperationDefinition/Subscription-events" },
    };

    /// <summary>Gets a value indicating whether this operation is a named query.</summary>
    public bool IsNamedQuery => false;

    /// <summary>Gets a value indicating whether we allow get.</summary>
    public bool AllowGet => true;

    /// <summary>Gets a value indicating whether we allow post.</summary>
    public bool AllowPost => true;

    /// <summary>Gets a value indicating whether we allow system level.</summary>
    public bool AllowSystemLevel => false;

    /// <summary>Gets a value indicating whether we allow resource level.</summary>
    public bool AllowResourceLevel => false;

    /// <summary>Gets a value indicating whether we allow instance level.</summary>
    public bool AllowInstanceLevel => true;

    /// <summary>Gets a value indicating whether the accepts non FHIR.</summary>
    public bool AcceptsNonFhir => false;

    /// <summary>Gets a value indicating whether the returns non FHIR.</summary>
    public bool ReturnsNonFhir => false;

    /// <summary>
    /// If this operation requires a specific FHIR package to be loaded, the package identifier.
    /// </summary>
    public string RequiresPackage => string.Empty;

    /// <summary>Gets the supported resources.</summary>
    public HashSet<string> SupportedResources => new()
    {
        "Subscription"
    };

    /// <summary>Executes the Subscription/$events operation.</summary>
    /// <param name="ctx">          The authentication.</param>
    /// <param name="store">        The store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="focusResource">The focus resource.</param>
    /// <param name="bodyResource"> The body resource.</param>
    /// <param name="opResponse">   [out] The response resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool DoOperation(
        FhirRequestContext ctx,
        Storage.VersionedFhirStore store,
        Storage.IVersionedResourceStore? resourceStore,
        Hl7.Fhir.Model.Resource? focusResource,
        Hl7.Fhir.Model.Resource? bodyResource,
        out FhirResponseContext opResponse)
    {
        string eventsSince = string.Empty;
        string eventsUntil = string.Empty;
        string contentLevel = string.Empty;

        // check for a subscription ID
        if (string.IsNullOrEmpty(ctx.Id) ||
            (!store._subscriptions.ContainsKey(ctx.Id)))
        {
            opResponse = new()
            {
                StatusCode = HttpStatusCode.NotFound,
                Outcome = FhirCandle.Serialization.SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.NotFound,
                    $"Subscription {ctx.Id} was not found."),
            };

            return false;
        }

        // check for query string parameters
        if (!string.IsNullOrEmpty(ctx.UrlQuery))
        {
            System.Collections.Specialized.NameValueCollection query = System.Web.HttpUtility.ParseQueryString(ctx.UrlQuery);
            foreach (string key in query)
            {
                if (string.IsNullOrWhiteSpace(key) ||
                    string.IsNullOrWhiteSpace(query[key]))
                {
                    continue;
                }

                switch (key)
                {
                    case "events-since-number":
                    case "eventssincenumber":
                    case "eventsSinceNumber":
                        eventsSince = query[key] ?? string.Empty;
                        break;

                    case "events-until-number":
                    case "eventsuntilnumber":
                    case "eventsUntilNumber":
                        eventsUntil = query[key] ?? string.Empty;
                        break;

                    case "content":
                        contentLevel = query[key] ?? string.Empty;
                        break;
                }
            }
        }

        // check for body parameters
        if ((bodyResource != null) &&
            (bodyResource is Hl7.Fhir.Model.Parameters bodyParams) &&
            (bodyParams.Parameter?.Any() ?? false))
        {
            eventsSince = bodyParams.Parameter
                .Where(p => p.Name.Equals("eventsSinceNumber", StringComparison.Ordinal))?
                .Select(p => p.Value.ToString() ?? string.Empty)
                .First() ?? string.Empty;

            eventsUntil = bodyParams.Parameter
                .Where(p => p.Name.Equals("eventsUntilNumber", StringComparison.Ordinal))?
                .Select(p => p.Value.ToString() ?? string.Empty)
                .First() ?? string.Empty;

            contentLevel = bodyParams.Parameter
                .Where(p => p.Name.Equals("content", StringComparison.Ordinal))?
                .Select(p => p.Value.ToString() ?? string.Empty)
                .First() ?? string.Empty;
        }

        long highestEvent = store._subscriptions[ctx.Id].CurrentEventCount;

        if (!long.TryParse(eventsSince, out long sinceNumber))
        {
            sinceNumber = 0;
        }

        if (!long.TryParse(eventsUntil, out long untilNumber))
        {
            untilNumber = highestEvent;
        }

        List<long> eventNumbers = new();
        for (long i = sinceNumber; i <= untilNumber; i++)
        {
            eventNumbers.Add(i);
        }

        opResponse = new()
        {
            StatusCode = HttpStatusCode.OK,
            Resource = store.BundleForSubscriptionEvents(
                ctx.Id,
                eventNumbers,
                "query-event",
                contentLevel),
            Outcome = FhirCandle.Serialization.SerializationUtils.BuildOutcomeForRequest(
                HttpStatusCode.OK,
                $"Events for subscription {ctx.Id}."),
        };

        return true;
    }

    /// <summary>Gets an OperationDefinition for this operation.</summary>
    /// <param name="fhirVersion">The FHIR version.</param>
    /// <returns>The definition.</returns>
    public Hl7.Fhir.Model.OperationDefinition? GetDefinition(
        FhirCandle.Utils.FhirReleases.FhirSequenceCodes fhirVersion)
    {
        Hl7.Fhir.Model.OperationDefinition def = new()
        {
            Id = OperationName.Substring(1) + "-" + OperationVersion.Replace('.', '-'),
            Name = OperationName,
            Url = CanonicalByFhirVersion[fhirVersion],
            Status = Hl7.Fhir.Model.PublicationStatus.Draft,
            Kind = IsNamedQuery ? Hl7.Fhir.Model.OperationDefinition.OperationKind.Query : Hl7.Fhir.Model.OperationDefinition.OperationKind.Operation,
            Code = OperationName.Substring(1),
            Resource = SupportedResources.CopyTargetsNullable(),
            System = AllowSystemLevel,
            Type = AllowResourceLevel,
            Instance = AllowInstanceLevel,
            Parameter = new(),
        };

        def.Parameter.Add(new()
        {
            Name = "eventsSinceNumber",
            Use = Hl7.Fhir.Model.OperationParameterUse.In,
            Min = 0,
            Max = "1",
            Type = VersionedUtils.GetInt64Type,
            Documentation = "The starting event number, inclusive of this event (lower bound)."
        });

        def.Parameter.Add(new()
        {
            Name = "eventsUntilNumber",
            Use = Hl7.Fhir.Model.OperationParameterUse.In,
            Min = 0,
            Max = "1",
            Type = VersionedUtils.GetInt64Type,
            Documentation = "The ending event number, inclusive of this event (upper bound)."
        });

        def.Parameter.Add(new()
        {
            Name = "content",
            Use = Hl7.Fhir.Model.OperationParameterUse.In,
            Min = 0,
            Max = "1",
            Type = Hl7.Fhir.Model.FHIRAllTypes.Code,
            Binding = new()
            {
                Strength = Hl7.Fhir.Model.BindingStrength.Required,
                ValueSet = SubscriptionConverter.PayloadContentVsUrl,
            },
            Documentation = "Requested content style of returned data. Codes are from the payload content value set (e.g., empty, id-only, full-resource). Note that this is only a hint to the server what a client would prefer, and MAY be ignored."
        });

        def.Parameter.Add(new()
        {
            Name = "return",
            Use = Hl7.Fhir.Model.OperationParameterUse.Out,
            Min = 1,
            Max = "1",
            Type = Hl7.Fhir.Model.FHIRAllTypes.Bundle,
            Documentation = GetReturnDocValue(),
        });

        string GetReturnDocValue() => fhirVersion switch
        {
            Utils.FhirReleases.FhirSequenceCodes.R4 => "The operation returns a valid notification bundle, with the first entry being a Parameters resource. The bundle type is \"history\".",
            Utils.FhirReleases.FhirSequenceCodes.R4B => "The operation returns a valid notification bundle, with the first entry being a SubscriptionStatus resource. The bundle type is \"history\".",
            Utils.FhirReleases.FhirSequenceCodes.R5 => "The operation returns a valid notification bundle, with the first entry being a SubscriptionStatus resource. The bundle type is \"subscription-notification\".",
            _ => string.Empty,
        };

        return def;
    }
}


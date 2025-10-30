// <copyright file="TopicConverter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;

namespace FhirCandle.Subscriptions;

/// <summary>A FHIR R4B topic format converter.</summary>
public class TopicConverter
{
    /// <summary>
    /// Attempts to parse a FHIR SubscriptionTopic.
    /// </summary>
    /// <param name="topic"> The topic.</param>
    /// <param name="common">[out] The common.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParse(object topic, out ParsedSubscriptionTopic common)
    {
        if ((topic is null) ||
            (topic is not Hl7.Fhir.Model.SubscriptionTopic st) ||
            string.IsNullOrEmpty(st.Id) ||
            string.IsNullOrEmpty(st.Url))
        {
            common = null!;
            return false;
        }

        common = new()
        {
            Id = st.Id,
            Url = st.Url,
            Status = st.Status?.ToString() ?? string.Empty,
            Name = string.Empty,
            Version = st.Version ?? string.Empty,
            Title = st.Title ?? string.Empty,
            Date = st.Date?.ToString() ?? string.Empty,
            Description = st.Description ?? string.Empty,
        };

        if (st.ResourceTrigger?.Any() ?? false)
        {
            foreach (Hl7.Fhir.Model.SubscriptionTopic.ResourceTriggerComponent rt in st.ResourceTrigger)
            {
                if (rt.Resource is null)
                {
                    continue;
                }

                string resourceType = rt.Resource.Contains('/') ? rt.Resource.Substring(rt.Resource.LastIndexOf('/') + 1) : rt.Resource;

                if (!common.ResourceTriggers.ContainsKey(resourceType))
                {
                    common.ResourceTriggers.Add(resourceType, new());
                }

                common.ResourceTriggers[resourceType].Add(new(
                    resourceType,
                    rt.SupportedInteraction?.Contains(Hl7.Fhir.Model.SubscriptionTopic.InteractionTrigger.Create) ?? false,
                    rt.SupportedInteraction?.Contains(Hl7.Fhir.Model.SubscriptionTopic.InteractionTrigger.Update) ?? false,
                    rt.SupportedInteraction?.Contains(Hl7.Fhir.Model.SubscriptionTopic.InteractionTrigger.Delete) ?? false,
                    rt.QueryCriteria?.Previous ?? string.Empty,
                    (rt.QueryCriteria?.ResultForCreate ?? Hl7.Fhir.Model.SubscriptionTopic.CriteriaNotExistsBehavior.TestFails) == Hl7.Fhir.Model.SubscriptionTopic.CriteriaNotExistsBehavior.TestPasses,
                    (rt.QueryCriteria?.ResultForCreate ?? Hl7.Fhir.Model.SubscriptionTopic.CriteriaNotExistsBehavior.TestPasses) == Hl7.Fhir.Model.SubscriptionTopic.CriteriaNotExistsBehavior.TestFails,
                    rt.QueryCriteria?.Current ?? string.Empty,
                    (rt.QueryCriteria?.ResultForDelete ?? Hl7.Fhir.Model.SubscriptionTopic.CriteriaNotExistsBehavior.TestFails) == Hl7.Fhir.Model.SubscriptionTopic.CriteriaNotExistsBehavior.TestPasses,
                    (rt.QueryCriteria?.ResultForDelete ?? Hl7.Fhir.Model.SubscriptionTopic.CriteriaNotExistsBehavior.TestPasses) == Hl7.Fhir.Model.SubscriptionTopic.CriteriaNotExistsBehavior.TestFails,
                    (rt.QueryCriteria?.RequireBoth ?? false) == true,
                    rt.FhirPathCriteria ?? string.Empty));
            }
        }

        if (st.EventTrigger?.Any() ?? false)
        {
            foreach (Hl7.Fhir.Model.SubscriptionTopic.EventTriggerComponent et in st.EventTrigger)
            {
                if (et.Resource is null)
                {
                    continue;
                }

                if (!common.EventTriggers.ContainsKey(et.Resource))
                {
                    common.EventTriggers.Add(et.Resource, new());
                }

                if (!(et.Event?.Coding?.Any() ?? false))
                {
                    common.EventTriggers[et.Resource].Add(new(
                            et.Resource,
                            string.Empty,
                            string.Empty,
                            et.Event?.Text ?? et.Description ?? string.Empty));
                    continue;
                }

                foreach (Hl7.Fhir.Model.Coding c in et.Event.Coding)
                {
                    common.EventTriggers[et.Resource].Add(new(
                        et.Resource,
                        c.System ?? string.Empty,
                        c.Code ?? string.Empty,
                        c.Display ?? string.Empty));
                }
            }
        }

        if (st.CanFilterBy?.Any() ?? false)
        {
            foreach (Hl7.Fhir.Model.SubscriptionTopic.CanFilterByComponent cf in st.CanFilterBy)
            {
                if (cf.Resource is null)
                {
                    continue;
                }

                string resourceType = cf.Resource.Contains('/') ? cf.Resource.Substring(cf.Resource.LastIndexOf('/') + 1) : cf.Resource;

                if (!common.AllowedFilters.ContainsKey(resourceType))
                {
                    common.AllowedFilters.Add(resourceType, new());
                }

                // TODO: Grab the FHIRPath expression from the definition
                common.AllowedFilters[resourceType].Add(new(
                    resourceType,
                    cf.FilterParameter ?? string.Empty,
                    cf.FilterDefinition ?? string.Empty));
            }
        }

        if (st.NotificationShape?.Any() ?? false)
        {
            foreach (Hl7.Fhir.Model.SubscriptionTopic.NotificationShapeComponent ns in st.NotificationShape)
            {
                if (ns.Resource is null)
                {
                    continue;
                }

                string resourceType = ns.Resource.Contains('/') ? ns.Resource.Substring(ns.Resource.LastIndexOf('/') + 1) : ns.Resource;

                if (!common.NotificationShapes.ContainsKey(resourceType))
                {
                    common.NotificationShapes.Add(resourceType, new());
                }

                common.NotificationShapes[resourceType].Add(new(
                    resourceType,
                    ns.Include?.Select(i => "_include=" + i?.Replace("&iterate=", "&_include:iterate=", StringComparison.Ordinal)).ToList() ?? new(),
                    ns.RevInclude?.Select(r => "_revinclude=" + r?.Replace("&iterate=", "&_revinclude:iterate=", StringComparison.Ordinal)).ToList() ?? new()));
            }
        }

        return true;
    }
}

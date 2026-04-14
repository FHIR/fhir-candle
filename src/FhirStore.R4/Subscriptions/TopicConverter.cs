// <copyright file="TopicConverter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using Hl7.Fhir.Model;
using static FhirCandle.Subscriptions.ConverterUtils;

namespace FhirCandle.Subscriptions;

/// <summary>FHIR R4 topic format converter.</summary>
public class TopicConverter
{
    /// <summary>Attempts to parse a FHIR SubscriptionTopic.</summary>
    /// <param name="topic"> The topic.</param>
    /// <param name="common">[out] The common.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParse(object topic, out ParsedSubscriptionTopic common)
    {
        if ((topic is null) ||
            (topic is not Hl7.Fhir.Model.Basic st) ||
            string.IsNullOrEmpty(st.Id) ||
            (st.Code is null) ||
            (st.Code.Coding is null) ||
            !st.Code.Coding.Any(c =>
                (c.Code?.Equals("SubscriptionTopic", StringComparison.Ordinal) ?? false) &&
                (c.System?.Equals("http://hl7.org/fhir/fhir-types", StringComparison.Ordinal) ?? false)))
        {
            common = null!;
            return false;
        }

        ParseExtensions(
            st.ModifierExtension,
            out Dictionary<string, List<Hl7.Fhir.Model.DataType?>> modifierExts,
            out Dictionary<string, List<List<Hl7.Fhir.Model.Extension>>> modifierNested);

        ParseExtensions(
            st.Extension,
            out Dictionary<string, List<Hl7.Fhir.Model.DataType?>> exts,
            out Dictionary<string, List<List<Hl7.Fhir.Model.Extension>>> nested);

        common = new()
        {
            Id = st.Id,
            Url = GetString(exts, "url"),
            Status = GetString(modifierExts, "status"),
            Name = GetString(exts, "name"),
            Version = GetString(exts, "version"),
            Title = GetString(exts, "title"),
            Date = GetString(exts, "date"),
            Description = GetString(exts, "description"),
        };

        if (nested.ContainsKey("resourceTrigger"))
        {
            foreach (List<Hl7.Fhir.Model.Extension> rtExtensions in nested["resourceTrigger"])
            {
                ParseExtensions(
                    rtExtensions,
                    out Dictionary<string, List<Hl7.Fhir.Model.DataType?>> rt,
                    out Dictionary<string, List<List<Hl7.Fhir.Model.Extension>>> rtNested);

                string resourceType = GetString(rt, "resource");

                if (resourceType.Contains('/'))
                {
                    resourceType = resourceType.Substring(resourceType.LastIndexOf('/') + 1);
                }

                if (!common.ResourceTriggers.ContainsKey(resourceType))
                {
                    common.ResourceTriggers.Add(resourceType, new());
                }

                HashSet<string> interactions = new();
                foreach (string i in GetStrings(rt, "supportedInteraction").Distinct())
                {
                    interactions.Add(i);
                }

                Dictionary<string, List<Hl7.Fhir.Model.DataType?>> qc;
                if (rtNested.ContainsKey("queryCriteria"))
                {
                    ParseExtensions(rtNested["queryCriteria"].First(), out qc, out _);
                }
                else
                {
                    qc = new();
                }

                common.ResourceTriggers[resourceType].Add(new(
                    resourceType,
                    interactions.Contains("create"),
                    interactions.Contains("update"),
                    interactions.Contains("delete"),
                    GetString(qc, "previous"),
                    GetString(qc, "resultForCreate").Equals("test-passes", StringComparison.Ordinal) ? true : false,
                    GetString(qc, "resultForCreate").Equals("test-fails", StringComparison.Ordinal) ? true : false,
                    GetString(qc, "current"),
                    GetString(qc, "resultForDelete").Equals("test-passes", StringComparison.Ordinal) ? true : false,
                    GetString(qc, "resultForDelete").Equals("test-fails", StringComparison.Ordinal) ? true : false,
                    GetBool(qc, "requireBoth"),
                    GetString(rt, "fhirPathCriteria")));
            }
        }

        if (nested.ContainsKey("eventTrigger"))
        {
            foreach (List<Hl7.Fhir.Model.Extension> etExtensions in nested["eventTrigger"])
            {
                ParseExtensions(
                    etExtensions,
                    out Dictionary<string, List<Hl7.Fhir.Model.DataType?>> et,
                    out Dictionary<string, List<List<Hl7.Fhir.Model.Extension>>> etNested);

                string resourceType = GetString(et, "resource");

                if (resourceType.Contains('/'))
                {
                    resourceType = resourceType.Substring(resourceType.LastIndexOf('/') + 1);
                }

                if (!common.EventTriggers.ContainsKey(resourceType))
                {
                    common.EventTriggers.Add(resourceType, new());
                }

                if ((et["event"].First() is CodeableConcept etConcept) &&
                    etConcept.Coding.Any())
                {
                    foreach (Hl7.Fhir.Model.Coding c in etConcept.Coding)
                    {
                        common.EventTriggers[resourceType].Add(new(
                            resourceType,
                            c.System ?? string.Empty,
                            c.Code ?? string.Empty,
                            c.Display ?? GetString(et, "description")));
                    }
                }
            }
        }

        if (nested.ContainsKey("canFilterBy"))
        {
            foreach (List<Hl7.Fhir.Model.Extension> cfExtensions in nested["canFilterBy"])
            {
                ParseExtensions(
                    cfExtensions,
                    out Dictionary<string, List<Hl7.Fhir.Model.DataType?>> cf,
                    out Dictionary<string, List<List<Hl7.Fhir.Model.Extension>>> cfNested);

                string resourceType = GetString(cf, "resource");

                if (resourceType.Contains('/'))
                {
                    resourceType = resourceType.Substring(resourceType.LastIndexOf('/') + 1);
                }

                if (!common.AllowedFilters.ContainsKey(resourceType))
                {
                    common.AllowedFilters.Add(resourceType, new());
                }

                // TODO: Grab the FHIRPath expression from the definition
                // TODO: Get Comparators and Modifiers
                common.AllowedFilters[resourceType].Add(new(
                    resourceType,
                    GetString(cf, "filterParameter"),
                    GetString(cf, "filterDefinition")));
            }
        }

        if (nested.ContainsKey("notificationShape"))
        {
            foreach (List<Hl7.Fhir.Model.Extension> nsExtensions in nested["notificationShape"])
            {
                ParseExtensions(
                    nsExtensions,
                    out Dictionary<string, List<Hl7.Fhir.Model.DataType?>> ns,
                    out Dictionary<string, List<List<Hl7.Fhir.Model.Extension>>> nsNested);

                string resourceType = GetString(ns, "resource");

                if (resourceType.Contains('/'))
                {
                    resourceType = resourceType.Substring(resourceType.LastIndexOf('/') + 1);
                }

                if (!common.NotificationShapes.ContainsKey(resourceType))
                {
                    common.NotificationShapes.Add(resourceType, new());
                }

                common.NotificationShapes[resourceType].Add(new(
                    resourceType,
                    GetStrings(ns, "include")?.Select(i => "_include=" + i.Replace("&iterate=", "&_include:iterate=", StringComparison.Ordinal)).ToList() ?? new(),
                    GetStrings(ns, "revInclude")?.Select(r => "_revinclude=" + r.Replace("&iterate=", "&_revinclude:iterate=", StringComparison.Ordinal)).ToList() ?? new()));
            }
        }

        return true;
    }
}

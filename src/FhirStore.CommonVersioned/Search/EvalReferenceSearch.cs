// <copyright file="EvalReferenceSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using Hl7.Fhir.ElementModel;

namespace FhirCandle.Search;

/// <summary>A class that contains functions to test reference inputs against various FHIR types.</summary>
public static class EvalReferenceSearch
{
    /// <summary>Compare references common.</summary>
    /// <param name="r">A ResourceReference to process.</param>
    /// <param name="s">A SegmentedReference to process.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private static bool CompareRefsCommon(Hl7.Fhir.Model.ResourceReference r, ParsedSearchParameter.SegmentedReference s)
    {
        if (!string.IsNullOrEmpty(s.ResourceType) &&
            !string.IsNullOrEmpty(s.Id) &&
            (r.Reference == s.ResourceType + "/" + s.Id))
        {
            return true;
        }
        if (string.IsNullOrEmpty(s.ResourceType) &&
            (!string.IsNullOrEmpty(s.Id)) &&
            r.Reference.EndsWith("/" + s.Id, StringComparison.Ordinal))
        {
            // TODO: check resource versions

            return true;
        }

        if (s.Url.Equals(r.Reference, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>Compare OID reference.</summary>
    /// <param name="r">A ResourceReference to process.</param>
    /// <param name="s">A SegmentedReference to process.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private static bool CompareRefsOid(Hl7.Fhir.Model.ResourceReference r, ParsedSearchParameter.SegmentedReference s)
    {
        if (s.Url.Equals(r.Reference, StringComparison.OrdinalIgnoreCase) ||
            s.Url.Equals("urn:oid:" + r.Reference, StringComparison.OrdinalIgnoreCase) ||
            ("urn:oid:" + s.Url).Equals(r.Reference, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>Compare UUID reference.</summary>
    /// <param name="r">A ResourceReference to process.</param>
    /// <param name="s">A SegmentedReference to process.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private static bool CompareRefsUuid(Hl7.Fhir.Model.ResourceReference r, ParsedSearchParameter.SegmentedReference s)
    {
        if (s.Url.Equals(r.Reference, StringComparison.OrdinalIgnoreCase) ||
            s.Url.Equals("urn:uuid:" + r.Reference, StringComparison.OrdinalIgnoreCase) ||
            ("urn:uuid:" + s.Url).Equals(r.Reference, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>Tests reference against most FHIR types.</summary>
    /// <param name="valueNode">         The value node.</param>
    /// <param name="sp">                The sp.</param>
    /// <param name="resourceTypeFilter">(Optional) A filter specifying the resource type.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestReference(
        ITypedElement valueNode,
        ParsedSearchParameter sp,
        string resourceTypeFilter = "")
    {
        if ((valueNode == null) ||
            (valueNode.InstanceType != "Reference") ||
            (sp.ValueReferences == null))
        {
            return false;
        }

        Hl7.Fhir.Model.ResourceReference r = valueNode.ToPoco<Hl7.Fhir.Model.ResourceReference>();

        if (string.IsNullOrEmpty(r.Reference))
        {
            return false;
        }

        string filterMatch = string.IsNullOrEmpty(resourceTypeFilter)
            ? string.Empty
            : resourceTypeFilter + '/';

        for (int i = 0; i < sp.ValueReferences.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (CompareRefsCommon(r, sp.ValueReferences[i]))
            {
                if (string.IsNullOrEmpty(filterMatch))
                {
                    return true;
                }

                if (r.Reference.Contains(filterMatch, StringComparison.Ordinal) ||
                    ((!string.IsNullOrEmpty(r.Type)) && r.Type.Equals(resourceTypeFilter)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tests references against OIDs.</summary>
    /// <param name="valueNode">         The value node.</param>
    /// <param name="sp">                The sp.</param>
    /// <param name="resourceTypeFilter">(Optional) A filter specifying the resource type.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestReferenceAgainstOid(
        ITypedElement valueNode, 
        ParsedSearchParameter sp,
        string resourceTypeFilter = "")
    {
        if ((valueNode == null) ||
            (valueNode.InstanceType != "Reference") ||
            (sp.ValueReferences == null))
        {
            return false;
        }

        Hl7.Fhir.Model.ResourceReference r = valueNode.ToPoco<Hl7.Fhir.Model.ResourceReference>();

        if (string.IsNullOrEmpty(r.Reference))
        {
            return false;
        }

        for (int i = 0; i < sp.ValueReferences.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (CompareRefsOid(r, sp.ValueReferences[i]))
            {
                if (string.IsNullOrEmpty(resourceTypeFilter))
                {
                    return true;
                }

                if ((!string.IsNullOrEmpty(r.Type)) && r.Type.Equals(resourceTypeFilter, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tests references against UUIDs.</summary>
    /// <param name="valueNode">         The value node.</param>
    /// <param name="sp">                The sp.</param>
    /// <param name="resourceTypeFilter">(Optional) A filter specifying the resource type.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestReferenceAgainstUuid(
        ITypedElement valueNode,
        ParsedSearchParameter sp,
        string resourceTypeFilter = "")
    {
        if ((valueNode == null) ||
            (valueNode.InstanceType != "Reference") ||
            (sp.ValueReferences == null))
        {
            return false;
        }

        Hl7.Fhir.Model.ResourceReference r = valueNode.ToPoco<Hl7.Fhir.Model.ResourceReference>();

        if (string.IsNullOrEmpty(r.Reference))
        {
            return false;
        }

        for (int i = 0; i < sp.ValueReferences.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (CompareRefsUuid(r, sp.ValueReferences[i]))
            {
                if (string.IsNullOrEmpty(resourceTypeFilter))
                {
                    return true;
                }

                if ((!string.IsNullOrEmpty(r.Type)) && r.Type.Equals(resourceTypeFilter, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tests reference against primitive url types (canonical, uri, url).</summary>
    /// <param name="valueNode">         The value node.</param>
    /// <param name="sp">                The sp.</param>
    /// <param name="resourceTypeFilter">(Optional) A filter specifying the resource type.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestReferenceAgainstPrimitive(
        ITypedElement valueNode, 
        ParsedSearchParameter sp,
        string resourceTypeFilter = "")
    {
        if ((valueNode == null) ||
            (sp.ValueReferences == null))
        {
            return false;
        }

        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        string filterMatch = string.IsNullOrEmpty(resourceTypeFilter)
            ? string.Empty
            : resourceTypeFilter + '/';

        int index = value.LastIndexOf('|');

        if (index != -1)
        {
            string cv = value.Substring(index + 1);
            string cu = value.Substring(0, index);

            for (int i = 0; i < sp.ValueReferences.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                ParsedSearchParameter.SegmentedReference s = sp.ValueReferences[i];

                if (s.Url.Equals(cu, StringComparison.Ordinal) &&
                    (string.IsNullOrEmpty(s.CanonicalVersion) || s.CanonicalVersion.Equals(cv, StringComparison.Ordinal)))
                {
                    if (string.IsNullOrEmpty(resourceTypeFilter))
                    {
                        return true;
                    }
                    
                    if (cu.Contains(filterMatch, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        for (int i = 0; i < sp.ValueReferences.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (sp.ValueReferences[i].Url.Equals(value, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(resourceTypeFilter))
                {
                    return true;
                }

                if (sp.ValueReferences[i].Url.Contains(filterMatch, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tests reference identifier.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestReferenceIdentifier(
        ITypedElement valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode == null) ||
            (sp.ValueFhirCodes == null))
        {
            return false;
        }

        Hl7.Fhir.Model.ResourceReference v = valueNode.ToPoco<Hl7.Fhir.Model.ResourceReference>();

        string valueSystem = v.Identifier?.System ?? string.Empty;
        string valueCode = v.Identifier?.Value ?? string.Empty;

        if (string.IsNullOrEmpty(valueSystem) && string.IsNullOrEmpty(valueCode))
        {
            return false;
        }
        
        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (EvalTokenSearch.CompareCodeWithSystem(valueSystem, valueCode, sp.ValueFhirCodes[i].System ?? string.Empty, sp.ValueFhirCodes[i].Value))
            {
                return true;
            }
        }

        return false;
    }
}

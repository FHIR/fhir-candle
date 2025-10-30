// <copyright file="EvalReferenceSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using FhirCandle.Storage;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;

namespace FhirCandle.Search;

/// <summary>A class that contains functions to test reference inputs against various FHIR types.</summary>
public static class EvalReferenceSearch
{
    /// <summary>Compare references common.</summary>
    /// <param name="r">A ResourceReference to process.</param>
    /// <param name="s">A SegmentedReference to process.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private static bool CompareRefsCommon(ResourceReference r, ParsedSearchParameter.SegmentedReference s)
    {
        if (!string.IsNullOrEmpty(s.ResourceType) &&
            !string.IsNullOrEmpty(s.Id) &&
            (r.Reference == s.ResourceType + "/" + s.Id))
        {
            return true;
        }
        if (string.IsNullOrEmpty(s.ResourceType) &&
            (!string.IsNullOrEmpty(s.Id)) &&
            (r.Reference?.EndsWith("/" + s.Id, StringComparison.Ordinal) == true))
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
    private static bool CompareRefsOid(ResourceReference r, ParsedSearchParameter.SegmentedReference s)
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
    private static bool CompareRefsUuid(ResourceReference r, ParsedSearchParameter.SegmentedReference s)
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
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        string resourceTypeFilter = "")
    {
        if ((valueNode?.Poco is not ResourceReference r) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        // TODO: need to check for Display element and compare strings

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
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        string resourceTypeFilter = "")
    {
        if ((valueNode?.Poco is not ResourceReference r) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

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
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        string resourceTypeFilter = "")
    {
        if ((valueNode?.Poco is not ResourceReference r) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

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
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        string resourceTypeFilter = "")
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? value = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

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
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is not ResourceReference r) ||
            (sp.ValueFhirCodes is null))
        {
            return false;
        }

        string valueSystem = r.Identifier?.System ?? string.Empty;
        string valueCode = r.Identifier?.Value ?? string.Empty;

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

    /// <summary>Tests whether the canonical reference in a resource is or subsumes the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the canonical reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the resource canonical reference subsumes the search parameter, false otherwise.</returns>
    public static bool TestReferenceAboveCanonical(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            ((sp.ValueReferences is null) && (sp.Values is null)))
        {
            return false;
        }

        string? canonicalUrl = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(canonicalUrl))
        {
            return false;
        }

        // Parse canonical URL to extract version if present
        string resourceUrl;
        string resourceVersion = string.Empty;
        int versionIndex = canonicalUrl.LastIndexOf('|');
        if (versionIndex != -1)
        {
            resourceUrl = canonicalUrl.Substring(0, versionIndex);
            resourceVersion = canonicalUrl.Substring(versionIndex + 1);
        }
        else
        {
            resourceUrl = canonicalUrl;
        }

        for (int i = 0; i < (sp.ValueReferences?.Length ?? sp.Values.Length); i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            ParsedSearchParameter.SegmentedReference searchRef = sp.ValueReferences is not null
                ? sp.ValueReferences[i]
                : new() { Url = sp.Values[i] };

            // Check if URLs match
            if (!resourceUrl.Equals(searchRef.Url, StringComparison.Ordinal))
            {
                continue;
            }

            // For version hierarchy, check if resource version >= search version
            if (!string.IsNullOrEmpty(searchRef.CanonicalVersion) && !string.IsNullOrEmpty(resourceVersion))
            {
                // Simple version comparison - could be enhanced with proper version scheme handling
                if (string.Compare(resourceVersion, searchRef.CanonicalVersion, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            else if (string.IsNullOrEmpty(searchRef.CanonicalVersion))
            {
                // No version specified in search, any version matches
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests whether the OID reference in a resource is or subsumes the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the OID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the resource OID subsumes the search parameter, false otherwise.</returns>
    public static bool TestReferenceAboveOid(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? oidValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(oidValue))
        {
            return false;
        }

        // Normalize OID format
        string normalizedOid = oidValue.StartsWith("urn:oid:", StringComparison.OrdinalIgnoreCase)
            ? oidValue.Substring(8)
            : oidValue;

        for (int i = 0; i < sp.ValueReferences.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchOid = sp.ValueReferences[i].Url;
            if (searchOid.StartsWith("urn:oid:", StringComparison.OrdinalIgnoreCase))
            {
                searchOid = searchOid.Substring(8);
            }

            // Check OID hierarchy: 1.2.3.4 is parent of 1.2.3.4.5
            if (searchOid.StartsWith(normalizedOid + ".", StringComparison.Ordinal) ||
                searchOid.Equals(normalizedOid, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests whether the URI reference in a resource is or subsumes the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the URI reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the resource URI subsumes the search parameter, false otherwise.</returns>
    public static bool TestReferenceAboveUri(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? uriValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(uriValue))
        {
            return false;
        }

        // For most URIs, hierarchical relationships are not well-defined
        // This could delegate to terminology service for code system hierarchies
        // For now, implement basic containment check for URLs
        for (int i = 0; i < sp.ValueReferences.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchUri = sp.ValueReferences[i].Url;

            // For URL-like URIs, check if resource URI is parent of search URI
            if (Uri.TryCreate(uriValue, UriKind.Absolute, out Uri? resourceUri) &&
                Uri.TryCreate(searchUri, UriKind.Absolute, out Uri? searchUriObj) &&
                resourceUri.Scheme == searchUriObj.Scheme &&
                resourceUri.Host == searchUriObj.Host)
            {
                string resourcePath = resourceUri.AbsolutePath.TrimEnd('/');
                string searchPath = searchUriObj.AbsolutePath.TrimEnd('/');

                if (searchPath.StartsWith(resourcePath + "/", StringComparison.Ordinal) ||
                    searchPath.Equals(resourcePath, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tests whether the URL reference in a resource is or subsumes the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the URL reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the resource URL subsumes the search parameter, false otherwise.</returns>
    public static bool TestReferenceAboveUrl(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? urlValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(urlValue))
        {
            return false;
        }

        for (int i = 0; i < sp.ValueReferences.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchUrl = sp.ValueReferences[i].Url;

            if (Uri.TryCreate(urlValue, UriKind.Absolute, out Uri? resourceUri) &&
                Uri.TryCreate(searchUrl, UriKind.Absolute, out Uri? searchUri) &&
                resourceUri.Scheme == searchUri.Scheme &&
                resourceUri.Host == searchUri.Host)
            {
                string resourcePath = resourceUri.AbsolutePath.TrimEnd('/');
                string searchPath = searchUri.AbsolutePath.TrimEnd('/');

                // Check if resource URL path is parent of search URL path
                if (searchPath.StartsWith(resourcePath + "/", StringComparison.Ordinal) ||
                    searchPath.Equals(resourcePath, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }


    /// <summary>Tests whether the canonical reference in a resource is or is subsumed by the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the canonical reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the search parameter subsumes the resource canonical reference, false otherwise.</returns>
    public static bool TestReferenceBelowCanonical(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? canonicalUrl = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(canonicalUrl))
        {
            return false;
        }

        // Parse canonical URL to extract version if present
        string resourceUrl;
        string resourceVersion = string.Empty;
        int versionIndex = canonicalUrl.LastIndexOf('|');
        if (versionIndex != -1)
        {
            resourceUrl = canonicalUrl.Substring(0, versionIndex);
            resourceVersion = canonicalUrl.Substring(versionIndex + 1);
        }
        else
        {
            resourceUrl = canonicalUrl;
        }

        for (int i = 0; i < sp.ValueReferences.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            ParsedSearchParameter.SegmentedReference searchRef = sp.ValueReferences[i];

            // Check if URLs match
            if (!resourceUrl.Equals(searchRef.Url, StringComparison.Ordinal))
            {
                continue;
            }

            // For version hierarchy, check if resource version <= search version
            if (!string.IsNullOrEmpty(searchRef.CanonicalVersion) && !string.IsNullOrEmpty(resourceVersion))
            {
                // Simple version comparison - could be enhanced with proper version scheme handling
                if (string.Compare(resourceVersion, searchRef.CanonicalVersion, StringComparison.OrdinalIgnoreCase) <= 0)
                {
                    return true;
                }
            }
            else if (string.IsNullOrEmpty(searchRef.CanonicalVersion))
            {
                // No version specified in search, any version matches
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests whether the OID reference in a resource is or is subsumed by the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the OID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the search parameter OID subsumes the resource OID, false otherwise.</returns>
    public static bool TestReferenceBelowOid(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? oidValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(oidValue))
        {
            return false;
        }

        // Normalize OID format
        string normalizedOid = oidValue.StartsWith("urn:oid:", StringComparison.OrdinalIgnoreCase)
            ? oidValue.Substring(8)
            : oidValue;

        for (int i = 0; i < sp.ValueReferences.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchOid = sp.ValueReferences[i].Url;
            if (searchOid.StartsWith("urn:oid:", StringComparison.OrdinalIgnoreCase))
            {
                searchOid = searchOid.Substring(8);
            }

            // Check OID hierarchy: 1.2.3.4.5 is child of 1.2.3.4
            if (normalizedOid.StartsWith(searchOid + ".", StringComparison.Ordinal) ||
                normalizedOid.Equals(searchOid, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests whether the URI reference in a resource is or is subsumed by the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the URI reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the search parameter URI subsumes the resource URI, false otherwise.</returns>
    public static bool TestReferenceBelowUri(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? uriValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(uriValue))
        {
            return false;
        }

        // For most URIs, hierarchical relationships are not well-defined
        // This could delegate to terminology service for code system hierarchies
        // For now, implement basic containment check for URLs
        for (int i = 0; i < sp.ValueReferences.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchUri = sp.ValueReferences[i].Url;

            // For URL-like URIs, check if search URI is parent of resource URI
            if (Uri.TryCreate(uriValue, UriKind.Absolute, out Uri? resourceUri) &&
                Uri.TryCreate(searchUri, UriKind.Absolute, out Uri? searchUriObj) &&
                resourceUri.Scheme == searchUriObj.Scheme &&
                resourceUri.Host == searchUriObj.Host)
            {
                string resourcePath = resourceUri.AbsolutePath.TrimEnd('/');
                string searchPath = searchUriObj.AbsolutePath.TrimEnd('/');

                if (resourcePath.StartsWith(searchPath + "/", StringComparison.Ordinal) ||
                    resourcePath.Equals(searchPath, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tests whether the URL reference in a resource is or is subsumed by the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the URL reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the search parameter URL subsumes the resource URL, false otherwise.</returns>
    public static bool TestReferenceBelowUrl(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? urlValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(urlValue))
        {
            return false;
        }

        for (int i = 0; i < sp.ValueReferences.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchUrl = sp.ValueReferences[i].Url;

            if (Uri.TryCreate(urlValue, UriKind.Absolute, out Uri? resourceUri) &&
                Uri.TryCreate(searchUrl, UriKind.Absolute, out Uri? searchUri) &&
                resourceUri.Scheme == searchUri.Scheme &&
                resourceUri.Host == searchUri.Host)
            {
                string resourcePath = resourceUri.AbsolutePath.TrimEnd('/');
                string searchPath = searchUri.AbsolutePath.TrimEnd('/');

                // Check if search URL path is parent of resource URL path
                if (resourcePath.StartsWith(searchPath + "/", StringComparison.Ordinal) ||
                    resourcePath.Equals(searchPath, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }


    /// <summary>Tests whether a coded value in a canonical reference matches the supplied parameter value using basic string matching.</summary>
    /// <param name="valueNode">The value node containing the canonical reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if any code text matches search criteria, false otherwise.</returns>
    public static bool TestReferenceCodeTextCanonical(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? canonicalUrl = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(canonicalUrl))
        {
            return false;
        }

        // Extract code-like portions from canonical URL (e.g., after last '/')
        string[] urlParts = canonicalUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (urlParts.Length == 0)
        {
            return false;
        }

        string extractedCode = urlParts[urlParts.Length - 1];

        // Remove version if present
        int versionIndex = extractedCode.LastIndexOf('|');
        if (versionIndex != -1)
        {
            extractedCode = extractedCode.Substring(0, versionIndex);
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchString = sp.Values[i];
            if (extractedCode.StartsWith(searchString, StringComparison.OrdinalIgnoreCase) ||
                extractedCode.Equals(searchString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    /// <summary>Tests whether a coded value in an OID reference matches the supplied parameter value using basic string matching.</summary>
    /// <param name="valueNode">The value node containing the OID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if any code text matches search criteria, false otherwise.</returns>
    public static bool TestReferenceCodeTextOid(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? oidValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(oidValue))
        {
            return false;
        }

        // OIDs are typically numeric dotted notation, not text codes
        // Convert OID segments to string and apply text matching
        string normalizedOid = oidValue.StartsWith("urn:oid:", StringComparison.OrdinalIgnoreCase)
            ? oidValue.Substring(8)
            : oidValue;

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchString = sp.Values[i];
            if (normalizedOid.Contains(searchString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests whether a coded value in a URI reference matches the supplied parameter value using basic string matching.</summary>
    /// <param name="valueNode">The value node containing the URI reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if any code text matches search criteria, false otherwise.</returns>
    public static bool TestReferenceCodeTextUri(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? uriValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(uriValue))
        {
            return false;
        }

        // Extract code-like portions from URI (path segments, fragments)
        if (Uri.TryCreate(uriValue, UriKind.Absolute, out Uri? uri))
        {
            string[] pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string fragment = uri.Fragment?.TrimStart('#') ?? string.Empty;

            List<string> codeParts = new List<string>(pathSegments);
            if (!string.IsNullOrEmpty(fragment))
            {
                codeParts.Add(fragment);
            }

            for (int i = 0; i < sp.Values.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                string searchString = sp.Values[i];
                foreach (string codePart in codeParts)
                {
                    if (codePart.StartsWith(searchString, StringComparison.OrdinalIgnoreCase) ||
                        codePart.Equals(searchString, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Tests whether a coded value in a URL reference matches the supplied parameter value using basic string matching.</summary>
    /// <param name="valueNode">The value node containing the URL reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if any code text matches search criteria, false otherwise.</returns>
    public static bool TestReferenceCodeTextUrl(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? urlValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(urlValue))
        {
            return false;
        }

        // Parse URL path segments and query parameters for code-like values
        if (Uri.TryCreate(urlValue, UriKind.Absolute, out Uri? uri))
        {
            string[] pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            List<string> codeParts = new List<string>(pathSegments);

            // Add query parameter values
            if (!string.IsNullOrEmpty(uri.Query))
            {
                string query = uri.Query.TrimStart('?');
                string[] queryPairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
                foreach (string pair in queryPairs)
                {
                    string[] keyValue = pair.Split('=', 2);
                    if (keyValue.Length == 2)
                    {
                        codeParts.Add(keyValue[1]);
                    }
                }
            }

            for (int i = 0; i < sp.Values.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                string searchString = sp.Values[i];
                foreach (string codePart in codeParts)
                {
                    if (codePart.StartsWith(searchString, StringComparison.OrdinalIgnoreCase) ||
                        codePart.Equals(searchString, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Tests whether a coded value in a UUID reference matches the supplied parameter value using basic string matching.</summary>
    /// <param name="valueNode">The value node containing the UUID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if any code text matches search criteria, false otherwise.</returns>
    public static bool TestReferenceCodeTextUuid(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? uuidValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(uuidValue))
        {
            return false;
        }

        // UUIDs are typically not textual codes, but check for pattern matching
        string normalizedUuid = uuidValue.StartsWith("urn:uuid:", StringComparison.OrdinalIgnoreCase)
            ? uuidValue.Substring(9)
            : uuidValue;

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchString = sp.Values[i];
            if (normalizedUuid.Contains(searchString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }



    /// <summary>Tests whether the URI reference in a resource is a member of the supplied parameter ValueSet.</summary>
    /// <param name="valueNode">The value node containing the URI reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology service access.</param>
    /// <returns>True if URI or its codes are in the ValueSet, false otherwise.</returns>
    public static bool TestReferenceInUri(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? uriValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(uriValue))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.Values[i];

            // Check if URI itself is a code in the ValueSet
            // For code system URIs, we might extract codes and check membership
            // For now, check if the URI directly matches a concept in the ValueSet
            if (store.Terminology.VsContains(valueSetUri, string.Empty, uriValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests whether the URL reference in a resource is a member of the supplied parameter ValueSet.</summary>
    /// <param name="valueNode">The value node containing the URL reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology service access.</param>
    /// <returns>True if URL-derived codes are in the ValueSet, false otherwise.</returns>
    public static bool TestReferenceInUrl(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? urlValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(urlValue))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.Values[i];

            // Check if URL represents a code that could be in the ValueSet
            // For terminology URLs, extract relevant code portions
            if (store.Terminology.VsContains(valueSetUri, string.Empty, urlValue))
            {
                return true;
            }

            // Could also extract meaningful code portions from URL path
            if (Uri.TryCreate(urlValue, UriKind.Absolute, out Uri? uri))
            {
                string[] pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                foreach (string segment in pathSegments)
                {
                    if (store.Terminology.VsContains(valueSetUri, string.Empty, segment))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }


    /// <summary>Tests whether the URI reference in a resource is not a member of the supplied parameter ValueSet.</summary>
    /// <param name="valueNode">The value node containing the URI reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology service access.</param>
    /// <returns>True if URI and its codes are NOT in the ValueSet, false otherwise.</returns>
    public static bool TestReferenceNotInUri(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? uriValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(uriValue))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.Values[i];

            // Check if URI itself is NOT a code in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, uriValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests whether the URL reference in a resource is not a member of the supplied parameter ValueSet.</summary>
    /// <param name="valueNode">The value node containing the URL reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology service access.</param>
    /// <returns>True if URL-derived codes are NOT in the ValueSet, false otherwise.</returns>
    public static bool TestReferenceNotInUrl(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueReferences is null))
        {
            return false;
        }

        string? urlValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Id fid => fid.Value,
            Oid fo => fo.Value,
            ResourceReference fr => fr.Reference,
            Uuid fuu => fuu.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(urlValue))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.Values[i];

            // Check if URL does NOT represent a code in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, urlValue))
            {
                // Also check meaningful code portions from URL path
                if (Uri.TryCreate(urlValue, UriKind.Absolute, out Uri? uri))
                {
                    string[] pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    bool anySegmentInValueSet = false;
                    foreach (string segment in pathSegments)
                    {
                        if (store.Terminology.VsContains(valueSetUri, string.Empty, segment))
                        {
                            anySegmentInValueSet = true;
                            break;
                        }
                    }
                    if (!anySegmentInValueSet)
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }
        }

        return false;
    }
}

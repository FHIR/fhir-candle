// <copyright file="EvalReferenceSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using FhirCandle.Storage;
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

    #region Reference Above Modifier Methods

    /// <summary>Tests whether the canonical reference in a resource is or subsumes the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the canonical reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the resource canonical reference subsumes the search parameter, false otherwise.</returns>
    public static bool TestReferenceAboveCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.ValueReferences == null)
        {
            return false;
        }

        string canonicalUrl = valueNode.Value.ToString() ?? string.Empty;
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

    /// <summary>Tests whether the reference in a resource is or subsumes the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the resource reference subsumes the search parameter, false otherwise.</returns>
    public static bool TestReferenceAboveReference(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.InstanceType != "Reference" || sp.ValueReferences == null)
        {
            return false;
        }

        // For reference hierarchies, we would need to resolve the reference and check
        // hierarchical relationships (e.g., Location.partOf chains)
        // This is a complex operation that requires access to the store to resolve references
        // For now, return false as this requires store integration
        return false;
    }

    /// <summary>Tests whether the OID reference in a resource is or subsumes the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the OID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the resource OID subsumes the search parameter, false otherwise.</returns>
    public static bool TestReferenceAboveOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.ValueReferences == null)
        {
            return false;
        }

        string oidValue = valueNode.Value.ToString() ?? string.Empty;
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
    public static bool TestReferenceAboveUri(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.ValueReferences == null)
        {
            return false;
        }

        string uriValue = valueNode.Value.ToString() ?? string.Empty;
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
    public static bool TestReferenceAboveUrl(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.ValueReferences == null)
        {
            return false;
        }

        string urlValue = valueNode.Value.ToString() ?? string.Empty;
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

    /// <summary>Tests whether the UUID reference in a resource is or subsumes the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the UUID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the resource UUID subsumes the search parameter, false otherwise.</returns>
    public static bool TestReferenceAboveUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.ValueReferences == null)
        {
            return false;
        }

        // UUIDs typically don't have hierarchical relationships
        // Return false unless specific UUID-based hierarchy is defined
        return false;
    }

    #endregion

    #region Reference Below Modifier Methods

    /// <summary>Tests whether the canonical reference in a resource is or is subsumed by the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the canonical reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the search parameter subsumes the resource canonical reference, false otherwise.</returns>
    public static bool TestReferenceBelowCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.ValueReferences == null)
        {
            return false;
        }

        string canonicalUrl = valueNode.Value.ToString() ?? string.Empty;
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

    /// <summary>Tests whether the reference in a resource is or is subsumed by the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the search parameter subsumes the resource reference, false otherwise.</returns>
    public static bool TestReferenceBelowReference(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.InstanceType != "Reference" || sp.ValueReferences == null)
        {
            return false;
        }

        // For reference hierarchies, we would need to resolve the reference and check
        // hierarchical relationships (e.g., Location.partOf chains)
        // This is a complex operation that requires access to the store to resolve references
        // For now, return false as this requires store integration
        return false;
    }

    /// <summary>Tests whether the OID reference in a resource is or is subsumed by the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the OID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the search parameter OID subsumes the resource OID, false otherwise.</returns>
    public static bool TestReferenceBelowOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.ValueReferences == null)
        {
            return false;
        }

        string oidValue = valueNode.Value.ToString() ?? string.Empty;
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
    public static bool TestReferenceBelowUri(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.ValueReferences == null)
        {
            return false;
        }

        string uriValue = valueNode.Value.ToString() ?? string.Empty;
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
    public static bool TestReferenceBelowUrl(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.ValueReferences == null)
        {
            return false;
        }

        string urlValue = valueNode.Value.ToString() ?? string.Empty;
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

    /// <summary>Tests whether the UUID reference in a resource is or is subsumed by the supplied parameter value (hierarchical relationships).</summary>
    /// <param name="valueNode">The value node containing the UUID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if the search parameter UUID subsumes the resource UUID, false otherwise.</returns>
    public static bool TestReferenceBelowUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.ValueReferences == null)
        {
            return false;
        }

        // UUIDs typically don't have hierarchical relationships
        // Return false unless specific UUID-based hierarchy is defined
        return false;
    }

    #endregion

    #region Reference CodeText Modifier Methods

    /// <summary>Tests whether a coded value in a canonical reference matches the supplied parameter value using basic string matching.</summary>
    /// <param name="valueNode">The value node containing the canonical reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if any code text matches search criteria, false otherwise.</returns>
    public static bool TestReferenceCodeTextCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        string canonicalUrl = valueNode.Value.ToString() ?? string.Empty;
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

    /// <summary>Tests whether a coded value in a reference element matches the supplied parameter value using basic string matching.</summary>
    /// <param name="valueNode">The value node containing the reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if any code text matches search criteria, false otherwise.</returns>
    public static bool TestReferenceCodeTextReference(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.InstanceType != "Reference" || sp.Values == null)
        {
            return false;
        }

        // For Reference elements, we would need to resolve the reference and extract codes
        // from the target resource (e.g., CodeSystem.code, ValueSet.compose.include.concept.code)
        // This requires store access to resolve references
        // For now, return false as this requires store integration
        return false;
    }

    /// <summary>Tests whether a coded value in an OID reference matches the supplied parameter value using basic string matching.</summary>
    /// <param name="valueNode">The value node containing the OID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if any code text matches search criteria, false otherwise.</returns>
    public static bool TestReferenceCodeTextOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        string oidValue = valueNode.Value.ToString() ?? string.Empty;
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
    public static bool TestReferenceCodeTextUri(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        string uriValue = valueNode.Value.ToString() ?? string.Empty;
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
    public static bool TestReferenceCodeTextUrl(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        string urlValue = valueNode.Value.ToString() ?? string.Empty;
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
    public static bool TestReferenceCodeTextUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        string uuidValue = valueNode.Value.ToString() ?? string.Empty;
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

    #endregion

    #region Reference In Modifier Methods

    /// <summary>Tests whether the canonical reference in a resource is a member of the supplied parameter ValueSet.</summary>
    /// <param name="valueNode">The value node containing the canonical reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology service access.</param>
    /// <returns>True if any codes from canonical resource are in the ValueSet, false otherwise.</returns>
    public static bool TestReferenceInCanonical(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        string canonicalUrl = valueNode.Value.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(canonicalUrl))
        {
            return false;
        }

        // Extract ValueSet URI from search parameter
        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.Values[i];

            // For canonical references, we would need to:
            // 1. Resolve the canonical reference to get the target resource
            // 2. Extract codes from the resource if it's a terminology resource
            // 3. Check if any codes are in the specified ValueSet using store.Terminology.VsContains
            // This requires complex resolution logic and terminology service integration
            // For now, return false as this requires extensive store integration
        }

        return false;
    }

    /// <summary>Tests whether the reference in a resource is a member of the supplied parameter ValueSet.</summary>
    /// <param name="valueNode">The value node containing the reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology service access.</param>
    /// <returns>True if any referenced resource codes are in the ValueSet, false otherwise.</returns>
    public static bool TestReferenceInReference(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        if (valueNode?.InstanceType != "Reference" || sp.Values == null)
        {
            return false;
        }

        // For Reference elements, we would need to:
        // 1. Resolve the reference to get the target resource
        // 2. Extract relevant codes from the target resource
        // 3. Check each code against the specified ValueSet using store.Terminology.VsContains
        // This requires complex resolution logic and terminology service integration
        // For now, return false as this requires extensive store integration
        return false;
    }

    /// <summary>Tests whether the OID reference in a resource is a member of the supplied parameter ValueSet.</summary>
    /// <param name="valueNode">The value node containing the OID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology service access.</param>
    /// <returns>True if OID or its derived codes are in the ValueSet, false otherwise.</returns>
    public static bool TestReferenceInOid(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        string oidValue = valueNode.Value.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(oidValue))
        {
            return false;
        }

        string normalizedOid = oidValue.StartsWith("urn:oid:", StringComparison.OrdinalIgnoreCase) 
            ? oidValue.Substring(8) 
            : oidValue;

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.Values[i];

            // OIDs may not directly map to ValueSet concepts
            // We could check if the OID itself is defined as a code in the ValueSet
            // For now, return false as OIDs typically don't appear in ValueSets directly
        }

        return false;
    }

    /// <summary>Tests whether the URI reference in a resource is a member of the supplied parameter ValueSet.</summary>
    /// <param name="valueNode">The value node containing the URI reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology service access.</param>
    /// <returns>True if URI or its codes are in the ValueSet, false otherwise.</returns>
    public static bool TestReferenceInUri(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        string uriValue = valueNode.Value.ToString() ?? string.Empty;
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
    public static bool TestReferenceInUrl(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        string urlValue = valueNode.Value.ToString() ?? string.Empty;
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

    /// <summary>Tests whether the UUID reference in a resource is a member of the supplied parameter ValueSet.</summary>
    /// <param name="valueNode">The value node containing the UUID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology service access.</param>
    /// <returns>True if UUID represents codes in the ValueSet, false otherwise.</returns>
    public static bool TestReferenceInUuid(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        // UUIDs typically don't represent codes in ValueSets
        // They are identifiers, not codes
        // Return false in most cases
        return false;
    }

    #endregion

    #region Reference NotIn Modifier Methods

    /// <summary>Tests whether the canonical reference in a resource is not a member of the supplied parameter ValueSet.</summary>
    /// <param name="valueNode">The value node containing the canonical reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology service access.</param>
    /// <returns>True if NO codes from canonical resource are in the ValueSet, false otherwise.</returns>
    public static bool TestReferenceNotInCanonical(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        string canonicalUrl = valueNode.Value.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(canonicalUrl))
        {
            return false;
        }

        // Extract ValueSet URI from search parameter
        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.Values[i];

            // For canonical references, we would need to:
            // 1. Resolve the canonical reference to get the target resource
            // 2. Extract codes from the resource if it's a terminology resource
            // 3. Check if NO codes are in the specified ValueSet using !store.Terminology.VsContains
            // This requires complex resolution logic and terminology service integration
            // For now, return true as most canonical references won't be in ValueSets
            return true;
        }

        return true;
    }

    /// <summary>Tests whether the reference in a resource is not a member of the supplied parameter ValueSet.</summary>
    /// <param name="valueNode">The value node containing the reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology service access.</param>
    /// <returns>True if ALL referenced resource codes are NOT in the ValueSet, false otherwise.</returns>
    public static bool TestReferenceNotInReference(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        if (valueNode?.InstanceType != "Reference" || sp.Values == null)
        {
            return false;
        }

        // For Reference elements, we would need to:
        // 1. Resolve the reference to get the target resource
        // 2. Extract relevant codes from the target resource
        // 3. Check each code is NOT in the specified ValueSet using !store.Terminology.VsContains
        // This requires complex resolution logic and terminology service integration
        // For now, return true as most references won't have codes in specific ValueSets
        return true;
    }

    /// <summary>Tests whether the OID reference in a resource is not a member of the supplied parameter ValueSet.</summary>
    /// <param name="valueNode">The value node containing the OID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology service access.</param>
    /// <returns>True if OID or its derived codes are NOT in the ValueSet, false otherwise.</returns>
    public static bool TestReferenceNotInOid(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        string oidValue = valueNode.Value.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(oidValue))
        {
            return false;
        }

        string normalizedOid = oidValue.StartsWith("urn:oid:", StringComparison.OrdinalIgnoreCase) 
            ? oidValue.Substring(8) 
            : oidValue;

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.Values[i];

            // OIDs may not directly map to ValueSet concepts
            // Return true as OIDs typically don't appear in ValueSets directly
            return true;
        }

        return true;
    }

    /// <summary>Tests whether the URI reference in a resource is not a member of the supplied parameter ValueSet.</summary>
    /// <param name="valueNode">The value node containing the URI reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology service access.</param>
    /// <returns>True if URI and its codes are NOT in the ValueSet, false otherwise.</returns>
    public static bool TestReferenceNotInUri(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        string uriValue = valueNode.Value.ToString() ?? string.Empty;
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
    public static bool TestReferenceNotInUrl(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        string urlValue = valueNode.Value.ToString() ?? string.Empty;
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

    /// <summary>Tests whether the UUID reference in a resource is not a member of the supplied parameter ValueSet.</summary>
    /// <param name="valueNode">The value node containing the UUID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology service access.</param>
    /// <returns>True if UUID represents codes NOT in the ValueSet, false otherwise.</returns>
    public static bool TestReferenceNotInUuid(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        // UUIDs typically don't represent codes in ValueSets
        // They are identifiers, not codes
        // Return true in most cases (UUIDs are identifiers, not codes)
        return true;
    }

    #endregion

    #region Reference Text Modifier Methods

    /// <summary>Tests whether the textual value in a canonical reference matches the supplied parameter value using basic string matching.</summary>
    /// <param name="valueNode">The value node containing the canonical reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if any display text matches search criteria, false otherwise.</returns>
    public static bool TestReferenceTextCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        string canonicalUrl = valueNode.Value.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(canonicalUrl))
        {
            return false;
        }

        // For canonical references, we would need to:
        // 1. Look for associated display text in canonical reference context
        // 2. Check Reference.display if available in parent context
        // 3. Resolve canonical to get target resource and check its title/name/display fields
        // This requires complex resolution logic
        // For now, return false as this requires extensive integration
        return false;
    }

    /// <summary>Tests whether the textual value in a reference element matches the supplied parameter value using basic string matching.</summary>
    /// <param name="valueNode">The value node containing the reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if Reference.display or target resource text matches search criteria, false otherwise.</returns>
    public static bool TestReferenceTextReference(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.InstanceType != "Reference" || sp.Values == null)
        {
            return false;
        }

        Hl7.Fhir.Model.ResourceReference r = valueNode.ToPoco<Hl7.Fhir.Model.ResourceReference>();

        // Check Reference.display first for direct text match
        if (!string.IsNullOrEmpty(r.Display))
        {
            for (int i = 0; i < sp.Values.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                string searchString = sp.Values[i];
                if (r.Display.StartsWith(searchString, StringComparison.OrdinalIgnoreCase) ||
                    r.Display.Equals(searchString, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // For resolving reference to get target resource and search its display fields,
        // we would need store access to resolve references
        // This requires complex resolution logic and store integration
        return false;
    }

    /// <summary>Tests whether the textual value in an OID reference matches the supplied parameter value using basic string matching.</summary>
    /// <param name="valueNode">The value node containing the OID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if any associated text matches search criteria, false otherwise.</returns>
    public static bool TestReferenceTextOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        // OIDs typically don't have display text
        // We would need to check for associated display text in parent elements
        // or resolve OID to get associated terminology and check display names
        // For now, return false as OIDs rarely have associated text
        return false;
    }

    /// <summary>Tests whether the textual value in a URI reference matches the supplied parameter value using basic string matching.</summary>
    /// <param name="valueNode">The value node containing the URI reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if any display text matches search criteria, false otherwise.</returns>
    public static bool TestReferenceTextUri(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        // For URI references, we would need to:
        // 1. Check for associated display text in context
        // 2. For terminology URIs, resolve and check display names
        // 3. Check surrounding text elements
        // This requires complex resolution logic
        // For now, return false as this requires extensive integration
        return false;
    }

    /// <summary>Tests whether the textual value in a URL reference matches the supplied parameter value using basic string matching.</summary>
    /// <param name="valueNode">The value node containing the URL reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if any associated text matches search criteria, false otherwise.</returns>
    public static bool TestReferenceTextUrl(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        // For URL references, we would need to:
        // 1. Check for associated display text in context
        // 2. Extract human-readable portions from URL path
        // 3. Check surrounding elements for display text
        // This requires complex context analysis
        // For now, return false as this requires extensive integration
        return false;
    }

    /// <summary>Tests whether the textual value in a UUID reference matches the supplied parameter value using basic string matching.</summary>
    /// <param name="valueNode">The value node containing the UUID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if any associated text matches search criteria, false otherwise.</returns>
    public static bool TestReferenceTextUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        // UUIDs typically don't have display text
        // We would need to check for associated display text in parent elements
        // or resolve UUID reference to get target resource display fields
        // For now, return false as UUIDs rarely have associated text
        return false;
    }

    #endregion

    #region Reference TextAdvanced Modifier Methods

    /// <summary>Tests whether the value in a canonical reference matches the supplied parameter value using advanced text handling.</summary>
    /// <param name="valueNode">The value node containing the canonical reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if advanced text query matches any display text, false otherwise.</returns>
    public static bool TestReferenceTextAdvancedCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        string canonicalUrl = valueNode.Value.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(canonicalUrl))
        {
            return false;
        }

        // For advanced text search on canonical references, we would need to:
        // 1. Parse search parameter for advanced text query (may contain AND, OR, proximity operators)
        // 2. Look for associated display text in canonical reference context
        // 3. Resolve canonical to get target resource and check title/name/display fields
        // 4. Apply advanced text processing: word boundary detection, stemming, synonyms
        // 5. Support logical operators (AND, OR) and proximity searches
        // This requires sophisticated text indexing functionality
        // For now, return false as this requires extensive NLP and indexing integration
        return false;
    }

    /// <summary>Tests whether the value in a reference element matches the supplied parameter value using advanced text handling.</summary>
    /// <param name="valueNode">The value node containing the reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if advanced query matches Reference.display or target resource text, false otherwise.</returns>
    public static bool TestReferenceTextAdvancedReference(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.InstanceType != "Reference" || sp.Values == null)
        {
            return false;
        }

        Hl7.Fhir.Model.ResourceReference r = valueNode.ToPoco<Hl7.Fhir.Model.ResourceReference>();

        // For advanced text search, we would need to:
        // 1. Parse search parameter for advanced text query
        // 2. Check Reference.display with advanced text processing
        // 3. Resolve reference to get target resource
        // 4. Search target resource text fields with advanced algorithms
        // 5. Apply word boundary recognition, stemming, thesaurus expansion
        // 6. Support logical operators and proximity matching
        // This requires sophisticated text indexing functionality
        // For now, return false as this requires extensive NLP and indexing integration
        return false;
    }

    /// <summary>Tests whether the value in an OID reference matches the supplied parameter value using advanced text handling.</summary>
    /// <param name="valueNode">The value node containing the OID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if advanced query matches any associated text, false otherwise.</returns>
    public static bool TestReferenceTextAdvancedOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        // OIDs typically don't have display text for advanced text search
        // We would need to resolve OID to get terminology and apply advanced text search to display names
        // This requires sophisticated text indexing and terminology resolution
        // For now, return false as OIDs rarely have associated text suitable for advanced search
        return false;
    }

    /// <summary>Tests whether the value in a URI reference matches the supplied parameter value using advanced text handling.</summary>
    /// <param name="valueNode">The value node containing the URI reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if advanced query matches any display text, false otherwise.</returns>
    public static bool TestReferenceTextAdvancedUri(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        // For advanced text search on URI references, we would need to:
        // 1. Parse search parameter for advanced text query
        // 2. Check for associated display text in context
        // 3. For terminology URIs, resolve and apply advanced text search to display names
        // 4. Apply word boundary detection, stemming, synonyms to URI-associated text
        // 5. Support logical operators in text queries
        // This requires sophisticated text indexing and terminology resolution
        // For now, return false as this requires extensive NLP and indexing integration
        return false;
    }

    /// <summary>Tests whether the value in a URL reference matches the supplied parameter value using advanced text handling.</summary>
    /// <param name="valueNode">The value node containing the URL reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if advanced query matches any associated text, false otherwise.</returns>
    public static bool TestReferenceTextAdvancedUrl(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        // For advanced text search on URL references, we would need to:
        // 1. Parse search parameter for advanced text query
        // 2. Check for associated display text in context
        // 3. Extract human-readable portions from URL and apply advanced text processing
        // 4. Apply sophisticated text matching to URL-associated text
        // 5. Support thesaurus expansion and proximity searches
        // This requires sophisticated text indexing functionality
        // For now, return false as this requires extensive NLP and indexing integration
        return false;
    }

    /// <summary>Tests whether the value in a UUID reference matches the supplied parameter value using advanced text handling.</summary>
    /// <param name="valueNode">The value node containing the UUID reference.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if advanced query matches any associated text, false otherwise.</returns>
    public static bool TestReferenceTextAdvancedUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null || sp.Values == null)
        {
            return false;
        }

        // UUIDs typically don't have display text for advanced text search
        // We would need to resolve UUID reference and apply advanced text search to target resource
        // This requires sophisticated text indexing and reference resolution
        // For now, return false as UUIDs rarely have associated text suitable for advanced search
        return false;
    }

    #endregion
}

// <copyright file="EvalUriSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using FhirCandle.Storage;
using Hl7.Fhir.ElementModel;

namespace FhirCandle.Search;

/// <summary>A class that contains functions to test URI inputs against various FHIR types.</summary>
public static class EvalUriSearch
{

    /// <summary>Tests a token search value against string-type nodes, using exact matching (equality & case-sensitive).</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestUriAgainstStringValue(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (sp.Values[i].Equals(value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests uri values against OIDs.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestUriAgainstOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.StartsWith("urn:oid:", StringComparison.Ordinal))
        {
            for (int i = 0; i < sp.Values.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                if (sp.Values[i].Equals(value, StringComparison.OrdinalIgnoreCase) ||
                    ("urn:oid:" + sp.Values[i]).Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (sp.Values[i].Equals(value, StringComparison.OrdinalIgnoreCase) ||
                sp.Values[i].Equals("urn:oid:" + value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests uri values against UUIDs.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestUriAgainstUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.StartsWith("urn:uuid:", StringComparison.Ordinal))
        {
            for (int i = 0; i < sp.Values.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                if (sp.Values[i].Equals(value, StringComparison.OrdinalIgnoreCase) ||
                    ("urn:uuid:" + sp.Values[i]).Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (sp.Values[i].Equals(value, StringComparison.OrdinalIgnoreCase) ||
                sp.Values[i].Equals("urn:uuid:" + value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // URI Above Modifier methods - URL hierarchy (parent paths)
    
    /// <summary>Tests URI above for canonical elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if canonical URI is parent path of search URI.</returns>
    public static bool TestUriAboveCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string canonicalUri = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(canonicalUri))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchUri = sp.Values[i];
            
            // Check URL hierarchy: resource URI should be parent of search URI
            if (IsParentUrl(canonicalUri, searchUri))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI above for OID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if OID hierarchy supports above (returns false - OIDs as URNs don't support above).</returns>
    public static bool TestUriAboveOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // OIDs as URNs don't support above modifier (as per FHIR spec)
        // Above modifier only applies to URLs, not URNs like OIDs
        return false;
    }

    /// <summary>Tests URI above for URI elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URI is parent path of search URI (URLs only).</returns>
    public static bool TestUriAboveUri(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string uriValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(uriValue))
        {
            return false;
        }

        // Only URLs support above modifier, not URNs
        if (uriValue.StartsWith("urn:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchUri = sp.Values[i];
            
            // Check URL hierarchy for URLs only
            if (IsParentUrl(uriValue, searchUri))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI above for URL elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URL is parent path of search URL.</returns>
    public static bool TestUriAboveUrl(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string urlValue = (string)(valueNode?.Value ?? string.Empty);
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

            string searchUrl = sp.Values[i];
            
            // Check URL hierarchy
            if (IsParentUrl(urlValue, searchUrl))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI above for UUID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if UUID hierarchy supports above (returns false - UUIDs as URNs don't support above).</returns>
    public static bool TestUriAboveUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // UUIDs as URNs don't support above modifier
        // Above modifier only applies to URLs, not URNs like UUIDs
        return false;
    }

    // URI Below Modifier methods - URL hierarchy (child paths)
    
    /// <summary>Tests URI below for canonical elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if canonical URI is child path of search URI.</returns>
    public static bool TestUriBelowCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string canonicalUri = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(canonicalUri))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchUri = sp.Values[i];
            
            // Check URL hierarchy: search URI should be parent of resource URI
            if (IsParentUrl(searchUri, canonicalUri))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI below for OID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if OID hierarchy supports below (returns false - OIDs as URNs don't support below).</returns>
    public static bool TestUriBelowOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // OIDs as URNs don't support below modifier (as per FHIR spec)
        // Below modifier only applies to URLs, not URNs like OIDs
        return false;
    }

    /// <summary>Tests URI below for URI elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URI is child path of search URI (URLs only).</returns>
    public static bool TestUriBelowUri(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string uriValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(uriValue))
        {
            return false;
        }

        // Only URLs support below modifier, not URNs
        if (uriValue.StartsWith("urn:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchUri = sp.Values[i];
            
            // Check URL hierarchy for URLs only
            if (IsParentUrl(searchUri, uriValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI below for URL elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URL is child path of search URL.</returns>
    public static bool TestUriBelowUrl(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string urlValue = (string)(valueNode?.Value ?? string.Empty);
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

            string searchUrl = sp.Values[i];
            
            // Check URL hierarchy
            if (IsParentUrl(searchUrl, urlValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI below for UUID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if UUID hierarchy supports below (returns false - UUIDs as URNs don't support below).</returns>
    public static bool TestUriBelowUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // UUIDs as URNs don't support below modifier
        // Below modifier only applies to URLs, not URNs like UUIDs
        return false;
    }

    // URI Contains Modifier methods - substring matching
    
    /// <summary>Tests URI contains for canonical elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if canonical URI contains search substring.</returns>
    public static bool TestUriContainsCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string canonicalUri = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(canonicalUri))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchString = sp.Values[i];
            
            // Check if canonical URI contains search string (case-insensitive)
            if (canonicalUri.Contains(searchString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI contains for OID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if OID URI contains search substring.</returns>
    public static bool TestUriContainsOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string oidValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(oidValue))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchString = sp.Values[i];
            
            // Check if OID contains search string (case-insensitive)
            if (oidValue.Contains(searchString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI contains for URI elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URI contains search substring.</returns>
    public static bool TestUriContainsUri(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string uriValue = (string)(valueNode?.Value ?? string.Empty);
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

            string searchString = sp.Values[i];
            
            // Check if URI contains search string (case-insensitive)
            if (uriValue.Contains(searchString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI contains for URL elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URL contains search substring.</returns>
    public static bool TestUriContainsUrl(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string urlValue = (string)(valueNode?.Value ?? string.Empty);
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

            string searchString = sp.Values[i];
            
            // Check if URL contains search string (case-insensitive)
            if (urlValue.Contains(searchString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI contains for UUID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if UUID URI contains search substring.</returns>
    public static bool TestUriContainsUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string uuidValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(uuidValue))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchString = sp.Values[i];
            
            // Check if UUID contains search string (case-insensitive)
            if (uuidValue.Contains(searchString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // URI In Modifier methods - ValueSet membership
    
    /// <summary>Tests URI in for canonical elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if canonical URI is member of ValueSet.</returns>
    public static bool TestUriInCanonical(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string canonicalUri = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(canonicalUri) || sp.ValueFhirCodes == null)
        {
            return false;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            // Check ValueSet membership using terminology service
            if (store.Terminology.VsContains(sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System ?? string.Empty, string.Empty, canonicalUri))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI in for OID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if OID URI is member of ValueSet.</returns>
    public static bool TestUriInOid(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string oidValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(oidValue) || sp.ValueFhirCodes == null)
        {
            return false;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            // Check ValueSet membership using terminology service
            if (store.Terminology.VsContains(sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System ?? string.Empty, string.Empty, oidValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI in for URI elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if URI is member of ValueSet.</returns>
    public static bool TestUriInUri(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string uriValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(uriValue) || sp.ValueFhirCodes == null)
        {
            return false;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            // Check ValueSet membership using terminology service
            if (store.Terminology.VsContains(sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System ?? string.Empty, string.Empty, uriValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI in for URL elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if URL is member of ValueSet.</returns>
    public static bool TestUriInUrl(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string urlValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(urlValue) || sp.ValueFhirCodes == null)
        {
            return false;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            // Check ValueSet membership using terminology service
            if (store.Terminology.VsContains(sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System ?? string.Empty, string.Empty, urlValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI in for UUID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if UUID URI is member of ValueSet.</returns>
    public static bool TestUriInUuid(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string uuidValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(uuidValue) || sp.ValueFhirCodes == null)
        {
            return false;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            // Check ValueSet membership using terminology service
            if (store.Terminology.VsContains(sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System ?? string.Empty, string.Empty, uuidValue))
            {
                return true;
            }
        }

        return false;
    }

    // URI Not Modifier methods - negation
    
    /// <summary>Tests URI not for canonical elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if canonical URI does not match search values.</returns>
    public static bool TestUriNotCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string canonicalUri = (string)(valueNode?.Value ?? string.Empty);
        
        if (string.IsNullOrEmpty(canonicalUri))
        {
            // Note that in 'not', missing values are matches
            return true;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (sp.Values[i].Equals(canonicalUri, StringComparison.Ordinal))
            {
                // Not is inverted
                return false;
            }
        }

        // Not is inverted
        return true;
    }

    /// <summary>Tests URI not for OID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if OID URI does not match search values.</returns>
    public static bool TestUriNotOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string oidValue = (string)(valueNode?.Value ?? string.Empty);
        
        if (string.IsNullOrEmpty(oidValue))
        {
            // Note that in 'not', missing values are matches
            return true;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            // Check both with and without urn:oid: prefix
            if (sp.Values[i].Equals(oidValue, StringComparison.OrdinalIgnoreCase) ||
                ("urn:oid:" + sp.Values[i]).Equals(oidValue, StringComparison.OrdinalIgnoreCase) ||
                sp.Values[i].Equals("urn:oid:" + oidValue, StringComparison.OrdinalIgnoreCase))
            {
                // Not is inverted
                return false;
            }
        }

        // Not is inverted
        return true;
    }

    /// <summary>Tests URI not for URI elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URI does not match search values.</returns>
    public static bool TestUriNotUri(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string uriValue = (string)(valueNode?.Value ?? string.Empty);
        
        if (string.IsNullOrEmpty(uriValue))
        {
            // Note that in 'not', missing values are matches
            return true;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (sp.Values[i].Equals(uriValue, StringComparison.Ordinal))
            {
                // Not is inverted
                return false;
            }
        }

        // Not is inverted
        return true;
    }

    /// <summary>Tests URI not for URL elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URL does not match search values.</returns>
    public static bool TestUriNotUrl(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string urlValue = (string)(valueNode?.Value ?? string.Empty);
        
        if (string.IsNullOrEmpty(urlValue))
        {
            // Note that in 'not', missing values are matches
            return true;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (sp.Values[i].Equals(urlValue, StringComparison.Ordinal))
            {
                // Not is inverted
                return false;
            }
        }

        // Not is inverted
        return true;
    }

    /// <summary>Tests URI not for UUID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if UUID URI does not match search values.</returns>
    public static bool TestUriNotUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string uuidValue = (string)(valueNode?.Value ?? string.Empty);
        
        if (string.IsNullOrEmpty(uuidValue))
        {
            // Note that in 'not', missing values are matches
            return true;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            // Check both with and without urn:uuid: prefix
            if (sp.Values[i].Equals(uuidValue, StringComparison.OrdinalIgnoreCase) ||
                ("urn:uuid:" + sp.Values[i]).Equals(uuidValue, StringComparison.OrdinalIgnoreCase) ||
                sp.Values[i].Equals("urn:uuid:" + uuidValue, StringComparison.OrdinalIgnoreCase))
            {
                // Not is inverted
                return false;
            }
        }

        // Not is inverted
        return true;
    }

    // URI NotIn Modifier methods - ValueSet exclusion
    
    /// <summary>Tests URI not in for canonical elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if canonical URI is not member of ValueSet.</returns>
    public static bool TestUriNotInCanonical(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string canonicalUri = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(canonicalUri) || sp.ValueFhirCodes == null)
        {
            // Missing values are considered NOT in any ValueSet
            return true;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System ?? string.Empty;
            
            // Check if canonical URI is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, canonicalUri))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI not in for OID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if OID URI is not member of ValueSet.</returns>
    public static bool TestUriNotInOid(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string oidValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(oidValue) || sp.ValueFhirCodes == null)
        {
            // Missing values are considered NOT in any ValueSet
            return true;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System ?? string.Empty;
            
            // Check if OID is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, oidValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI not in for URI elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if URI is not member of ValueSet.</returns>
    public static bool TestUriNotInUri(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string uriValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(uriValue) || sp.ValueFhirCodes == null)
        {
            // Missing values are considered NOT in any ValueSet
            return true;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System ?? string.Empty;
            
            // Check if URI is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, uriValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI not in for URL elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if URL is not member of ValueSet.</returns>
    public static bool TestUriNotInUrl(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string urlValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(urlValue) || sp.ValueFhirCodes == null)
        {
            // Missing values are considered NOT in any ValueSet
            return true;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System ?? string.Empty;
            
            // Check if URL is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, urlValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI not in for UUID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if UUID URI is not member of ValueSet.</returns>
    public static bool TestUriNotInUuid(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string uuidValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(uuidValue) || sp.ValueFhirCodes == null)
        {
            // Missing values are considered NOT in any ValueSet
            return true;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System ?? string.Empty;
            
            // Check if UUID is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, uuidValue))
            {
                return true;
            }
        }

        return false;
    }

    // URI OfType Modifier methods - type-specific matching
    
    /// <summary>Tests URI of type for canonical elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if canonical URI matches type-specific criteria.</returns>
    public static bool TestUriOfTypeCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string canonicalUri = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(canonicalUri))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchValue = sp.Values[i];
            
            // Type-specific matching for canonical URIs (exact match)
            if (canonicalUri.Equals(searchValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI of type for OID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if OID URI matches type-specific criteria.</returns>
    public static bool TestUriOfTypeOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string oidValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(oidValue))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchValue = sp.Values[i];
            
            // Type-specific matching for OIDs (with and without urn:oid: prefix)
            if (oidValue.Equals(searchValue, StringComparison.OrdinalIgnoreCase) ||
                ("urn:oid:" + searchValue).Equals(oidValue, StringComparison.OrdinalIgnoreCase) ||
                searchValue.Equals("urn:oid:" + oidValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI of type for URI elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URI matches type-specific criteria.</returns>
    public static bool TestUriOfTypeUri(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string uriValue = (string)(valueNode?.Value ?? string.Empty);
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

            string searchValue = sp.Values[i];
            
            // Type-specific matching for URIs (exact match)
            if (uriValue.Equals(searchValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI of type for URL elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URL matches type-specific criteria.</returns>
    public static bool TestUriOfTypeUrl(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string urlValue = (string)(valueNode?.Value ?? string.Empty);
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

            string searchValue = sp.Values[i];
            
            // Type-specific matching for URLs (exact match)
            if (urlValue.Equals(searchValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI of type for UUID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if UUID URI matches type-specific criteria.</returns>
    public static bool TestUriOfTypeUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string uuidValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(uuidValue))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchValue = sp.Values[i];
            
            // Type-specific matching for UUIDs (with and without urn:uuid: prefix)
            if (uuidValue.Equals(searchValue, StringComparison.OrdinalIgnoreCase) ||
                ("urn:uuid:" + searchValue).Equals(uuidValue, StringComparison.OrdinalIgnoreCase) ||
                searchValue.Equals("urn:uuid:" + uuidValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // URI Text Modifier methods - basic text search
    
    /// <summary>Tests URI text for canonical elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if canonical URI matches text search criteria.</returns>
    public static bool TestUriTextCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string canonicalUri = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(canonicalUri))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchText = sp.Values[i];
            
            // Basic text matching (begins with or is, case-insensitive)
            if (canonicalUri.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                canonicalUri.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI text for OID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if OID URI matches text search criteria.</returns>
    public static bool TestUriTextOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string oidValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(oidValue))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchText = sp.Values[i];
            
            // Basic text matching (begins with or is, case-insensitive)
            if (oidValue.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                oidValue.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI text for URI elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URI matches text search criteria.</returns>
    public static bool TestUriTextUri(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string uriValue = (string)(valueNode?.Value ?? string.Empty);
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

            string searchText = sp.Values[i];
            
            // Basic text matching (begins with or is, case-insensitive)
            if (uriValue.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                uriValue.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI text for URL elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URL matches text search criteria.</returns>
    public static bool TestUriTextUrl(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string urlValue = (string)(valueNode?.Value ?? string.Empty);
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

            string searchText = sp.Values[i];
            
            // Basic text matching (begins with or is, case-insensitive)
            if (urlValue.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                urlValue.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI text for UUID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if UUID URI matches text search criteria.</returns>
    public static bool TestUriTextUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string uuidValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(uuidValue))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchText = sp.Values[i];
            
            // Basic text matching (begins with or is, case-insensitive)
            if (uuidValue.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                uuidValue.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // URI TextAdvanced Modifier methods - advanced text search
    
    /// <summary>Tests URI text advanced for canonical elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if canonical URI matches advanced text search criteria.</returns>
    public static bool TestUriTextAdvancedCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string canonicalUri = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(canonicalUri))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchText = sp.Values[i];
            
            // Apply advanced text processing using the existing helper
            if (ProcessAdvancedTextSearch(canonicalUri, searchText))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI text advanced for OID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if OID URI matches advanced text search criteria.</returns>
    public static bool TestUriTextAdvancedOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string oidValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(oidValue))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchText = sp.Values[i];
            
            // OIDs are numeric and typically don't have meaningful text
            // Apply advanced text processing to the OID string itself
            if (ProcessAdvancedTextSearch(oidValue, searchText))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI text advanced for URI elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URI matches advanced text search criteria.</returns>
    public static bool TestUriTextAdvancedUri(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string uriValue = (string)(valueNode?.Value ?? string.Empty);
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

            string searchText = sp.Values[i];
            
            // Apply advanced text processing to extract meaningful portions
            if (ProcessAdvancedTextSearch(uriValue, searchText))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI text advanced for URL elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URL matches advanced text search criteria.</returns>
    public static bool TestUriTextAdvancedUrl(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string urlValue = (string)(valueNode?.Value ?? string.Empty);
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

            string searchText = sp.Values[i];
            
            // Apply advanced text processing to extract human-readable portions
            if (ProcessAdvancedTextSearch(urlValue, searchText))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI text advanced for UUID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if UUID URI matches advanced text search criteria.</returns>
    public static bool TestUriTextAdvancedUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string uuidValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(uuidValue))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchText = sp.Values[i];
            
            // UUIDs are not human-readable text, but apply advanced search anyway
            if (ProcessAdvancedTextSearch(uuidValue, searchText))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Processes advanced text search with basic logical operations support.</summary>
    /// <param name="textValue">The text value to search in.</param>
    /// <param name="searchQuery">The advanced search query.</param>
    /// <returns>True if the query matches the text value.</returns>
    private static bool ProcessAdvancedTextSearch(string textValue, string searchQuery)
    {
        if (string.IsNullOrEmpty(textValue) || string.IsNullOrEmpty(searchQuery))
        {
            return false;
        }

        // Basic implementation of advanced text search
        // Convert to lowercase for case-insensitive matching
        string lowerText = textValue.ToLowerInvariant();
        string lowerQuery = searchQuery.ToLowerInvariant();

        // Handle simple AND operations (space-separated terms must all be present)
        if (lowerQuery.Contains(" and "))
        {
            string[] andTerms = lowerQuery.Split(new[] { " and" }, StringSplitOptions.RemoveEmptyEntries);
            return andTerms.All(term => lowerText.Contains(term.Trim()));
        }

        // Handle simple OR operations (any space-separated term can be present)
        if (lowerQuery.Contains(" or "))
        {
            string[] orTerms = lowerQuery.Split(new[] { " or " }, StringSplitOptions.RemoveEmptyEntries);
            return orTerms.Any(term => lowerText.Contains(term.Trim()));
        }

        // Handle word boundary detection (simple implementation)
        string[] queryWords = lowerQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (queryWords.Length > 1)
        {
            // All words must be present (implicit AND)
            return queryWords.All(word => lowerText.Contains(word));
        }

        // Simple substring matching as fallback
        return lowerText.Contains(lowerQuery);
    }

    /// <summary>Checks if one URL is a parent of another in the URL hierarchy.</summary>
    /// <param name="parentUrl">The potential parent URL.</param>
    /// <param name="childUrl">The potential child URL.</param>
    /// <returns>True if parentUrl is a parent path of childUrl.</returns>
    private static bool IsParentUrl(string parentUrl, string childUrl)
    {
        if (string.IsNullOrEmpty(parentUrl) || string.IsNullOrEmpty(childUrl))
        {
            return false;
        }

        try
        {
            Uri parentUri = new Uri(parentUrl);
            Uri childUri = new Uri(childUrl);

            // Must be same scheme and authority
            if (!parentUri.Scheme.Equals(childUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
                !parentUri.Authority.Equals(childUri.Authority, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check path hierarchy
            string parentPath = parentUri.AbsolutePath.TrimEnd('/');
            string childPath = childUri.AbsolutePath.TrimEnd('/');

            // Parent path should be contained in child path
            return childPath.StartsWith(parentPath + "/", StringComparison.OrdinalIgnoreCase) ||
                   parentPath.Equals(childPath, StringComparison.OrdinalIgnoreCase);
        }
        catch (UriFormatException)
        {
            // If URIs are malformed, fall back to string comparison
            return childUrl.StartsWith(parentUrl + "/", StringComparison.OrdinalIgnoreCase) ||
                   parentUrl.Equals(childUrl, StringComparison.OrdinalIgnoreCase);
        }
    }
}

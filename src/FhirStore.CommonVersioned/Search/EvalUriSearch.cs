// <copyright file="EvalUriSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using FhirCandle.Storage;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;

namespace FhirCandle.Search;

/// <summary>A class that contains functions to test URI inputs against various FHIR types.</summary>
public static class EvalUriSearch
{

    /// <summary>Tests a token search oidValue against string-type nodes, using exact matching (equality & case-sensitive).</summary>
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestUriAgainstStringValue(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? stringValue = valueNode.Poco switch
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

        if (string.IsNullOrEmpty(stringValue))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (sp.Values[i].Equals(stringValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests uri values against OIDs.</summary>
    /// <param name="valueNode">The oid node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestUriAgainstOid(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return true;
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
            return true;
        }

        if (oidValue.StartsWith("urn:oid:", StringComparison.Ordinal))
        {
            for (int i = 0; i < sp.Values.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                if (sp.Values[i].Equals(oidValue, StringComparison.OrdinalIgnoreCase) ||
                    ("urn:oid:" + sp.Values[i]).Equals(oidValue, StringComparison.OrdinalIgnoreCase))
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

            if (sp.Values[i].Equals(oidValue, StringComparison.OrdinalIgnoreCase) ||
                sp.Values[i].Equals("urn:oid:" + oidValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests uri values against UUIDs.</summary>
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestUriAgainstUuid(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return true;
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
            return true;
        }

        if (uuidValue.StartsWith("urn:uuid:", StringComparison.Ordinal))
        {
            for (int i = 0; i < sp.Values.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                if (sp.Values[i].Equals(uuidValue, StringComparison.OrdinalIgnoreCase) ||
                    ("urn:uuid:" + sp.Values[i]).Equals(uuidValue, StringComparison.OrdinalIgnoreCase))
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

            if (sp.Values[i].Equals(uuidValue, StringComparison.OrdinalIgnoreCase) ||
                sp.Values[i].Equals("urn:uuid:" + uuidValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
    
    /// <summary>Tests URI above for canonical elements.</summary>
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if canonical URI is parent path of search URI.</returns>
    public static bool TestUriAboveCanonical(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
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

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchUri = sp.Values[i];
            
            // Check URL hierarchy: resource URI should be parent of search URI
            if (IsParentUrl(canonicalUrl, searchUri))
            {
                return true;
            }
        }

        return false;
    }


    /// <summary>Tests URI above for URI elements.</summary>
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URI is parent path of search URI (URLs only).</returns>
    public static bool TestUriAboveUri(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
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
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URL is parent path of search URL.</returns>
    public static bool TestUriAboveUrl(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
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

            string searchUrl = sp.Values[i];
            
            // Check URL hierarchy
            if (IsParentUrl(urlValue, searchUrl))
            {
                return true;
            }
        }

        return false;
    }


    /// <summary>Tests URI below for canonical elements.</summary>
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if canonical URI is child path of search URI.</returns>
    public static bool TestUriBelowCanonical(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
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

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchUri = sp.Values[i];
            
            // Check URL hierarchy: search URI should be parent of resource URI
            if (IsParentUrl(searchUri, canonicalUrl))
            {
                return true;
            }
        }

        return false;
    }


    /// <summary>Tests URI below for URI elements.</summary>
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URI is child path of search URI (URLs only).</returns>
    public static bool TestUriBelowUri(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
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
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URL is child path of search URL.</returns>
    public static bool TestUriBelowUrl(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
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

            string searchUrl = sp.Values[i];
            
            // Check URL hierarchy
            if (IsParentUrl(searchUrl, urlValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI contains for canonical elements.</summary>
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if canonical URI contains search substring.</returns>
    public static bool TestUriContainsCanonical(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
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

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchString = sp.Values[i];
            
            // Check if canonical URI contains search string (case-insensitive)
            if (canonicalUrl.Contains(searchString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests URI contains for OID elements.</summary>
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if OID URI contains search substring.</returns>
    public static bool TestUriContainsOid(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
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
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URI contains search substring.</returns>
    public static bool TestUriContainsUri(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
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
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URL contains search substring.</returns>
    public static bool TestUriContainsUrl(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
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
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if UUID URI contains search substring.</returns>
    public static bool TestUriContainsUuid(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
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
    
    // URI Not Modifier methods - negation
    
    /// <summary>Tests URI not for canonical elements.</summary>
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if canonical URI does not match search values.</returns>
    public static bool TestUriNotCanonical(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return true;
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
            return true;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (sp.Values[i].Equals(canonicalUrl, StringComparison.Ordinal))
            {
                // Not is inverted
                return false;
            }
        }

        // Not is inverted
        return true;
    }

    /// <summary>Tests URI not for OID elements.</summary>
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if OID URI does not match search values.</returns>
    public static bool TestUriNotOid(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return true;
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
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URI does not match search values.</returns>
    public static bool TestUriNotUri(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return true;
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
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URL does not match search values.</returns>
    public static bool TestUriNotUrl(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return true;
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
    /// <param name="valueNode">The oidValue node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if UUID URI does not match search values.</returns>
    public static bool TestUriNotUuid(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return true;
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
    
    /// <summary>Processes advanced text search with basic logical operations support.</summary>
    /// <param name="textValue">The text oidValue to search in.</param>
    /// <param name="searchQuery">The advanced search query.</param>
    /// <returns>True if the query matches the text oidValue.</returns>
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

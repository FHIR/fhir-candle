// <copyright file="EvalTokenSearch.cs" company="Microsoft Corporation">
//     Copyright (fhirCode) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using FhirCandle.Storage;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;

namespace FhirCandle.Search;

/// <summary>A class that contains functions to test token inputs against various FHIR types.</summary>
public static class EvalTokenSearch
{
    /// <summary>Compare code and system values - note that empty values in '2' slots are considered matches against anything.</summary>
    /// <param name="s1">The first system.</param>
    /// <param name="c1">The first code.</param>
    /// <param name="s2">The second system.</param>
    /// <param name="c2">The second code.</param>
    /// <returns>True if they match, false if they do not.</returns>
    internal static bool CompareCodeWithSystem(string? s1, string? c1, string? s2, string? c2)
    {
        if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2))
        {
            if (string.IsNullOrEmpty(c2))
            {
                return true;
            }

            if (string.IsNullOrEmpty(c1))
            {
                return false;
            }

            return c1.Equals(c2, StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrEmpty(s2))
        {
            if (string.IsNullOrEmpty(c2))
            {
                return true;
            }

            if (string.IsNullOrEmpty(c1))
            {
                return false;
            }

            return c1.Equals(c2, StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrEmpty(s1))
        {
            return false;
        }

        if (!s1.Equals(s2, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrEmpty(c2))
        {
            return true;
        }

        if (string.IsNullOrEmpty(c1))
        {
            return false;
        }

        return c1.Equals(c2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Tests a token search fc against string-type nodes, using exact matching (equality & case-sensitive).</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenAgainstStringValue(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? value = valueNode.Poco switch
        {
            Id fid => fid.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            _ => null,
        };

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

    /// <summary>Tests a token search fc against string-type nodes, using exact matching (case-sensitive), modified to 'not'.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenNotAgainstStringValue(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            // note that in 'not', missing values are matches
            return true;
        }

        string? value = valueNode.Poco switch
        {
            Id fid => fid.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(value))
        {
            // note that in 'not', missing values are matches
            return true;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (sp.Values[i].Equals(value, StringComparison.Ordinal))
            {
                // not is inverted
                return false;
            }
        }

        // not is inverted
        return true;
    }

    /// <summary>Tests token against bool.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenAgainstBool(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        bool? value = valueNode.Poco switch
        {
            FhirBoolean fb => fb.Value,
            FhirString fs => fs.Value?.ToLowerInvariant() switch
            {
                "true" or "t" or "1" => true,
                "false" or "f" or "0" => false,
                _ => null,
            },
            Integer i => i.Value switch
            {
                1 => true,
                0 => false,
                _ => null,
            },
            _ => null,
        };

        if (value is null)
        {
            return false;
        }

        if (sp.ValueBools?.Any() ?? false)
        {
            for (int i = 0; i < sp.ValueBools.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                if (sp.ValueBools[i] == value)
                {
                    return true;
                }
            }
        }
        else
        {
            for (int i = 0; i < sp.Values.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                if ((value.Value && sp.Values[i].StartsWith("t", StringComparison.OrdinalIgnoreCase)) ||
                    (!value.Value && sp.Values[i].StartsWith("f", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tests token not against bool.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenNotAgainstBool(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            // note that in 'not', missing values are matches
            return true;
        }

        bool? value = valueNode.Poco switch
        {
            FhirBoolean fb => fb.Value,
            FhirString fs => fs.Value?.ToLowerInvariant() switch
            {
                "true" or "t" or "1" => true,
                "false" or "f" or "0" => false,
                _ => null,
            },
            Integer i => i.Value switch
            {
                1 => true,
                0 => false,
                _ => null,
            },
            _ => null,
        };

        if (value is null)
        {
            // note that in 'not', missing values are matches
            return true;
        }

        if (sp.ValueBools?.Any() ?? false)
        {
            for (int i = 0; i < sp.ValueBools.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                if (sp.ValueBools[i] == value)
                {
                    // not is inverted
                    return false;
                }
            }
        }
        else
        {
            for (int i = 0; i < sp.Values.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                if ((value.Value && sp.Values[i].StartsWith("t", StringComparison.OrdinalIgnoreCase)) ||
                    (!value.Value && sp.Values[i].StartsWith("f", StringComparison.OrdinalIgnoreCase)))
                {
                    // not is inverted
                    return false;
                }
            }
        }

        // not is inverted
        return true;
    }

    /// <summary>Tests token against codeeable types.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenAgainstCoding(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueFhirCodes is null))
        {
            return false;
        }

        string? valueSystem, valueCode;

        switch (valueNode.Poco)
        {
            case Code fc:
                {
                    valueSystem = null;
                    valueCode = fc.Value;
                }
                break;
                
            case Coding fco:
                {
                    valueSystem = fco.System;
                    valueCode = fco.Code;
                }
                break;

            case CodeableConcept fcc:
                {
                    if (fcc.Coding.Count != 1)
                    {
                        // substitute correct call
                        return TestTokenAgainstCodeableConcept(valueNode, sp);
                    }

                    // just process a single fco here
                    valueSystem = fcc.Coding[0].System;
                    valueCode = fcc.Coding[0].Code;
                }
                break;

            case Identifier fi:
                {
                    valueSystem = fi.System;
                    valueCode = fi.Value;
                }
                break;

            case ContactPoint fcp:
                {
                    valueSystem = fcp.System?.ToString();
                    valueCode = fcp.Value;
                }
                break;

            case FhirString fs:
                {
                    valueSystem = null;
                    valueCode = fs.Value;
                }
                break;

            default:
                throw new Exception($"Cannot test token against type: {valueNode.Poco.TypeName} as Coding");
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (CompareCodeWithSystem(valueSystem, valueCode, sp.ValueFhirCodes[i].System, sp.ValueFhirCodes[i].Value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token against codeable concept.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenAgainstCodeableConcept(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueFhirCodes is null))
        {
            return false;
        }

        switch (valueNode.Poco)
        {
            case Code fc:
                {
                    for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
                    {
                        if (sp.IgnoredValueFlags[i])
                        {
                            continue;
                        }

                        if (CompareCodeWithSystem(
                                null,
                                fc.Value,
                                sp.ValueFhirCodes[i].System,
                                sp.ValueFhirCodes[i].Value))
                        {
                            return true;
                        }
                    }
                }
                break;

            case CodeableConcept fcc:
                {
                    if (fcc.Coding is null)
                    {
                        return false;
                    }

                    foreach (Coding c in fcc.Coding)
                    {
                        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
                        {
                            if (sp.IgnoredValueFlags[i])
                            {
                                continue;
                            }

                            if (CompareCodeWithSystem(
                                    c.System,
                                    c.Code,
                                    sp.ValueFhirCodes[i].System,
                                    sp.ValueFhirCodes[i].Value))
                            {
                                return true;
                            }
                        }
                    }
                }
                break;

            case Coding fco:
                {
                    for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
                    {
                        if (sp.IgnoredValueFlags[i])
                        {
                            continue;
                        }

                        if (CompareCodeWithSystem(
                                fco.System,
                                fco.Code,
                                sp.ValueFhirCodes[i].System,
                                sp.ValueFhirCodes[i].Value))
                        {
                            return true;
                        }
                    }
                }
                break;

            default:
                throw new Exception($"Cannot test token against type: {valueNode.Poco.TypeName} as CodeableConcept");
        }

        return false;
    }

    /// <summary>Tests token in codeable concept.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">       The sp.</param>
    /// <param name="store">    The store.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenInCodeableConcept(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueFhirCodes is null))
        {
            return false;
        }

        switch (valueNode.Poco)
        {
            case Code fc:
                {
                    for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
                    {
                        if (sp.IgnoredValueFlags[i])
                        {
                            continue;
                        }

                        if (store.Terminology.VsContains(sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System, null, fc.Value))
                        {
                            return true;
                        }
                    }
                }
                break;

            case CodeableConcept fcc:
                {
                    if (fcc.Coding is null)
                    {
                        return false;
                    }

                    foreach (Coding c in fcc.Coding)
                    {
                        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
                        {
                            if (sp.IgnoredValueFlags[i])
                            {
                                continue;
                            }

                            if (store.Terminology.VsContains(sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System, c.System, c.Code))
                            {
                                return true;
                            }
                        }
                    }
                }
                break;

            case Coding fco:
                {
                    for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
                    {
                        if (sp.IgnoredValueFlags[i])
                        {
                            continue;
                        }

                        if (store.Terminology.VsContains(sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System, fco.System, fco.Code))
                        {
                            return true;
                        }
                    }
                }
                break;


            default:
                throw new Exception($"Cannot test token against type: {valueNode.Poco.TypeName} as CodeableConcept");
        }

        return false;
    }

    /// <summary>Tests token in fc.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">       The sp.</param>
    /// <param name="store">    The store.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenInCoding(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueFhirCodes is null))
        {
            return false;
        }

        string? valueSystem, valueCode;

        switch (valueNode.Poco)
        {
            case Code fc:
                {
                    valueSystem = null;
                    valueCode = fc.Value;
                }
                break;

            case Coding fco:
                {
                    valueSystem = fco.System;
                    valueCode = fco.Code;
                }
                break;

            case Identifier fi:
                {
                    valueSystem = fi.System;
                    valueCode = fi.Value;
                }
                break;

            case ContactPoint fcp:
                {
                    valueSystem = fcp.System?.ToString();
                    valueCode = fcp.Value;
                }
                break;

            case FhirString fs:
                {
                    valueSystem = null;
                    valueCode = fs.Value;
                }
                break;

            default:
                throw new Exception($"Cannot test token against type: {valueNode.Poco.TypeName} as Coding");
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (store.Terminology.VsContains(
                    sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System,
                    valueSystem,
                    valueCode))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not against fc.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenNotAgainstCoding(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueFhirCodes is null))
        {
            // note that in 'not', missing values are matches
            return true;
        }

        string? valueSystem, valueCode;

        switch (valueNode.Poco)
        {
            case Code fc:
                {
                    valueSystem = null;
                    valueCode = fc.Value;
                }
                break;

            case Coding fco:
                {
                    valueSystem = fco.System;
                    valueCode = fco.Code;
                }
                break;

            case Identifier fi:
                {
                    valueSystem = fi.System;
                    valueCode = fi.Value;
                }
                break;

            case ContactPoint fcp:
                {
                    valueSystem = fcp.System?.ToString();
                    valueCode = fcp.Value;
                }
                break;

            case FhirString fs:
                {
                    valueSystem = null;
                    valueCode = fs.Value;
                }
                break;

            default:
                throw new Exception($"Cannot test token against type: {valueNode.Poco.TypeName} as Coding");
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (CompareCodeWithSystem(valueSystem, valueCode, sp.ValueFhirCodes[i].System, sp.ValueFhirCodes[i].Value))
            {
                // not is inverted
                return false;
            }
        }

        // not is inverted
        return true;
    }

    /// <summary>Tests token of type identifier.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">       The sp.</param>
    /// <param name="store">    The store.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenOfTypeIdentifier(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (sp.ValueFhirCodes is null))
        {
            return false;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            switch (valueNode.Poco)
            {
                case Identifier identifierValue:
                    {
                        // if there is a fc, it needs to match
                        if ((!string.IsNullOrEmpty(sp.ValueFhirCodes[i].Value)) &&
                            (!identifierValue.Value?.Equals(sp.ValueFhirCodes[i].Value, StringComparison.Ordinal) ?? false))

                        {
                            continue;
                        }

                        if ((identifierValue.Type?.Coding is not null) && (sp.ValueFhirCodeTypes is not null))
                        {
                            foreach (Coding c in identifierValue.Type.Coding)
                            {
                                for (int j = 0; j < sp.ValueFhirCodeTypes.Length; j++)
                                {
                                    if (sp.IgnoredValueFlags[j])
                                    {
                                        continue;
                                    }

                                    if (CompareCodeWithSystem(
                                            c.System ?? string.Empty,
                                            c.Code ?? string.Empty,
                                            sp.ValueFhirCodeTypes[j].System ?? string.Empty,
                                            sp.ValueFhirCodeTypes[j].Value))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    break;
            }
        }

        return false;
    }

    /// <summary>Tests token above for OID elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if OID subsumes search OID.</returns>
    public static bool TestTokenAboveOid(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? oidValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(oidValue))
        {
            return false;
        }

        // Remove urn:oid: prefix if present
        if (oidValue.StartsWith("urn:oid:", StringComparison.OrdinalIgnoreCase))
        {
            oidValue = oidValue.Substring(8);
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchOid = sp.Values[i];
            if (searchOid.StartsWith("urn:oid:", StringComparison.OrdinalIgnoreCase))
            {
                searchOid = searchOid.Substring(8);
            }

            // Check OID hierarchy: 1.2.3.4 subsumes 1.2.3.4.5
            if (searchOid.StartsWith(oidValue + ".", StringComparison.OrdinalIgnoreCase) ||
                oidValue.Equals(searchOid, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token above for URI elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if URI concept subsumes search URI concept.</returns>
    public static bool TestTokenAboveUri(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? uriValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
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

            string searchUri = sp.Values[i];

            // Since terminology service doesn't have working Subsumes method,
            // fallback to exact match for now
            if (uriValue.Equals(searchUri, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token above for URL elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if URL matches (URLs typically don't have subsumption).</returns>
    public static bool TestTokenAboveUrl(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? urlValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
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

            // Most URLs don't have subsumption relationships
            if (urlValue.Equals(searchUrl, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token below for OID elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if search OID subsumes resource OID.</returns>
    public static bool TestTokenBelowOid(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? oidValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(oidValue))
        {
            return false;
        }

        // Remove urn:oid: prefix if present
        if (oidValue.StartsWith("urn:oid:", StringComparison.OrdinalIgnoreCase))
        {
            oidValue = oidValue.Substring(8);
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchOid = sp.Values[i];
            if (searchOid.StartsWith("urn:oid:", StringComparison.OrdinalIgnoreCase))
            {
                searchOid = searchOid.Substring(8);
            }

            // Check OID hierarchy: 1.2.3.4.5 is subsumed by 1.2.3.4
            if (oidValue.StartsWith(searchOid + ".", StringComparison.OrdinalIgnoreCase) ||
                oidValue.Equals(searchOid, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token below for URI elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if search URI concept subsumes resource URI concept.</returns>
    public static bool TestTokenBelowUri(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? uriValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
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

            string searchUri = sp.Values[i];

            // Since terminology service doesn't have working Subsumes method,
            // fallback to exact match for now
            if (uriValue.Equals(searchUri, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token below for URL elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if URL matches (URLs typically don't have subsumption).</returns>
    public static bool TestTokenBelowUrl(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? urlValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
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

            if (urlValue.StartsWith(searchUrl, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
    
    /// <summary>Tests token code text for code elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if code fc matches search text criteria.</returns>
    public static bool TestTokenCodeTextCode(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? value = valueNode.Poco switch
        {
            Code c => c.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            _ => null,
        };

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

            string searchText = sp.Values[i];
            
            // Case-insensitive text matching (starts with or equals)
            if (value.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                value.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token code text for fc elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if fc code fc matches search text criteria.</returns>
    public static bool TestTokenCodeTextCoding(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not Coding coding) ||
            string.IsNullOrEmpty(coding.Code))
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
            
            // Case-insensitive text matching against Coding.code (ignore system)
            if (coding.Code.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                coding.Code.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token code text for codeableconcept elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if any fc code in CodeableConcept matches search text.</returns>
    public static bool TestTokenCodeTextCodeableConcept(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not CodeableConcept codeableConcept) ||
            (codeableConcept.Coding.Count == 0))
        {
            return false;
        }

        // Test each fc in the CodeableConcept
        foreach (Hl7.Fhir.Model.Coding coding in codeableConcept.Coding)
        {
            if (string.IsNullOrEmpty(coding.Code))
            {
                continue;
            }

            for (int i = 0; i < sp.Values.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                string searchText = sp.Values[i];
                
                // Case-insensitive text matching against each Coding.code
                if (coding.Code.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                    coding.Code.Equals(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tests token code text for identifier elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if identifier fc matches search text criteria.</returns>
    public static bool TestTokenCodeTextIdentifier(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not Identifier identifier))
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
            
            // Case-insensitive text matching against identifier fc
            if ((identifier.Value?.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (identifier.Value?.Equals(searchText, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token code text for contactpoint elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if contact point fc matches search text criteria.</returns>
    public static bool TestTokenCodeTextContactPoint(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not ContactPoint contactPoint))
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
            
            // Case-insensitive text matching against contact fc
            if ((contactPoint.Value?.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (contactPoint.Value?.Equals(searchText, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token code text for canonical elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if canonical URL codes match search text criteria.</returns>
    public static bool TestTokenCodeTextCanonical(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? canonicalUrl = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(canonicalUrl))
        {
            return false;
        }

        // Extract code-like portions from canonical URL (last path segment)
        if (!Uri.TryCreate(canonicalUrl, UriKind.RelativeOrAbsolute, out Uri? uri))
        {
            return false;
        }

        string lastSegment = uri.Segments?.LastOrDefault()?.TrimEnd('/') ?? string.Empty;

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchText = sp.Values[i];
            
            // Case-insensitive text matching against URL code portions
            if (lastSegment.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                lastSegment.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token code text for OID elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if OID representation matches search text (rare case).</returns>
    public static bool TestTokenCodeTextOid(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? oidValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(oidValue))
        {
            return false;
        }

        // Remove urn:oid: prefix if present
        if (oidValue.StartsWith("urn:oid:", StringComparison.OrdinalIgnoreCase))
        {
            oidValue = oidValue.Substring(8);
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchText = sp.Values[i];
            
            // Case-insensitive text matching against OID string
            if (oidValue.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                oidValue.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token code text for URI elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URI codes match search text criteria.</returns>
    public static bool TestTokenCodeTextUri(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? uriValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(uriValue))
        {
            return false;
        }

        // Extract code-like portions from URI (path segments, fragments)
        if (!Uri.TryCreate(uriValue, UriKind.RelativeOrAbsolute, out Uri? uri))
        {
            return false;
        }

        List<string> codeParts = [];
        if (uri.Segments is not null)
        {
            foreach (string segment in uri.Segments)
            {
                string cleanSegment = segment.TrimEnd('/');
                if (!string.IsNullOrEmpty(cleanSegment) && cleanSegment != "/")
                {
                    codeParts.Add(cleanSegment);
                }
            }
        }
        
        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            codeParts.Add(uri.Fragment.TrimStart('#'));
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchText = sp.Values[i];
            
            // Case-insensitive text matching against URI code portions
            foreach (string codePart in codeParts)
            {
                if (codePart.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                    codePart.Equals(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tests token code text for URL elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URL codes match search text criteria.</returns>
    public static bool TestTokenCodeTextUrl(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? urlValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(urlValue))
        {
            return false;
        }

        // Extract meaningful code portions from URL path and query
        if (!Uri.TryCreate(urlValue, UriKind.RelativeOrAbsolute, out Uri? uri))
        {
            return false;
        }

        List<string> codeParts = [];
        if (uri.Segments is not null)
        {
            foreach (string segment in uri.Segments)
            {
                string cleanSegment = segment.TrimEnd('/');
                if (!string.IsNullOrEmpty(cleanSegment) && cleanSegment != "/")
                {
                    codeParts.Add(cleanSegment);
                }
            }
        }
        
        if (!string.IsNullOrEmpty(uri.Query))
        {
            string[] queryParts = uri.Query.TrimStart('?').Split('&');
            foreach (string queryPart in queryParts)
            {
                string[] keyValue = queryPart.Split('=');
                if (keyValue.Length > 1)
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

            string searchText = sp.Values[i];
            
            // Case-insensitive text matching against URL code parts
            foreach (string codePart in codeParts)
            {
                if (codePart.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                    codePart.Equals(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tests token code text for UUID elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if UUID representation matches search text (rare case).</returns>
    public static bool TestTokenCodeTextUuid(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? uuidValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(uuidValue))
        {
            return false;
        }

        // Remove urn:uuid: prefix if present
        if (uuidValue.StartsWith("urn:uuid:", StringComparison.OrdinalIgnoreCase))
        {
            uuidValue = uuidValue.Substring(9);
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchText = sp.Values[i];
            
            // Case-insensitive text matching against UUID string
            if (uuidValue.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                uuidValue.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token code text for string elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if string fc matches search text criteria.</returns>
    public static bool TestTokenCodeTextString(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? stringValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Id fid => fid.Value,
            Code fc => fc.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            FhirString fs => fs.Value,
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

            string searchText = sp.Values[i];
            
            // Case-insensitive text matching against string fc
            if (stringValue.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                stringValue.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for code elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if code is NOT in ValueSet.</returns>
    public static bool TestTokenNotInCode(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if ((valueNode?.Poco is null))
        {
            // Missing values are considered NOT in any ValueSet
            return true;
        }

        string? codeValue = valueNode.Poco switch
        {
            Id fid => fid.Value,
            Code fc => fc.Value,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(codeValue))
        {
            // Missing values are considered NOT in any ValueSet
            return true;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.Values[i];
            
            // Check if code is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, codeValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for fco elements.</summary>
    /// <param name="valueNode">The fco node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if fc is NOT in ValueSet.</returns>
    public static bool TestTokenNotInCoding(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not Coding coding) ||
            string.IsNullOrEmpty(coding.Code))
        {
            // Missing values are considered NOT in any ValueSet
            return true;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.Values[i];
            
            // Check if fc is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, coding.System ?? string.Empty, coding.Code))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for codeableconcept elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if NO fc in CodeableConcept is in ValueSet.</returns>
    public static bool TestTokenNotInCodeableConcept(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not CodeableConcept codeableConcept) ||
            (codeableConcept.Coding.Count == 0))
        {
            // Missing values are considered NOT in any ValueSet
            return true;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.Values[i];
            
            // Check if ALL codings are NOT in the ValueSet
            bool anyInValueSet = false;
            foreach (Hl7.Fhir.Model.Coding coding in codeableConcept.Coding)
            {
                if (!string.IsNullOrEmpty(coding.Code) &&
                    store.Terminology.VsContains(valueSetUri, coding.System ?? string.Empty, coding.Code))
                {
                    anyInValueSet = true;
                    break;
                }
            }
            
            if (!anyInValueSet)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for identifier elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if identifier is NOT in ValueSet.</returns>
    public static bool TestTokenNotInIdentifier(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not Identifier identifier))
        {
            return true;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.Values[i];
            
            // Check if identifier is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, identifier.System ?? string.Empty, identifier.Value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for contactpoint elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if contact point is NOT in ValueSet.</returns>
    public static bool TestTokenNotInContactPoint(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not ContactPoint contactPoint))
        {
            return true;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.Values[i];
            
            // Check if contact point is NOT in the ValueSet
            string system = contactPoint.System?.ToString() ?? string.Empty;
            if (!store.Terminology.VsContains(valueSetUri, system, contactPoint.Value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for canonical elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if canonical is NOT in ValueSet.</returns>
    public static bool TestTokenNotInCanonical(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if (valueNode?.Poco is null)
        {
            return true;
        }

        string? canonicalUrl = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
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

            string valueSetUri = sp.Values[i];
            
            // Check if canonical URL is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, canonicalUrl))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for OID elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if OID is NOT in ValueSet.</returns>
    public static bool TestTokenNotInOid(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if (valueNode?.Poco is null)
        {
            return true;
        }

        string? oidValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
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

            string valueSetUri = sp.Values[i];
            
            // Check if OID is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, oidValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for URI elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if URI is NOT in ValueSet.</returns>
    public static bool TestTokenNotInUri(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if (valueNode?.Poco is null)
        {
            return true;
        }

        string? uriValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
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

            string valueSetUri = sp.Values[i];
            
            // Check if URI is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, uriValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for URL elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if URL is NOT in ValueSet.</returns>
    public static bool TestTokenNotInUrl(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if (valueNode?.Poco is null)
        {
            return true;
        }

        string? urlValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
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

            string valueSetUri = sp.Values[i];
            
            // Check if URL is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, urlValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for UUID elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if UUID is NOT in ValueSet.</returns>
    public static bool TestTokenNotInUuid(PocoNode? valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        if (valueNode?.Poco is null)
        {
            return true;
        }

        string? uuidValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            ResourceReference r => r.Reference,
            FhirString fs => fs.Value,
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

            string valueSetUri = sp.Values[i];
            
            // Check if UUID is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, uuidValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for string elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if string is NOT in ValueSet.</returns>
    public static bool TestTokenNotInString(
        PocoNode? valueNode,
        ParsedSearchParameter sp,
        VersionedFhirStore store)
    {
        if (valueNode?.Poco is null)
        {
            return true;
        }

        string? stringValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Id fid => fid.Value,
            Code fc => fc.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            FhirString fs => fs.Value,
            _ => null,
        };

        if (string.IsNullOrEmpty(stringValue))
        {
            return true;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string valueSetUri = sp.Values[i];
            
            // Check if string is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, stringValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token text for fc elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if fc display text matches search criteria.</returns>
    public static bool TestTokenTextCoding(
        PocoNode? valueNode,
        ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not Coding coding) ||
            string.IsNullOrEmpty(coding.Display))
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
            if (coding.Display.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                coding.Display.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token text for codeableconcept elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if CodeableConcept text or fc display matches search criteria.</returns>
    public static bool TestTokenTextCodeableConcept(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not CodeableConcept codeableConcept) ||
            ((codeableConcept.Coding.Count == 0) && string.IsNullOrEmpty(codeableConcept.Text)))
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
            
            // Check CodeableConcept.text
            if (!string.IsNullOrEmpty(codeableConcept.Text))
            {
                if (codeableConcept.Text.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                    codeableConcept.Text.Equals(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            // Check Coding.display for each fc
            if (codeableConcept.Coding is not null)
            {
                foreach (Hl7.Fhir.Model.Coding coding in codeableConcept.Coding)
                {
                    if (!string.IsNullOrEmpty(coding.Display))
                    {
                        if (coding.Display.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                            coding.Display.Equals(searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Tests token text for identifier elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if identifier type text matches search criteria.</returns>
    public static bool TestTokenTextIdentifier(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not Identifier identifier))
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
            
            // Check Identifier.type.text
            if (!string.IsNullOrEmpty(identifier.Type?.Text))
            {
                if (identifier.Type.Text.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                    identifier.Type.Text.Equals(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            // Check Identifier.type.fc.display
            if (identifier.Type?.Coding is not null)
            {
                foreach (Hl7.Fhir.Model.Coding coding in identifier.Type.Coding)
                {
                    if (!string.IsNullOrEmpty(coding.Display))
                    {
                        if (coding.Display.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                            coding.Display.Equals(searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Tests token text for string elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if string fc matches search criteria as text.</returns>
    public static bool TestTokenTextString(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? stringValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Id fid => fid.Value,
            Code fc => fc.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            FhirString fs => fs.Value,
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

            string searchText = sp.Values[i];
            
            // Basic text matching (begins with or is, case-insensitive)
            if (stringValue.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                stringValue.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token advanced text for fc elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if fc display text matches advanced search criteria.</returns>
    public static bool TestTokenTextAdvancedCoding(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not Coding coding) ||
            string.IsNullOrEmpty(coding.Display))
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
            
            // Advanced text processing - supports basic logical operations
            if (ProcessAdvancedTextSearch(coding.Display, searchText))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token advanced text for codeableconcept elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if CodeableConcept text matches advanced search criteria.</returns>
    public static bool TestTokenTextAdvancedCodeableConcept(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not CodeableConcept codeableConcept))
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
            
            // Check CodeableConcept.text with advanced processing
            if (!string.IsNullOrEmpty(codeableConcept.Text))
            {
                if (ProcessAdvancedTextSearch(codeableConcept.Text, searchText))
                {
                    return true;
                }
            }
            
            // Check Coding.display for each fc with advanced processing
            if (codeableConcept.Coding is not null)
            {
                foreach (Hl7.Fhir.Model.Coding coding in codeableConcept.Coding)
                {
                    if (!string.IsNullOrEmpty(coding.Display))
                    {
                        if (ProcessAdvancedTextSearch(coding.Display, searchText))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Tests token advanced text for identifier elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if identifier type text matches advanced search criteria.</returns>
    public static bool TestTokenTextAdvancedIdentifier(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not Identifier identifier))
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
            
            // Check Identifier.type.text with advanced processing
            if (!string.IsNullOrEmpty(identifier.Type?.Text))
            {
                if (ProcessAdvancedTextSearch(identifier.Type.Text, searchText))
                {
                    return true;
                }
            }
            
            // Check Identifier.type.fc.display with advanced processing
            if (identifier.Type?.Coding is not null)
            {
                foreach (Hl7.Fhir.Model.Coding coding in identifier.Type.Coding)
                {
                    if (!string.IsNullOrEmpty(coding.Display))
                    {
                        if (ProcessAdvancedTextSearch(coding.Display, searchText))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Tests token advanced text for string elements.</summary>
    /// <param name="valueNode">The fc node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if string fc matches advanced search criteria.</returns>
    public static bool TestTokenTextAdvancedString(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        string? stringValue = valueNode.Poco switch
        {
            Canonical c => c.Value,
            Id fid => fid.Value,
            Code fc => fc.Value,
            FhirUri fi => fi.Value,
            FhirUrl fl => fl.Value,
            Oid o => o.Value,
            Uuid u => u.Value,
            FhirString fs => fs.Value,
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

            string searchText = sp.Values[i];
            
            // Advanced text processing for string values
            if (ProcessAdvancedTextSearch(stringValue, searchText))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Processes advanced text search with basic logical operations support.</summary>
    /// <param name="textValue">The text fc to search in.</param>
    /// <param name="searchQuery">The advanced search query.</param>
    /// <returns>True if the query matches the text fc.</returns>
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
            string[] andTerms = lowerQuery.Split(new[] { " and " }, StringSplitOptions.RemoveEmptyEntries);
            return andTerms.All(term => lowerText.Contains(term.Trim()));
        }

        // Handle simple OR operations (any space-separated term can be present)
        if (lowerQuery.Contains(" or "))
        {
            string[] orTerms = lowerQuery.Split(new[] { " or " }, StringSplitOptions.RemoveEmptyEntries);
            return orTerms.Any(term => lowerText.Contains(term.Trim()));
        }

        // Handle multiple words as implicit AND (all words must be present)
        if (lowerQuery.Contains(' '))
        {
            string[] words = lowerQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return words.All(word => lowerText.Contains(word));
        }

        // Single word search with word boundary consideration
        // Check for whole word matches or partial matches
        return lowerText.Contains(lowerQuery) || 
               lowerText.Split(' ').Any(word => word.StartsWith(lowerQuery));
    }
}

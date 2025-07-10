// <copyright file="EvalTokenSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using FhirCandle.Storage;
using Hl7.Fhir.ElementModel;

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
    internal static bool CompareCodeWithSystem(string s1, string c1, string s2, string c2)
    {
        if (string.IsNullOrEmpty(s2) ||
            s1.Equals(s2, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrEmpty(c2) || c1.Equals(c2, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>Tests a token search value against string-type nodes, using exact matching (equality & case-sensitive).</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenAgainstStringValue(ITypedElement valueNode, ParsedSearchParameter sp)
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

    /// <summary>Tests a token search value against string-type nodes, using exact matching (case-sensitive), modified to 'not'.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenNotAgainstStringValue(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenAgainstBool(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null)
        {
            return false;
        }

        bool value = (bool)valueNode.Value;

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

                if ((value && sp.Values[i].StartsWith("t", StringComparison.OrdinalIgnoreCase)) ||
                    (!value && sp.Values[i].StartsWith("f", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tests token not against bool.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenNotAgainstBool(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null)
        {
            // note that in 'not', missing values are matches
            return true;
        }

        bool value = (bool)valueNode.Value;

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

                if ((value && sp.Values[i].StartsWith("t", StringComparison.OrdinalIgnoreCase)) ||
                    (!value && sp.Values[i].StartsWith("f", StringComparison.OrdinalIgnoreCase)))
                {
                    // not is inverted
                    return false;
                }
            }
        }

        // not is inverted
        return true;
    }

    /// <summary>Tests token against code and coding types.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenAgainstCoding(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode == null) ||
            (sp.ValueFhirCodes == null))
        {
            return false;
        }

        string valueSystem, valueCode;

        switch (valueNode.InstanceType)
        {
            case "Code":
                {
                    Hl7.Fhir.Model.Code v = valueNode.ToPoco<Hl7.Fhir.Model.Code>();

                    valueSystem = string.Empty;
                    valueCode = v.Value;
                }
                break;

            case "Coding":
                {
                    Hl7.Fhir.Model.Coding v = valueNode.ToPoco<Hl7.Fhir.Model.Coding>();

                    valueSystem = v.System ?? string.Empty;
                    valueCode = v.Code ?? string.Empty;
                }
                break;

            case "Identifier":
                {
                    Hl7.Fhir.Model.Identifier v = valueNode.ToPoco<Hl7.Fhir.Model.Identifier>();

                    valueSystem = v.System ?? string.Empty;
                    valueCode = v.Value ?? string.Empty;
                }
                break;

            case "ContactPoint":
                {
                    Hl7.Fhir.Model.ContactPoint v = valueNode.ToPoco<Hl7.Fhir.Model.ContactPoint>();

                    valueSystem = v.System?.ToString() ?? string.Empty;
                    valueCode = v.Value ?? string.Empty;
                }
                break;

            default:
                {
                    if ((valueNode.Value != null) &&
                        (valueNode.Value is string v))
                    {
                        valueSystem = string.Empty;
                        valueCode = v;
                    }
                    else
                    {
                        throw new Exception($"Cannot test token against type: {valueNode.InstanceType} as Coding");
                    }
                }
                break;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (CompareCodeWithSystem(valueSystem, valueCode, sp.ValueFhirCodes[i].System ?? string.Empty, sp.ValueFhirCodes[i].Value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token against codeable concept.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenAgainstCodeableConcept(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode == null) ||
            (sp.ValueFhirCodes == null))
        {
            return false;
        }

        switch (valueNode.InstanceType)
        {
            case "CodeableConcept":
                {
                    Hl7.Fhir.Model.CodeableConcept cc = valueNode.ToPoco<Hl7.Fhir.Model.CodeableConcept>();

                    if (cc.Coding != null)
                    {
                        foreach (Hl7.Fhir.Model.Coding c in cc.Coding)
                        {
                            for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
                            {
                                if (sp.IgnoredValueFlags[i])
                                {
                                    continue;
                                }

                                if (CompareCodeWithSystem(
                                        c.System ?? string.Empty,
                                        c.Code ?? string.Empty,
                                        sp.ValueFhirCodes[i].System ?? string.Empty,
                                        sp.ValueFhirCodes[i].Value))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                break;

            //case Hl7.Fhir.ElementModel.Types.Concept ec:
            //    {
            //        if (ec.Codes != null)
            //        {
            //            foreach (Hl7.Fhir.ElementModel.Types.Code c in ec.Codes)
            //            {
            //                if (sp.ValueFhirCodes.Any(v => CompareCodeWithSystem(
            //                        c.System ?? string.Empty,
            //                        c.Value ?? string.Empty,
            //                        v.System ?? string.Empty,
            //                        v.Value)))
            //                {
            //                    return true;
            //                }
            //            }
            //        }
            //    }
            //    break;

            default:
                throw new Exception($"Cannot test token against type: {valueNode.GetType()} as CodeableConcept");
        }

        return false;
    }

    /// <summary>Tests token in codeable concept.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <param name="store">    The store.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenInCodeableConcept(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        if ((valueNode == null) ||
            (sp.ValueFhirCodes == null))
        {
            return false;
        }

        switch (valueNode.InstanceType)
        {
            case "CodeableConcept":
                {
                    Hl7.Fhir.Model.CodeableConcept cc = valueNode.ToPoco<Hl7.Fhir.Model.CodeableConcept>();

                    if (cc.Coding != null)
                    {
                        foreach (Hl7.Fhir.Model.Coding c in cc.Coding)
                        {
                            for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
                            {
                                if (sp.IgnoredValueFlags[i])
                                {
                                    continue;
                                }

                                if (store.Terminology.VsContains(sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System ?? string.Empty, c.System ?? string.Empty, c.Code ?? string.Empty))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                break;


            default:
                throw new Exception($"Cannot test token against type: {valueNode.GetType()} as CodeableConcept");
        }

        return false;
    }

    /// <summary>Tests token in coding.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <param name="store">    The store.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenInCoding(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        if ((valueNode == null) ||
            (sp.ValueFhirCodes == null))
        {
            return false;
        }

        string valueSystem, valueCode;

        switch (valueNode.InstanceType)
        {
            case "Code":
                {
                    Hl7.Fhir.Model.Code v = valueNode.ToPoco<Hl7.Fhir.Model.Code>();

                    valueSystem = string.Empty;
                    valueCode = v.Value;
                }
                break;

            case "Coding":
                {
                    Hl7.Fhir.Model.Coding v = valueNode.ToPoco<Hl7.Fhir.Model.Coding>();

                    valueSystem = v.System ?? string.Empty;
                    valueCode = v.Code ?? string.Empty;
                }
                break;

            case "Identifier":
                {
                    Hl7.Fhir.Model.Identifier v = valueNode.ToPoco<Hl7.Fhir.Model.Identifier>();

                    valueSystem = v.System ?? string.Empty;
                    valueCode = v.Value ?? string.Empty;
                }
                break;

            case "ContactPoint":
                {
                    Hl7.Fhir.Model.ContactPoint v = valueNode.ToPoco<Hl7.Fhir.Model.ContactPoint>();

                    valueSystem = v.System?.ToString() ?? string.Empty;
                    valueCode = v.Value ?? string.Empty;
                }
                break;

            default:
                {
                    if ((valueNode.Value != null) &&
                        (valueNode.Value is string v))
                    {
                        valueSystem = string.Empty;
                        valueCode = v;
                    }
                    else
                    {
                        throw new Exception($"Cannot test token against type: {valueNode.InstanceType} as Coding");
                    }
                }
                break;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (store.Terminology.VsContains(sp.ValueFhirCodes[i].Value ?? sp.ValueFhirCodes[i].System ?? string.Empty, valueSystem, valueCode))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not against coding.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenNotAgainstCoding(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode == null) ||
            (sp.ValueFhirCodes == null))
        {
            // note that in 'not', missing values are matches
            return true;
        }

        string valueSystem, valueCode;

        switch (valueNode.InstanceType)
        {
            case "Code":
                {
                    Hl7.Fhir.Model.Code v = valueNode.ToPoco<Hl7.Fhir.Model.Code>();

                    valueSystem = string.Empty;
                    valueCode = v.Value;
                }
                break;

            case "Coding":
                {
                    Hl7.Fhir.Model.Coding v = valueNode.ToPoco<Hl7.Fhir.Model.Coding>();

                    valueSystem = v.System ?? string.Empty;
                    valueCode = v.Code ?? string.Empty;
                }
                break;

            case "Identifier":
                {
                    Hl7.Fhir.Model.Identifier v = valueNode.ToPoco<Hl7.Fhir.Model.Identifier>();

                    valueSystem = v.System ?? string.Empty;
                    valueCode = v.Value ?? string.Empty;
                }
                break;

            case "ContactPoint":
                {
                    Hl7.Fhir.Model.ContactPoint v = valueNode.ToPoco<Hl7.Fhir.Model.ContactPoint>();

                    valueSystem = v.System?.ToString() ?? string.Empty;
                    valueCode = v.Value ?? string.Empty;
                }
                break;

            default:
                {
                    if ((valueNode.Value != null) &&
                        (valueNode.Value is string v))
                    {
                        valueSystem = string.Empty;
                        valueCode = v;
                    }
                    else
                    {
                        throw new Exception($"Cannot test token against type: {valueNode.InstanceType} as Coding");
                    }
                }
                break;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (CompareCodeWithSystem(valueSystem, valueCode, sp.ValueFhirCodes[i].System ?? string.Empty, sp.ValueFhirCodes[i].Value))
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <param name="store">    The store.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenOfType(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode == null) ||
            (sp.ValueFhirCodes == null))
        {
            return false;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            switch (valueNode.InstanceType)
            {
                case "Identifier":
                    {
                        Hl7.Fhir.Model.Identifier v = valueNode.ToPoco<Hl7.Fhir.Model.Identifier>();

                        // if there is a value, it needs to match
                        if ((!string.IsNullOrEmpty(sp.ValueFhirCodes[i].Value)) &&
                            (!v.Value.Equals(sp.ValueFhirCodes[i].Value, StringComparison.Ordinal)))

                        {
                            continue;
                        }

                        if ((v.Type?.Coding != null) && (sp.ValueFhirCodeTypes != null))
                        {
                            foreach (Hl7.Fhir.Model.Coding c in v.Type.Coding)
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

    // Token Above Modifier methods - hierarchical subsumption search
    
    /// <summary>Tests token above for code elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if resource code subsumes search code.</returns>
    public static bool TestTokenAboveCode(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string resourceCode = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(resourceCode))
        {
            return false;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchSystem = sp.ValueFhirCodes[i].System ?? string.Empty;
            string searchCode = sp.ValueFhirCodes[i].Value;

            // Since terminology service doesn't have working Subsumes method,
            // fallback to exact match for now
            if (resourceCode.Equals(searchCode, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token above for coding elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if resource coding subsumes search coding.</returns>
    public static bool TestTokenAboveCoding(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        Hl7.Fhir.Model.Coding coding = valueNode.ToPoco<Hl7.Fhir.Model.Coding>();
        if (coding == null || string.IsNullOrEmpty(coding.Code))
        {
            return false;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchSystem = sp.ValueFhirCodes[i].System ?? string.Empty;
            string searchCode = sp.ValueFhirCodes[i].Value;

            // Verify systems match if specified
            if (!string.IsNullOrEmpty(searchSystem) && 
                !string.IsNullOrEmpty(coding.System) &&
                !coding.System.Equals(searchSystem, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Since terminology service doesn't have working Subsumes method,
            // fallback to exact match for now
            if (coding.Code.Equals(searchCode, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token above for codeableconcept elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if any coding in CodeableConcept subsumes search code.</returns>
    public static bool TestTokenAboveCodeableConcept(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        Hl7.Fhir.Model.CodeableConcept codeableConcept = valueNode.ToPoco<Hl7.Fhir.Model.CodeableConcept>();
        if (codeableConcept?.Coding == null)
        {
            return false;
        }

        // Test each coding in the CodeableConcept for subsumption
        foreach (Hl7.Fhir.Model.Coding coding in codeableConcept.Coding)
        {
            if (string.IsNullOrEmpty(coding.Code))
            {
                continue;
            }

            for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                string searchSystem = sp.ValueFhirCodes[i].System ?? string.Empty;
                string searchCode = sp.ValueFhirCodes[i].Value;

                // Verify systems match if specified
                if (!string.IsNullOrEmpty(searchSystem) && 
                    !string.IsNullOrEmpty(coding.System) &&
                    !coding.System.Equals(searchSystem, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Since terminology service doesn't have working Subsumes method,
                // fallback to exact match for now
                if (coding.Code.Equals(searchCode, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tests token above for identifier elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if identifier matches (identifiers typically don't have subsumption).</returns>
    public static bool TestTokenAboveIdentifier(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        // Identifiers typically don't have subsumption relationships
        // Fall back to exact matching
        return TestTokenAgainstCoding(valueNode, sp);
    }

    /// <summary>Tests token above for contactpoint elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if contact point matches (contact points typically don't have subsumption).</returns>
    public static bool TestTokenAboveContactPoint(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        // ContactPoints typically don't have subsumption relationships
        // Fall back to exact matching
        return TestTokenAgainstCoding(valueNode, sp);
    }

    /// <summary>Tests token above for canonical elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if canonical reference subsumes search canonical.</returns>
    public static bool TestTokenAboveCanonical(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string canonicalUrl = (string)(valueNode?.Value ?? string.Empty);
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

            string searchCanonical = sp.Values[i];

            // Since terminology service doesn't have working Subsumes method,
            // fallback to exact match for now
            if (canonicalUrl.Equals(searchCanonical, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token above for OID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if OID subsumes search OID.</returns>
    public static bool TestTokenAboveOid(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string oidValue = (string)(valueNode?.Value ?? string.Empty);
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if URI concept subsumes search URI concept.</returns>
    public static bool TestTokenAboveUri(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if URL matches (URLs typically don't have subsumption).</returns>
    public static bool TestTokenAboveUrl(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
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

            // Most URLs don't have subsumption relationships
            if (urlValue.Equals(searchUrl, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token above for UUID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if UUID matches (UUIDs don't have subsumption).</returns>
    public static bool TestTokenAboveUuid(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
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

            string searchUuid = sp.Values[i];

            // UUIDs are unique identifiers, not hierarchical
            if (uuidValue.Equals(searchUuid, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token above for string elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if string matches (strings typically don't have subsumption).</returns>
    public static bool TestTokenAboveString(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string stringValue = (string)(valueNode?.Value ?? string.Empty);
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

            string searchString = sp.Values[i];

            // Strings are usually literal matches
            if (stringValue.Equals(searchString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // Token Below Modifier methods - hierarchical child/subsumption search
    
    /// <summary>Tests token below for code elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if search code subsumes resource code.</returns>
    public static bool TestTokenBelowCode(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string resourceCode = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(resourceCode))
        {
            return false;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchCode = sp.ValueFhirCodes[i].Value;

            // Since terminology service doesn't have working Subsumes method,
            // fallback to exact match for now
            if (resourceCode.Equals(searchCode, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token below for coding elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if search coding subsumes resource coding.</returns>
    public static bool TestTokenBelowCoding(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        Hl7.Fhir.Model.Coding coding = valueNode.ToPoco<Hl7.Fhir.Model.Coding>();
        if (coding == null || string.IsNullOrEmpty(coding.Code))
        {
            return false;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string searchSystem = sp.ValueFhirCodes[i].System ?? string.Empty;
            string searchCode = sp.ValueFhirCodes[i].Value;

            // Verify systems match if specified
            if (!string.IsNullOrEmpty(searchSystem) && 
                !string.IsNullOrEmpty(coding.System) &&
                !coding.System.Equals(searchSystem, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Since terminology service doesn't have working Subsumes method,
            // fallback to exact match for now
            if (coding.Code.Equals(searchCode, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token below for codeableconcept elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if any coding in CodeableConcept is subsumed by search code.</returns>
    public static bool TestTokenBelowCodeableConcept(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        Hl7.Fhir.Model.CodeableConcept codeableConcept = valueNode.ToPoco<Hl7.Fhir.Model.CodeableConcept>();
        if (codeableConcept?.Coding == null)
        {
            return false;
        }

        // Test each coding in the CodeableConcept for being subsumed by search code
        foreach (Hl7.Fhir.Model.Coding coding in codeableConcept.Coding)
        {
            if (string.IsNullOrEmpty(coding.Code))
            {
                continue;
            }

            for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                string searchSystem = sp.ValueFhirCodes[i].System ?? string.Empty;
                string searchCode = sp.ValueFhirCodes[i].Value;

                // Verify systems match if specified
                if (!string.IsNullOrEmpty(searchSystem) && 
                    !string.IsNullOrEmpty(coding.System) &&
                    !coding.System.Equals(searchSystem, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Since terminology service doesn't have working Subsumes method,
                // fallback to exact match for now
                if (coding.Code.Equals(searchCode, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tests token below for identifier elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if identifier matches (identifiers typically don't have subsumption).</returns>
    public static bool TestTokenBelowIdentifier(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        // Identifiers typically don't have subsumption relationships
        // Fall back to exact matching
        return TestTokenAgainstCoding(valueNode, sp);
    }

    /// <summary>Tests token below for contactpoint elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if contact point matches (contact points typically don't have subsumption).</returns>
    public static bool TestTokenBelowContactPoint(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        // ContactPoints typically don't have subsumption relationships
        // Fall back to exact matching
        return TestTokenAgainstCoding(valueNode, sp);
    }

    /// <summary>Tests token below for canonical elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if search canonical subsumes resource canonical.</returns>
    public static bool TestTokenBelowCanonical(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string canonicalUrl = (string)(valueNode?.Value ?? string.Empty);
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

            string searchCanonical = sp.Values[i];

            // Since terminology service doesn't have working Subsumes method,
            // fallback to exact match for now
            if (canonicalUrl.Equals(searchCanonical, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token below for OID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if search OID subsumes resource OID.</returns>
    public static bool TestTokenBelowOid(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string oidValue = (string)(valueNode?.Value ?? string.Empty);
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if search URI concept subsumes resource URI concept.</returns>
    public static bool TestTokenBelowUri(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if URL matches (URLs typically don't have subsumption).</returns>
    public static bool TestTokenBelowUrl(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
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

            // Most URLs don't have subsumption relationships
            if (urlValue.Equals(searchUrl, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token below for UUID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if UUID matches (UUIDs don't have subsumption).</returns>
    public static bool TestTokenBelowUuid(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
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

            string searchUuid = sp.Values[i];

            // UUIDs are unique identifiers, not hierarchical
            if (uuidValue.Equals(searchUuid, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token below for string elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if string matches (strings typically don't have subsumption).</returns>
    public static bool TestTokenBelowString(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string stringValue = (string)(valueNode?.Value ?? string.Empty);
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

            string searchString = sp.Values[i];

            // Strings are usually literal matches
            if (stringValue.Equals(searchString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // Token CodeText Modifier methods - text search against code values
    
    /// <summary>Tests token code text for code elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if code value matches search text criteria.</returns>
    public static bool TestTokenCodeTextCode(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string codeValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(codeValue))
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
            if (codeValue.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                codeValue.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token code text for coding elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if coding code value matches search text criteria.</returns>
    public static bool TestTokenCodeTextCoding(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        Hl7.Fhir.Model.Coding coding = valueNode.ToPoco<Hl7.Fhir.Model.Coding>();
        if (coding == null || string.IsNullOrEmpty(coding.Code))
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if any coding code in CodeableConcept matches search text.</returns>
    public static bool TestTokenCodeTextCodeableConcept(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        Hl7.Fhir.Model.CodeableConcept codeableConcept = valueNode.ToPoco<Hl7.Fhir.Model.CodeableConcept>();
        if (codeableConcept?.Coding == null)
        {
            return false;
        }

        // Test each coding in the CodeableConcept
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if identifier value matches search text criteria.</returns>
    public static bool TestTokenCodeTextIdentifier(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        Hl7.Fhir.Model.Identifier identifier = valueNode.ToPoco<Hl7.Fhir.Model.Identifier>();
        if (identifier == null || string.IsNullOrEmpty(identifier.Value))
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
            
            // Case-insensitive text matching against identifier value
            if (identifier.Value.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                identifier.Value.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token code text for contactpoint elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if contact point value matches search text criteria.</returns>
    public static bool TestTokenCodeTextContactPoint(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        Hl7.Fhir.Model.ContactPoint contactPoint = valueNode.ToPoco<Hl7.Fhir.Model.ContactPoint>();
        if (contactPoint == null || string.IsNullOrEmpty(contactPoint.Value))
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
            
            // Case-insensitive text matching against contact value
            if (contactPoint.Value.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                contactPoint.Value.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token code text for canonical elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if canonical URL codes match search text criteria.</returns>
    public static bool TestTokenCodeTextCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string canonicalUrl = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(canonicalUrl))
        {
            return false;
        }

        // Extract code-like portions from canonical URL (last path segment)
        Uri uri;
        if (!Uri.TryCreate(canonicalUrl, UriKind.RelativeOrAbsolute, out uri))
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if OID representation matches search text (rare case).</returns>
    public static bool TestTokenCodeTextOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string oidValue = (string)(valueNode?.Value ?? string.Empty);
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URI codes match search text criteria.</returns>
    public static bool TestTokenCodeTextUri(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string uriValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(uriValue))
        {
            return false;
        }

        // Extract code-like portions from URI (path segments, fragments)
        Uri uri;
        if (!Uri.TryCreate(uriValue, UriKind.RelativeOrAbsolute, out uri))
        {
            return false;
        }

        List<string> codeParts = new List<string>();
        if (uri.Segments != null)
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URL codes match search text criteria.</returns>
    public static bool TestTokenCodeTextUrl(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string urlValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(urlValue))
        {
            return false;
        }

        // Extract meaningful code portions from URL path and query
        Uri uri;
        if (!Uri.TryCreate(urlValue, UriKind.RelativeOrAbsolute, out uri))
        {
            return false;
        }

        List<string> codeParts = new List<string>();
        if (uri.Segments != null)
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if UUID representation matches search text (rare case).</returns>
    public static bool TestTokenCodeTextUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string uuidValue = (string)(valueNode?.Value ?? string.Empty);
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if string value matches search text criteria.</returns>
    public static bool TestTokenCodeTextString(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string stringValue = (string)(valueNode?.Value ?? string.Empty);
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
            
            // Case-insensitive text matching against string value
            if (stringValue.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                stringValue.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // Token NotIn Modifier methods - ValueSet exclusion tests
    
    /// <summary>Tests token not in for code elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if code is NOT in ValueSet.</returns>
    public static bool TestTokenNotInCode(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string codeValue = (string)(valueNode?.Value ?? string.Empty);
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

    /// <summary>Tests token not in for coding elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if coding is NOT in ValueSet.</returns>
    public static bool TestTokenNotInCoding(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        Hl7.Fhir.Model.Coding coding = valueNode.ToPoco<Hl7.Fhir.Model.Coding>();
        if (coding == null || string.IsNullOrEmpty(coding.Code))
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
            
            // Check if coding is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, coding.System ?? string.Empty, coding.Code))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for codeableconcept elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if NO coding in CodeableConcept is in ValueSet.</returns>
    public static bool TestTokenNotInCodeableConcept(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        Hl7.Fhir.Model.CodeableConcept codeableConcept = valueNode.ToPoco<Hl7.Fhir.Model.CodeableConcept>();
        if (codeableConcept?.Coding == null)
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if identifier is NOT in ValueSet.</returns>
    public static bool TestTokenNotInIdentifier(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        Hl7.Fhir.Model.Identifier identifier = valueNode.ToPoco<Hl7.Fhir.Model.Identifier>();
        if (identifier == null || string.IsNullOrEmpty(identifier.Value))
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
            
            // Check if identifier is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, identifier.System ?? string.Empty, identifier.Value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for contactpoint elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if contact point is NOT in ValueSet.</returns>
    public static bool TestTokenNotInContactPoint(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        Hl7.Fhir.Model.ContactPoint contactPoint = valueNode.ToPoco<Hl7.Fhir.Model.ContactPoint>();
        if (contactPoint == null || string.IsNullOrEmpty(contactPoint.Value))
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if canonical is NOT in ValueSet.</returns>
    public static bool TestTokenNotInCanonical(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string canonicalUrl = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(canonicalUrl))
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
            
            // Check if canonical URL is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, canonicalUrl))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for OID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if OID is NOT in ValueSet.</returns>
    public static bool TestTokenNotInOid(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string oidValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(oidValue))
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
            
            // Check if OID is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, oidValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for URI elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if URI is NOT in ValueSet.</returns>
    public static bool TestTokenNotInUri(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string uriValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(uriValue))
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
            
            // Check if URI is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, uriValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for URL elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if URL is NOT in ValueSet.</returns>
    public static bool TestTokenNotInUrl(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string urlValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(urlValue))
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
            
            // Check if URL is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, urlValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for UUID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if UUID is NOT in ValueSet.</returns>
    public static bool TestTokenNotInUuid(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string uuidValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(uuidValue))
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
            
            // Check if UUID is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, uuidValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not in for string elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <param name="store">The FHIR store for terminology services.</param>
    /// <returns>True if string is NOT in ValueSet.</returns>
    public static bool TestTokenNotInString(ITypedElement valueNode, ParsedSearchParameter sp, VersionedFhirStore store)
    {
        string stringValue = (string)(valueNode?.Value ?? string.Empty);
        if (string.IsNullOrEmpty(stringValue))
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
            
            // Check if string is NOT in the ValueSet
            if (!store.Terminology.VsContains(valueSetUri, string.Empty, stringValue))
            {
                return true;
            }
        }

        return false;
    }

    // Token Text Modifier methods - text search against display/text values
    
    /// <summary>Tests token text for code elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if code display text matches search criteria.</returns>
    public static bool TestTokenTextCode(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // Code elements typically don't have display text
        // Return false as codes are just values without associated text
        return false;
    }

    /// <summary>Tests token text for coding elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if coding display text matches search criteria.</returns>
    public static bool TestTokenTextCoding(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        Hl7.Fhir.Model.Coding coding = valueNode.ToPoco<Hl7.Fhir.Model.Coding>();
        if (coding == null || string.IsNullOrEmpty(coding.Display))
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if CodeableConcept text or coding display matches search criteria.</returns>
    public static bool TestTokenTextCodeableConcept(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        Hl7.Fhir.Model.CodeableConcept codeableConcept = valueNode.ToPoco<Hl7.Fhir.Model.CodeableConcept>();
        if (codeableConcept == null)
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
            
            // Check Coding.display for each coding
            if (codeableConcept.Coding != null)
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if identifier type text matches search criteria.</returns>
    public static bool TestTokenTextIdentifier(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        Hl7.Fhir.Model.Identifier identifier = valueNode.ToPoco<Hl7.Fhir.Model.Identifier>();
        if (identifier?.Type == null)
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
            if (!string.IsNullOrEmpty(identifier.Type.Text))
            {
                if (identifier.Type.Text.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) ||
                    identifier.Type.Text.Equals(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            // Check Identifier.type.coding.display
            if (identifier.Type.Coding != null)
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

    /// <summary>Tests token text for contactpoint elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if contact point text matches search criteria.</returns>
    public static bool TestTokenTextContactPoint(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // ContactPoint doesn't typically have display text associated
        // The value itself would be handled by other modifiers
        return false;
    }

    /// <summary>Tests token text for canonical elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if canonical display text matches search criteria.</returns>
    public static bool TestTokenTextCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // Canonical URLs don't have inherent display text
        // Would need to resolve the canonical to get title/name
        // For basic implementation, return false
        return false;
    }

    /// <summary>Tests token text for OID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if OID display text matches search criteria.</returns>
    public static bool TestTokenTextOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // OIDs don't have inherent display text
        // Would need to resolve the OID to get name/description
        // For basic implementation, return false
        return false;
    }

    /// <summary>Tests token text for URI elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URI display text matches search criteria.</returns>
    public static bool TestTokenTextUri(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // URIs don't have inherent display text
        // Would need context to determine associated text
        // For basic implementation, return false
        return false;
    }

    /// <summary>Tests token text for URL elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URL display text matches search criteria.</returns>
    public static bool TestTokenTextUrl(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // URLs don't have inherent display text
        // Would need context to determine associated text
        // For basic implementation, return false
        return false;
    }

    /// <summary>Tests token text for UUID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if UUID display text matches search criteria.</returns>
    public static bool TestTokenTextUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // UUIDs don't have display text
        // Return false
        return false;
    }

    /// <summary>Tests token text for string elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if string value matches search criteria as text.</returns>
    public static bool TestTokenTextString(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string stringValue = (string)(valueNode?.Value ?? string.Empty);
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

    // Token TextAdvanced Modifier methods - advanced text search against display/text values
    
    /// <summary>Tests token advanced text for code elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if code display text matches advanced search criteria.</returns>
    public static bool TestTokenTextAdvancedCode(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // Code elements typically don't have display text
        // Return false as codes are just values without associated text
        return false;
    }

    /// <summary>Tests token advanced text for coding elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if coding display text matches advanced search criteria.</returns>
    public static bool TestTokenTextAdvancedCoding(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        Hl7.Fhir.Model.Coding coding = valueNode.ToPoco<Hl7.Fhir.Model.Coding>();
        if (coding == null || string.IsNullOrEmpty(coding.Display))
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if CodeableConcept text matches advanced search criteria.</returns>
    public static bool TestTokenTextAdvancedCodeableConcept(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        Hl7.Fhir.Model.CodeableConcept codeableConcept = valueNode.ToPoco<Hl7.Fhir.Model.CodeableConcept>();
        if (codeableConcept == null)
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
            
            // Check Coding.display for each coding with advanced processing
            if (codeableConcept.Coding != null)
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
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if identifier type text matches advanced search criteria.</returns>
    public static bool TestTokenTextAdvancedIdentifier(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        Hl7.Fhir.Model.Identifier identifier = valueNode.ToPoco<Hl7.Fhir.Model.Identifier>();
        if (identifier?.Type == null)
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
            if (!string.IsNullOrEmpty(identifier.Type.Text))
            {
                if (ProcessAdvancedTextSearch(identifier.Type.Text, searchText))
                {
                    return true;
                }
            }
            
            // Check Identifier.type.coding.display with advanced processing
            if (identifier.Type.Coding != null)
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

    /// <summary>Tests token advanced text for contactpoint elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if contact point text matches advanced search criteria.</returns>
    public static bool TestTokenTextAdvancedContactPoint(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // ContactPoint doesn't typically have display text associated
        // The value itself would be handled by other modifiers
        return false;
    }

    /// <summary>Tests token advanced text for canonical elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if canonical display text matches advanced search criteria.</returns>
    public static bool TestTokenTextAdvancedCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // Canonical URLs don't have inherent display text
        // Would need to resolve the canonical to get title/name for advanced processing
        // For basic implementation, return false
        return false;
    }

    /// <summary>Tests token advanced text for OID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if OID display text matches advanced search criteria.</returns>
    public static bool TestTokenTextAdvancedOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // OIDs don't have inherent display text
        // Would need to resolve the OID to get name/description for advanced processing
        // For basic implementation, return false
        return false;
    }

    /// <summary>Tests token advanced text for URI elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URI display text matches advanced search criteria.</returns>
    public static bool TestTokenTextAdvancedUri(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // URIs don't have inherent display text
        // Would need context to determine associated text for advanced processing
        // For basic implementation, return false
        return false;
    }

    /// <summary>Tests token advanced text for URL elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if URL display text matches advanced search criteria.</returns>
    public static bool TestTokenTextAdvancedUrl(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // URLs don't have inherent display text
        // Would need context to determine associated text for advanced processing
        // For basic implementation, return false
        return false;
    }

    /// <summary>Tests token advanced text for UUID elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if UUID display text matches advanced search criteria.</returns>
    public static bool TestTokenTextAdvancedUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        // UUIDs don't have display text
        // Return false
        return false;
    }

    /// <summary>Tests token advanced text for string elements.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">The search parameter.</param>
    /// <returns>True if string value matches advanced search criteria.</returns>
    public static bool TestTokenTextAdvancedString(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string stringValue = (string)(valueNode?.Value ?? string.Empty);
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

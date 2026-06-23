// <copyright file="SearchTestString.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text;
using static FhirCandle.Search.SearchDefinitions;

namespace FhirCandle.Search;

/// <summary>A class that contains functions to test string inputs against various FHIR types.</summary>
public static class EvalStringSearch
{
    /// <summary>
    /// Folds a string for case- and accent-insensitive comparison. Forwards to the
    /// shared implementation on <see cref="ParsedSearchParameter"/>; resource-side
    /// strings (HumanName.Family, Address.City, etc.) are folded per-resource via
    /// this helper, while search-side values are pre-folded into
    /// <see cref="ParsedSearchParameter.FoldedValues"/> at parse time.
    /// </summary>
    /// <param name="s">The input string.</param>
    /// <returns>The folded form, or empty if null/empty.</returns>
    private static string FoldForSearch(string? s) => ParsedSearchParameter.FoldForSearch(s);

    /// <summary>Tests a string search value against string-type nodes, using starts-with & case-insensitive.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringStartsWith(PocoNode? valueNode, ParsedSearchParameter sp)
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

        string foldedValue = FoldForSearch(stringValue);

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string? v = sp.FoldedValues?[i];
            if (v is null)
            {
                continue;
            }

            if (foldedValue.StartsWith(v, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests a string search value against string-type nodes, using contains & case-insensitive.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringContains(PocoNode? valueNode, ParsedSearchParameter sp)
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

        string foldedValue = FoldForSearch(stringValue);

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string? v = sp.FoldedValues?[i];
            if (v is null)
            {
                continue;
            }

            if (foldedValue.Contains(v, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests a string search value against string-type nodes, using exact matching (equality & case-sensitive).</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringExact(PocoNode? valueNode, ParsedSearchParameter sp)
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

            if (stringValue.Equals(sp.Values[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests a string search value against a human name (family, given, or text), using starts-with & case-insensitive.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringStartsWithAgainstHumanName(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not HumanName hn))
        {
            return false;
        }

        string foldedFamily = FoldForSearch(hn.Family);
        string foldedText = FoldForSearch(hn.Text);
        string[] foldedGiven = hn.Given?.Select(FoldForSearch).ToArray() ?? [];

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string? v = sp.FoldedValues?[i];
            if (v is null)
            {
                continue;
            }

            if ((!string.IsNullOrEmpty(hn.Family) && foldedFamily.StartsWith(v, StringComparison.OrdinalIgnoreCase)) ||
                foldedGiven.Any(gn => !string.IsNullOrEmpty(gn) && gn.StartsWith(v, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(hn.Text) && foldedText.StartsWith(v, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests a string search value against a human name (family, given, or text), using contains & case-insensitive.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringContainsAgainstHumanName(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not HumanName hn))
        {
            return false;
        }

        string foldedFamily = FoldForSearch(hn.Family);
        string foldedText = FoldForSearch(hn.Text);
        string[] foldedGiven = hn.Given?.Select(FoldForSearch).ToArray() ?? [];

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string? v = sp.FoldedValues?[i];
            if (v is null)
            {
                continue;
            }

            if ((!string.IsNullOrEmpty(hn.Family) && foldedFamily.Contains(v, StringComparison.OrdinalIgnoreCase)) ||
                foldedGiven.Any(gn => !string.IsNullOrEmpty(gn) && gn.Contains(v, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(hn.Text) && foldedText.Contains(v, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests a string search value against a human name (family, given, or text), using exact matching (case-sensitive).</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringExactAgainstHumanName(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not HumanName hn))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string v = sp.Values[i];

            if ((hn.Family?.Equals(v, StringComparison.Ordinal) ?? false) ||
                (hn.Given?.Any(gn => gn?.Equals(v, StringComparison.Ordinal) ?? false) ?? false) ||
                (hn.Text?.Equals(v, StringComparison.Ordinal) ?? false))
            {
                return true;
            }
        }

        return false;
    }


    /// <summary>Tests a string search value against an address, using starts-with & case-insensitive.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringStartsWithAgainstAddress(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not Address nodeVal))
        {
            return false;
        }

        string foldedUse = FoldForSearch(nodeVal.Use?.ToString());
        string foldedType = FoldForSearch(nodeVal.Type?.ToString());
        string foldedText = FoldForSearch(nodeVal.Text);
        string foldedCity = FoldForSearch(nodeVal.City);
        string foldedDistrict = FoldForSearch(nodeVal.District);
        string foldedState = FoldForSearch(nodeVal.State);
        string foldedPostalCode = FoldForSearch(nodeVal.PostalCode);
        string foldedCountry = FoldForSearch(nodeVal.Country);
        string[] foldedLine = nodeVal.Line?.Select(FoldForSearch).ToArray() ?? [];

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string? v = sp.FoldedValues?[i];
            if (v is null)
            {
                continue;
            }

            if ((nodeVal.Use is not null && foldedUse.StartsWith(v, StringComparison.OrdinalIgnoreCase)) ||
                (nodeVal.Type is not null && foldedType.StartsWith(v, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(nodeVal.Text) && foldedText.StartsWith(v, StringComparison.OrdinalIgnoreCase)) ||
                foldedLine.Any(ln => !string.IsNullOrEmpty(ln) && ln.StartsWith(v, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(nodeVal.City) && foldedCity.StartsWith(v, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(nodeVal.District) && foldedDistrict.StartsWith(v, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(nodeVal.State) && foldedState.StartsWith(v, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(nodeVal.PostalCode) && foldedPostalCode.StartsWith(v, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(nodeVal.Country) && foldedCountry.StartsWith(v, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests a string search value against an address, using contains & case-insensitive.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringContainsAgainstAddress(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not Address nodeVal))
        {
            return false;
        }

        string foldedUse = FoldForSearch(nodeVal.Use?.ToString());
        string foldedType = FoldForSearch(nodeVal.Type?.ToString());
        string foldedText = FoldForSearch(nodeVal.Text);
        string foldedCity = FoldForSearch(nodeVal.City);
        string foldedDistrict = FoldForSearch(nodeVal.District);
        string foldedState = FoldForSearch(nodeVal.State);
        string foldedPostalCode = FoldForSearch(nodeVal.PostalCode);
        string foldedCountry = FoldForSearch(nodeVal.Country);
        string[] foldedLine = nodeVal.Line?.Select(FoldForSearch).ToArray() ?? [];

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string? v = sp.FoldedValues?[i];
            if (v is null)
            {
                continue;
            }

            if ((nodeVal.Use is not null && foldedUse.Contains(v, StringComparison.OrdinalIgnoreCase)) ||
                (nodeVal.Type is not null && foldedType.Contains(v, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(nodeVal.Text) && foldedText.Contains(v, StringComparison.OrdinalIgnoreCase)) ||
                foldedLine.Any(ln => !string.IsNullOrEmpty(ln) && ln.Contains(v, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(nodeVal.City) && foldedCity.Contains(v, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(nodeVal.District) && foldedDistrict.Contains(v, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(nodeVal.State) && foldedState.Contains(v, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(nodeVal.PostalCode) && foldedPostalCode.Contains(v, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(nodeVal.Country) && foldedCountry.Contains(v, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests a string search value against an address, using exact matching (case-sensitive).</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringExactAgainstAddress(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is null) ||
            (valueNode.Poco is not Address nodeVal))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string v = sp.Values[i];

            if ((nodeVal.Use?.ToString().Equals(v, StringComparison.Ordinal) ?? false) ||
                (nodeVal.Type?.ToString().Equals(v, StringComparison.Ordinal) ?? false) ||
                (nodeVal.Text?.Equals(v, StringComparison.Ordinal) ?? false) ||
                (nodeVal.Line?.Any(v => v?.Equals(v, StringComparison.Ordinal) ?? false) ?? false) ||
                (nodeVal.City?.Equals(v, StringComparison.Ordinal) ?? false) ||
                (nodeVal.District?.Equals(v, StringComparison.Ordinal) ?? false) ||
                (nodeVal.State?.Equals(v, StringComparison.Ordinal) ?? false) ||
                (nodeVal.PostalCode?.Equals(v, StringComparison.Ordinal) ?? false) ||
                (nodeVal.Country?.Equals(v, StringComparison.Ordinal) ?? false))
            {
                return true;
            }
        }

        return false;
    }
}

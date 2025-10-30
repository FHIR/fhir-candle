// <copyright file="SearchTestString.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Newtonsoft.Json.Linq;
using static FhirCandle.Search.SearchDefinitions;

namespace FhirCandle.Search;

/// <summary>A class that contains functions to test string inputs against various FHIR types.</summary>
public static class EvalStringSearch
{
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

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (stringValue.StartsWith(sp.Values[i], StringComparison.OrdinalIgnoreCase))
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

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (stringValue.Contains(sp.Values[i], StringComparison.OrdinalIgnoreCase))
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

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string v = sp.Values[i];

            if ((hn.Family?.StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (hn.Given?.Any(gn => gn?.StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false) ?? false) ||
                (hn.Text?.StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false))
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

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string v = sp.Values[i];

            if ((hn.Family?.Contains(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (hn.Given?.Any(gn => gn?.Contains(v, StringComparison.OrdinalIgnoreCase) ?? false) ?? false) ||
                (hn.Text?.Contains(v, StringComparison.OrdinalIgnoreCase) ?? false))
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

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string v = sp.Values[i];

            if ((nodeVal.Use?.ToString().StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (nodeVal.Type?.ToString().StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (nodeVal.Text?.StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (nodeVal.Line?.Any(v => v?.StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false) ?? false) ||
                (nodeVal.City?.StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (nodeVal.District?.StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (nodeVal.State?.StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (nodeVal.PostalCode?.StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (nodeVal.Country?.StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false))
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

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string v = sp.Values[i];

            if ((nodeVal.Use?.ToString().Contains(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (nodeVal.Type?.ToString().Contains(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (nodeVal.Text?.Contains(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (nodeVal.Line?.Any(v => v?.Contains(v, StringComparison.OrdinalIgnoreCase) ?? false) ?? false) ||
                (nodeVal.City?.Contains(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (nodeVal.District?.Contains(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (nodeVal.State?.Contains(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (nodeVal.PostalCode?.Contains(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (nodeVal.Country?.Contains(v, StringComparison.OrdinalIgnoreCase) ?? false))
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

// <copyright file="EvalQuantitySearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using static FhirCandle.Search.SearchDefinitions;

namespace FhirCandle.Search;

/// <summary>A class that contains functions to test quantity inputs against various FHIR types.</summary>
public static class EvalQuantitySearch
{
    /// <summary>Units match.</summary>
    /// <param name="s1">The first system.</param>
    /// <param name="c1">The first code.</param>
    /// <param name="u1">The first unit.</param>
    /// <param name="s2">The second system.</param>
    /// <param name="c2">The second code.</param>
    /// <returns>True if they match, false if they do not.</returns>
    private static bool UnitsMatch(string s1, string c1, string u1, string s2, string c2)
    {
        if (string.IsNullOrEmpty(s1) || 
            string.IsNullOrEmpty(s2) ||
            s1.Equals(s2, StringComparison.OrdinalIgnoreCase))
        {
            // if either code is missing or they match, return true
            return (string.IsNullOrEmpty(c1) && string.IsNullOrEmpty(u1)) ||
                string.IsNullOrEmpty(c2) ||
                c2.Equals(c1, StringComparison.OrdinalIgnoreCase) ||
                c2.Equals(u1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>Tests quantity.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestQuantity(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode?.Poco is not Quantity q) ||
            (q.Value is null))
        {
            return false;
        }

        decimal value = q.Value.Value;

        if (sp.ValueDecimals is null)
        {
            return false;
        }

        // traverse values and possibly prefixes
        for (int i = 0; i < sp.ValueDecimals.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            // TODO: right now only compare if units match - should instead test for unit class matches and do conversion
            if (!UnitsMatch(
                    q.System ?? string.Empty,
                    q.Code ?? string.Empty,
                    q.Unit ?? string.Empty,
                    sp.ValueFhirCodes?[i].System ?? string.Empty, 
                    sp.ValueFhirCodes?[i].Value ?? string.Empty))
            {
                continue;
            }

            // either grab the prefix or default to equality (default prefix is equality)
            SearchPrefixCodes prefix =
                ((sp.Prefixes?.Length ?? 0) > i)
                ? sp.Prefixes![i] ?? SearchPrefixCodes.Equal
                : SearchPrefixCodes.Equal;

            // TODO: figure out what to do with a quantity comparator

            // perform a comparison based on the comparator
            switch (prefix)
            {
                case SearchPrefixCodes.Equal:
                default:

                    // TODO: This is not proper decimal comparison as defined in the spec.
                    if (decimal.Abs(decimal.Round(value - sp.ValueDecimals[i], 0)) == 0)
                    {
                        return true;
                    }

                    break;

                case SearchPrefixCodes.NotEqual:

                    // TODO: This is not proper decimal comparison as defined in the spec.
                    if (decimal.Abs(decimal.Round(value - sp.ValueDecimals[i], 0)) != 0)
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.GreaterThan:
                    if (value > sp.ValueDecimals[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.LessThan:
                    if (value < sp.ValueDecimals[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.GreaterThanOrEqual:
                    if (value >= sp.ValueDecimals[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.LessThanOrEqual:
                    if (value <= sp.ValueDecimals[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.StartsAfter:
                    if (value > sp.ValueDecimals[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.EndsBefore:
                    if (value < sp.ValueDecimals[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.Approximately:

                    // values within 10% should match
                    if ((decimal.Abs(value - sp.ValueDecimals[i]) / decimal.Abs(value)) < 0.1m)
                    {
                        return true;
                    }
                    break;
            }
        }

        // if we did not find a match, this test failed
        return false;

    }
}

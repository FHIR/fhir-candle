// <copyright file="EvalNumberSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using static FhirCandle.Search.SearchDefinitions;

namespace FhirCandle.Search;

/// <summary>An eval number search.</summary>
public static class EvalNumberSearch
{
    /// <summary>Tests a number search value against 64-bit integer-type nodes.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestNumberAgainstLong(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        long? elementValue;

        switch (valueNode.Poco)
        {
            case Integer fi:
                elementValue = fi.Value;
                break;

            case UnsignedInt fui:
                elementValue = fui.Value;
                break;

            case PositiveInt fpi:
                elementValue = fpi.Value;
                break;

            case Integer64 fl:
                elementValue = fl.Value;
                break;

            default:
                return false;
        }

        if ((elementValue is null) ||
            (sp.ValueInts is null))
        {
            return false;
        }

        // traverse values and possibly prefixes
        for (int i = 0; i < sp.ValueInts.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            // either grab the prefix or default to equality (number default prefix is equality)
            SearchPrefixCodes prefix =
                ((sp.Prefixes?.Length ?? 0) > i)
                ? sp.Prefixes![i] ?? SearchPrefixCodes.Equal
                : SearchPrefixCodes.Equal;

            switch (prefix)
            {
                case SearchPrefixCodes.Equal:
                default:
                    if (elementValue == sp.ValueInts[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.NotEqual:
                    if (elementValue != sp.ValueInts[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.GreaterThan:
                    if (elementValue > sp.ValueInts[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.LessThan:
                    if (elementValue < sp.ValueInts[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.GreaterThanOrEqual:
                    if (elementValue >= sp.ValueInts[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.LessThanOrEqual:
                    if (elementValue <= sp.ValueInts[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.StartsAfter:
                    if (elementValue > sp.ValueInts[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.EndsBefore:
                    if (elementValue < sp.ValueInts[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.Approximately:
                    if (Math.Abs((double)(elementValue - sp.ValueInts[i])) <= 1.0)
                    {
                        return true;
                    }
                    break;
            }
        }

        // if we did not find a match, this test failed
        return false;
    }

    /// <summary>Tests a number search value against decimal-type nodes.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestNumberAgainstDecimal(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        if ((valueNode.Poco is not FhirDecimal fd) ||
            (fd.Value is not decimal elementValue))
        {
            return false;
        }

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

            // either grab the prefix or default to equality (number default prefix is equality)
            SearchPrefixCodes prefix =
                ((sp.Prefixes?.Length ?? 0) > i)
                ? sp.Prefixes![i] ?? SearchPrefixCodes.Equal
                : SearchPrefixCodes.Equal;

            switch (prefix)
            {
                case SearchPrefixCodes.Equal:
                default:

                    // TODO: This is not proper decimal comparison as defined in the spec.

                    //int digits = testValue.GetSignificantDigitCount();

                    if (decimal.Abs(decimal.Round(elementValue - sp.ValueDecimals[i], 0)) == 0)
                    {
                        return true;
                    }

                    break;

                case SearchPrefixCodes.NotEqual:

                    // TODO: This is not proper decimal comparison as defined in the spec.
                    if (decimal.Abs(decimal.Round(elementValue - sp.ValueDecimals[i], 0)) != 0)
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.GreaterThan:
                    if (elementValue > sp.ValueDecimals[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.LessThan:
                    if (elementValue < sp.ValueDecimals[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.GreaterThanOrEqual:
                    if (elementValue >= sp.ValueDecimals[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.LessThanOrEqual:
                    if (elementValue <= sp.ValueDecimals[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.StartsAfter:
                    if (elementValue > sp.ValueDecimals[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.EndsBefore:
                    if (elementValue < sp.ValueDecimals[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.Approximately:

                    // values within 10% should match
                    if ((decimal.Abs(elementValue - sp.ValueDecimals[i]) / decimal.Abs(elementValue)) < 0.1m)
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

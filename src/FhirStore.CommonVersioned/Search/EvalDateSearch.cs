// <copyright file="EvalDateSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using System.Globalization;
using static FhirCandle.Search.SearchDefinitions;

namespace FhirCandle.Search;

/// <summary>A class that contains functions to test date inputs against various FHIR types.</summary>
public static class EvalDateSearch
{
    /// <summary>Performs a search test for a date type.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public static bool TestDate(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return false;
        }

        DateTimeOffset valueStart = DateTimeOffset.MinValue;
        DateTimeOffset valueEnd = DateTimeOffset.MaxValue;

        switch (valueNode.Poco)
        {
            case Instant fi:
                valueStart = fi.Value ?? DateTimeOffset.MinValue;
                valueEnd = fi.Value ?? DateTimeOffset.MaxValue;
                break;

            case FhirDateTime fdt:
                if (!ParsedSearchParameter.TryParseFhirDate(fdt.Value, out valueStart, out valueEnd))
                {
                    return false;
                }
                break;

            case Date fd:
                if (!ParsedSearchParameter.TryParseFhirDate(fd.Value, out valueStart, out valueEnd))
                {
                    return false;
                }
                break;

            // Note that there is currently no way to actually search for a time
            //case Hl7.Fhir.ElementModel.Types.Time fhirTime:
            //    break;

            case Period fp:
                valueStart = fp.StartElement?.ToDateTimeOffset(TimeSpan.Zero) ?? DateTimeOffset.MinValue;
                valueEnd = fp.EndElement?.ToDateTimeOffset(TimeSpan.Zero) ?? DateTimeOffset.MaxValue;
                break;

            case Timing ft:
                if (ft.EventElement.Count != 0)
                {
                    valueStart = ft.EventElement.Min()?.ToDateTimeOffset(TimeSpan.Zero) ?? DateTimeOffset.MinValue;
                    valueEnd = ft.EventElement.Max()?.ToDateTimeOffset(TimeSpan.Zero) ?? DateTimeOffset.MaxValue;
                }
                else if (ft.Repeat?.Bounds is Period boundsPeriod)
                {
                    valueStart = boundsPeriod.StartElement?.ToDateTimeOffset(TimeSpan.Zero) ?? DateTimeOffset.MinValue;
                    valueEnd = boundsPeriod.EndElement?.ToDateTimeOffset(TimeSpan.Zero) ?? DateTimeOffset.MaxValue;
                }
                else if (ft.Repeat?.Bounds is FhirDateTime boundsDateTime)
                {
                    if (!ParsedSearchParameter.TryParseFhirDate(boundsDateTime.Value, out valueStart, out valueEnd))
                    {
                        return false;
                    }
                }
                else
                {
                    // TODO: need to check boundsRange, look for others
                    return false;
                }

                break;

            default:
                Console.WriteLine($"Unknown valueNode type: {valueNode.GetValue()?.GetType()}");
                break;
        }

        if ((sp.ValueDateStarts is null) || 
            (sp.ValueDateEnds is null) ||
            (sp.ValueDateStarts.Length != sp.ValueDateEnds.Length))
        {
            return false;
        }

        // traverse values and prefixes
        for (int i = 0; i < sp.ValueDateStarts.Length; i++)
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

                    if ((valueStart >= sp.ValueDateStarts[i]) && (valueEnd <= sp.ValueDateEnds[i]))
                    {
                        return true;
                    }

                    break;

                case SearchPrefixCodes.NotEqual:

                    if ((valueStart != sp.ValueDateStarts[i]) || (valueEnd != sp.ValueDateEnds[i]))
                    {
                        return true;
                    }

                    break;

                case SearchPrefixCodes.GreaterThan:
                    if (valueEnd > sp.ValueDateEnds[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.LessThan:
                    if (valueEnd < sp.ValueDateEnds[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.GreaterThanOrEqual:
                    if (valueEnd >= sp.ValueDateEnds[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.LessThanOrEqual:
                    if (valueEnd <= sp.ValueDateEnds[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.StartsAfter:
                    if (valueStart > sp.ValueDateEnds[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.EndsBefore:
                    if (valueEnd < sp.ValueDateStarts[i])
                    {
                        return true;
                    }
                    break;

                case SearchPrefixCodes.Approximately:
                    // TODO: this is not correct date approximation since it does not account for precision, but works well enough for now
                    if ((valueStart.Subtract(sp.ValueDateStarts[i]) < TimeSpan.FromDays(1)) ||
                        (valueEnd.Subtract(sp.ValueDateEnds[i]) < TimeSpan.FromDays(1)))
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

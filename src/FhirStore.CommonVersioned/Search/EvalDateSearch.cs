// <copyright file="EvalDateSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using Hl7.Fhir.ElementModel;
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
    public static bool TestDate(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null)
        {
            return false;
        }

        DateTimeOffset valueStart = DateTimeOffset.MinValue;
        DateTimeOffset valueEnd = DateTimeOffset.MaxValue;

        switch (valueNode.Value)
        {
            case Hl7.Fhir.ElementModel.Types.DateTime fhirDateTime:
                valueStart = fhirDateTime.ToDateTimeOffset(TimeSpan.Zero);

                switch (fhirDateTime.Precision)
                {
                    case Hl7.Fhir.ElementModel.Types.DateTimePrecision.Year:
                        valueEnd = valueStart.AddYears(1).AddTicks(-1);
                        break;

                    case Hl7.Fhir.ElementModel.Types.DateTimePrecision.Month:
                        valueEnd = valueStart.AddMonths(1).AddTicks(-1);
                        break;

                    case Hl7.Fhir.ElementModel.Types.DateTimePrecision.Day:
                        valueEnd = valueStart.AddDays(1).AddTicks(-1);
                        break;

                    case Hl7.Fhir.ElementModel.Types.DateTimePrecision.Hour:
                        valueEnd = valueStart.AddHours(1).AddTicks(-1);
                        break;

                    case Hl7.Fhir.ElementModel.Types.DateTimePrecision.Minute:
                        valueEnd = valueStart.AddMinutes(1).AddTicks(-1);
                        break;

                    // we choose to igore fractions of seconds
                    case Hl7.Fhir.ElementModel.Types.DateTimePrecision.Second:
                    case Hl7.Fhir.ElementModel.Types.DateTimePrecision.Fraction:
                        valueEnd = valueStart.AddSeconds(1).AddTicks(-1);
                        break;
                }
                break;

            case Hl7.Fhir.ElementModel.Types.Date fhirDate:
                valueStart = fhirDate.ToDateTimeOffset(0, 0, 0, TimeSpan.Zero);

                switch (fhirDate.Precision)
                {
                    case Hl7.Fhir.ElementModel.Types.DateTimePrecision.Year:
                        valueEnd = valueStart.AddYears(1).AddTicks(-1);
                        break;

                    case Hl7.Fhir.ElementModel.Types.DateTimePrecision.Month:
                        valueEnd = valueStart.AddMonths(1).AddTicks(-1);
                        break;

                    case Hl7.Fhir.ElementModel.Types.DateTimePrecision.Day:
                        valueEnd = valueStart.AddDays(1).AddTicks(-1);
                        break;

                    case Hl7.Fhir.ElementModel.Types.DateTimePrecision.Hour:
                        valueEnd = valueStart.AddHours(1).AddTicks(-1);
                        break;

                    case Hl7.Fhir.ElementModel.Types.DateTimePrecision.Minute:
                        valueEnd = valueStart.AddMinutes(1).AddTicks(-1);
                        break;

                    // we choose to ignore fractions of seconds
                    case Hl7.Fhir.ElementModel.Types.DateTimePrecision.Second:
                    case Hl7.Fhir.ElementModel.Types.DateTimePrecision.Fraction:
                        valueEnd = valueStart.AddSeconds(1).AddTicks(-1);
                        break;
                }
                break;

            // Note that there is currently no way to actually search for a time
            //case Hl7.Fhir.ElementModel.Types.Time fhirTime:
            //    break;

            case Hl7.Fhir.Model.Period fhirPeriod:
                valueStart = fhirPeriod.StartElement?.ToDateTimeOffset(TimeSpan.Zero) ?? DateTimeOffset.MinValue;
                valueEnd = fhirPeriod.EndElement?.ToDateTimeOffset(TimeSpan.Zero) ?? DateTimeOffset.MaxValue;
                break;

            case Hl7.Fhir.Model.Timing fhirTiming:
                if (fhirTiming.EventElement.Any())
                {
                    // TODO: this is iterating over the elements twice, should change to single pass
                    // add an extension method .MinAndMax() that returns both
                    valueStart = fhirTiming.EventElement.Min()?.ToDateTimeOffset(TimeSpan.Zero) ?? DateTimeOffset.MinValue;
                    valueEnd = fhirTiming.EventElement.Max()?.ToDateTimeOffset(TimeSpan.Zero) ?? DateTimeOffset.MaxValue;
                }
                else
                {
                    // for now, do not try and figure out how to search within repetitions
                    return false;
                }
                break;

            default:
                Console.WriteLine($"Unknown valueNote type: {valueNode.Value.GetType()}");
                break;
        }

        if ((sp.ValueDateStarts == null) || 
            (sp.ValueDateEnds == null) ||
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

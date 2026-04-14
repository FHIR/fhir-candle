// <copyright file="FhirSortComparer.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using FhirCandle.Storage;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Model.CdsHooks;
using System;
using System.ComponentModel;
using static FhirCandle.Search.SearchDefinitions;

namespace FhirCandle.Search;

public class FhirSortComparer : IComparer<Resource>
{
    private VersionedFhirStore _store;
    private ParsedResultParameters.SortRequest[] _sorting;

    public FhirSortComparer(
        VersionedFhirStore store,
        ParsedResultParameters.SortRequest[] sortRequests)
    {
        _store = store;
        _sorting = sortRequests;
    }

    public int Compare(Resource? x, Resource? y)
    {
        if ((x is null) && (y is null))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        // if there is nothing to sort, just go in order of the resources
        if (_sorting.Length == 0)
        {
            return 1;
        }

        PocoNode xPN = x.ToPocoNode();
        FhirEvaluationContext xFpc = new()
        {
            Resource = xPN,
            TerminologyService = _store.Terminology,
            ElementResolver = _store.Resolve,
        };

        PocoNode yPN = y.ToPocoNode();
        FhirEvaluationContext yFpc = new()
        {
            Resource = yPN,
            TerminologyService = _store.Terminology,
            ElementResolver = _store.Resolve,
        };

        foreach (ParsedResultParameters.SortRequest sr in _sorting)
        {
            PocoNode? xValue = sr.Compiled.Invoke(xPN, xFpc).FirstOrDefault();
            PocoNode? yValue = sr.Compiled.Invoke(yPN, yFpc).FirstOrDefault();

            if ((xValue is null) && (yValue is null))
            {
                continue;
            }

            if (xValue is null)
            {
                return sr.Ascending ? -1 : 1;
            }

            if (yValue is null)
            {
                return sr.Ascending ? 1 : -1;
            }

            if ((xValue.Poco is IComparable xComp) &&
                (yValue.Poco is IComparable yComp))
            {
                int result = xComp.CompareTo(yComp);
                if (result != 0)
                {
                    return sr.Ascending ? result : -result;
                }

                continue;
            }

            if ((xValue is IFhirValueProvider xFvp) &&
                (yValue is IFhirValueProvider yFvp))
            {
                switch (xFvp.FhirValue)
                {
                    case IComparable xIC:
                        if (yFvp.FhirValue is IComparable yIC)
                        {
                            int result = xIC.CompareTo(yIC);
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        break;

                    case Period xPeriod:
                        if (yFvp.FhirValue is Period yPeriod)
                        {
                            int result = xPeriod.Start?.CompareTo(yPeriod.Start) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                            result = xPeriod.End?.CompareTo(yPeriod.End) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        break;

                    case HumanName xHN:
                        if (yFvp.FhirValue is HumanName yHN)
                        {
                            if ((xHN.Text is not null) && (yHN.Text is not null))
                            {
                                int result = xHN.Text.CompareTo(yHN.Text);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xHN.Family is not null) && (yHN.Family is not null))
                            {
                                int result = xHN.Family.CompareTo(yHN.Family);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xHN.Given?.Any() == true) &&
                                (yHN.Given?.Any() == true))
                            {
                                int result = string.Join(' ', xHN.Given).CompareTo(string.Join(' ', yHN.Given));
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }
                        }
                        break;

                    case Address xAddress:
                        if (yFvp.FhirValue is Address yAddress)
                        {
                            if ((xAddress.Text is not null) && (yAddress.Text is not null))
                            {
                                int result = xAddress.Text.CompareTo(yAddress.Text);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xAddress.City is not null) && (yAddress.City is not null))
                            {
                                int result = xAddress.City.CompareTo(yAddress.City);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xAddress.State is not null) && (yAddress.State is not null))
                            {
                                int result = xAddress.State.CompareTo(yAddress.State);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xAddress.PostalCode is not null) && (yAddress.PostalCode is not null))
                            {
                                int result = xAddress.PostalCode.CompareTo(yAddress.PostalCode);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xAddress.Country is not null) && (yAddress.Country is not null))
                            {
                                int result = xAddress.Country.CompareTo(yAddress.Country);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }
                        }
                        break;

                    case ResourceReference xRR:
                        if (yFvp.FhirValue is ResourceReference yRR)
                        {
                            if ((xRR.Display is not null) && (yRR.Display is not null))
                            {
                                int result = xRR.Display.CompareTo(yRR.Display);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xRR.Reference is not null) && (yRR.Reference is not null))
                            {
                                int result = xRR.Reference.CompareTo(yRR.Reference);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xRR.Identifier is not null) && (yRR.Identifier is not null))
                            {
                                int sResult = (xRR.Identifier.System ?? string.Empty).CompareTo(yRR.Identifier.System ?? string.Empty);
                                int vResult = (xRR.Identifier.Value ?? string.Empty).CompareTo(yRR.Identifier.Value ?? string.Empty);

                                if ((sResult == 0) && (vResult != 0))
                                {
                                    return sr.Ascending ? vResult : -vResult;
                                }
                            }
                        }
                        break;

                    default:
                        //if (false)
                        //{
                        //    Console.Write("");
                        //}
                        break;
                }
            }
        }

        return 0;
    }
}

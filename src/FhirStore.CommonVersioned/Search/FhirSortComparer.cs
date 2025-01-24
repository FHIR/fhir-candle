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
        if ((x == null) && (y == null))
        {
            return 0;
        }

        if (x == null)
        {
            return -1;
        }

        if (y == null)
        {
            return 1;
        }

        // if there is nothing to sort, just go in order of the resources
        if (_sorting.Length == 0)
        {
            return 1;
        }

        ITypedElement xTE = x.ToTypedElement();
        FhirEvaluationContext xFpc = new()
        {
            Resource = xTE,
            TerminologyService = _store.Terminology,
            ElementResolver = _store.Resolve,
        };

        ITypedElement yTE = y.ToTypedElement();
        FhirEvaluationContext yFpc = new()
        {
            Resource = yTE,
            TerminologyService = _store.Terminology,
            ElementResolver = _store.Resolve,
        };

        foreach (ParsedResultParameters.SortRequest sr in _sorting)
        {
            ITypedElement? xValue = sr.Compiled.Invoke(xTE, xFpc).FirstOrDefault();
            ITypedElement? yValue = sr.Compiled.Invoke(yTE, yFpc).FirstOrDefault();

            if ((xValue == null) && (yValue == null))
            {
                continue;
            }

            if (xValue == null)
            {
                return sr.Ascending ? -1 : 1;
            }

            if (yValue == null)
            {
                return sr.Ascending ? 1 : -1;
            }

            if ((xValue.Value != null) &&
                (xValue.Value is IComparable xComp) &&
                (yValue.Value != null) &&
                (yValue.Value is IComparable yComp))
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
                //if (xFvp.FhirValue.TypeName != yFvp.FhirValue.TypeName)
                //{
                //    continue;
                //}

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
                            if ((xHN.Text != null) && (yHN.Text != null))
                            {
                                int result = xHN.Text.CompareTo(yHN.Text);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xHN.Family != null) && (yHN.Family != null))
                            {
                                int result = xHN.Family.CompareTo(yHN.Family);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xHN.Given != null) && xHN.Given.Any() &&
                                (yHN.Given != null) && yHN.Given.Any())
                            {
                                int result = xHN.Given.First().CompareTo(yHN.Given.First());
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
                            if ((xAddress.Text != null) && (yAddress.Text != null))
                            {
                                int result = xAddress.Text.CompareTo(yAddress.Text);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xAddress.City != null) && (yAddress.City != null))
                            {
                                int result = xAddress.City.CompareTo(yAddress.City);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xAddress.State != null) && (yAddress.State != null))
                            {
                                int result = xAddress.State.CompareTo(yAddress.State);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xAddress.PostalCode != null) && (yAddress.PostalCode != null))
                            {
                                int result = xAddress.PostalCode.CompareTo(yAddress.PostalCode);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xAddress.Country != null) && (yAddress.Country != null))
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
                            if ((xRR.Display != null) && (yRR.Display != null))
                            {
                                int result = xRR.Display.CompareTo(yRR.Display);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xRR.Reference != null) && (yRR.Reference != null))
                            {
                                int result = xRR.Reference.CompareTo(yRR.Reference);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xRR.Identifier != null) && (yRR.Identifier != null))
                            {
                                int sResult = xRR.Identifier.System.CompareTo(yRR.Identifier.System);
                                int vResult = xRR.Identifier.Value.CompareTo(yRR.Identifier.Value);

                                if ((sResult == 0) && (vResult != 0))
                                {
                                    return sr.Ascending ? vResult : -vResult;
                                }
                            }
                        }
                        break;

                    default:
                        if (false)
                        {
                            Console.Write("");
                        }
                        break;
                }


                //if ((xFvp.FhirValue is IComparable xFvc) &&
                //    (yFvp.FhirValue is IComparable yFvc))
                //{
                //    int result = xFvc.CompareTo(yFvc);
                //    if (result != 0)
                //    {
                //        return sr.Ascending ? result : -result;
                //    }

                //    continue;
                //}

                //if ((xFvp.FhirValue is Period xPeriod) &&
                //    (yFvp.FhirValue is Period yPeriod))
                //{
                //    int result = xPeriod.Start?.CompareTo(yPeriod.Start) ?? 0;
                //    if (result != 0)
                //    {
                //        return sr.Ascending ? result : -result;
                //    }

                //    result = xPeriod.End?.CompareTo(yPeriod.End) ?? 0;
                //    if (result != 0)
                //    {
                //        return sr.Ascending ? result : -result;
                //    }
                //}
            }

            //if (xValue.Value == null)
            //{
            //    return sr.Ascending ? -1 : 1;
            //}

            //if (yValue.Value == null)
            //{
            //    return sr.Ascending ? 1 : -1;
            //}

        }

        return 0;
    }
}

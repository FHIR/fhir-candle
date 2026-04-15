// <copyright file="FhirSortComparer.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System;
using System.ComponentModel;
using System.Reflection.Metadata;
using FhirCandle.Models;
using FhirCandle.Storage;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Model.CdsHooks;
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

            // handle special cases we want

            if ((xValue is IFhirValueProvider xFvp) &&
                (yValue is IFhirValueProvider yFvp))
            {
                switch (xFvp.FhirValue)
                {
                    //case IComparable xIC:
                    //    if (yFvp.FhirValue is IComparable yIC)
                    //    {
                    //        int result = xIC.CompareTo(yIC);
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    break;

                    case FhirBoolean xBool:
                        if (yFvp.FhirValue is FhirBoolean yBool)
                        {
                            if (xBool.Value == yBool.Value)
                            {
                                return 0;
                            }
                            else if ((sr.Ascending && (xBool.Value == true)) ||
                                     (!sr.Ascending && (yBool.Value == true)))
                            {
                                return -1;
                            }
                            else
                            {
                                return 1;
                            }
                        }
                        break;

                    case Integer xInt:
                        if (yFvp.FhirValue is Integer yInt)
                        {
                            int result = xInt.Value?.CompareTo(yInt.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is UnsignedInt yUI)
                        {
                            int result = xInt.Value?.CompareTo(yUI.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is PositiveInt yPI)
                        {
                            int result = xInt.Value?.CompareTo(yPI.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is Integer64 yI64)
                        {
                            int result = xInt.Value?.CompareTo(yI64.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        break;

                    case UnsignedInt xUInt:
                        if (yFvp.FhirValue is UnsignedInt yUInt)
                        {
                            int result = xUInt.Value?.CompareTo(yUInt.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is Integer yI)
                        {
                            int result = xUInt.Value?.CompareTo(yI.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is PositiveInt yPI)
                        {
                            int result = xUInt.Value?.CompareTo(yPI.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is Integer64 yI64)
                        {
                            int result = xUInt.Value?.CompareTo(yI64.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        break;

                    case PositiveInt xPInt:
                        if (yFvp.FhirValue is PositiveInt yPInt)
                        {
                            int result = xPInt.Value?.CompareTo(yPInt.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is Integer yI)
                        {
                            int result = xPInt.Value?.CompareTo(yI.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is UnsignedInt yUI)
                        {
                            int result = xPInt.Value?.CompareTo(yUI.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is Integer64 yI64)
                        {
                            int result = xPInt.Value?.CompareTo(yI64.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        break;

                    case Integer64 xInt64:
                        if (yFvp.FhirValue is Integer64 yInt64)
                        {
                            int result = xInt64.Value?.CompareTo(yInt64.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is Integer yI)
                        {
                            int result = xInt64.Value?.CompareTo(yI.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is UnsignedInt yUI)
                        {
                            int result = xInt64.Value?.CompareTo(yUI.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is PositiveInt yPI)
                        {
                            int result = xInt64.Value?.CompareTo(yPI.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        break;

                    //case FhirDecimal xDecimal:
                    //    if (yFvp.FhirValue is FhirDecimal yDecimal)
                    //    {
                    //        int result = xDecimal.Value?.CompareTo(yDecimal.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    break;

                    case Date xDate:
                        if (yFvp.FhirValue is Date yDate)
                        {
                            int result = xDate.ToDateTimeOffset().CompareTo(yDate.ToDateTimeOffset());
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is FhirDateTime yDT)
                        {
                            int result = xDate.ToDateTimeOffset().CompareTo(yDT.ToDateTimeOffset(TimeSpan.Zero));
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is Instant yI)
                        {
                            int result = xDate.ToDateTimeOffset().CompareTo(yI.Value!.Value);
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }

                        break;

                    case FhirDateTime xDateTime:
                        if (yFvp.FhirValue is FhirDateTime yDateTime)
                        {
                            int result = xDateTime.ToDateTimeOffset(TimeSpan.Zero).CompareTo(yDateTime.ToDateTimeOffset(TimeSpan.Zero));
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is Hl7.Fhir.Model.Date yD)
                        {
                            int result = xDateTime.ToDateTimeOffset(TimeSpan.Zero).CompareTo(yD.ToDateTimeOffset());
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is Instant yI)
                        {
                            int result = xDateTime.ToDateTimeOffset(TimeSpan.Zero).CompareTo(yI.Value!.Value);
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }

                        break;

                    case Instant xInstant:
                        if (yFvp.FhirValue is Instant yInstant)
                        {
                            int result = xInstant.Value!.Value.CompareTo(yInstant.Value!.Value);
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is FhirDateTime yDT)
                        {
                            int result = xInstant.Value!.Value.CompareTo(yDT.ToDateTimeOffset(TimeSpan.Zero));
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }
                        else if (yFvp.FhirValue is Hl7.Fhir.Model.Date yD)
                        {
                            int result = xInstant.Value!.Value.CompareTo(yD.ToDateTimeOffset());
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }

                        break;

                    case Period xPeriod:
                        if (yFvp.FhirValue is Period yPeriod)
                        {
                            if ((xPeriod.StartElement is not null) && (yPeriod.StartElement is not null))
                            {
                                int result = xPeriod.StartElement!.ToDateTimeOffset(TimeSpan.Zero).CompareTo(yPeriod.StartElement!.ToDateTimeOffset(TimeSpan.Zero));
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xPeriod.EndElement is not null) && (yPeriod.EndElement is not null))
                            {
                                int result = xPeriod.EndElement!.ToDateTimeOffset(TimeSpan.Zero).CompareTo(yPeriod.EndElement!.ToDateTimeOffset(TimeSpan.Zero));
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xPeriod.StartElement is not null) && (yPeriod.EndElement is not null))
                            {
                                int result = xPeriod.StartElement!.ToDateTimeOffset(TimeSpan.Zero).CompareTo(yPeriod.EndElement!.ToDateTimeOffset(TimeSpan.Zero));
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }

                            if ((xPeriod.EndElement is not null) && (yPeriod.StartElement is not null))
                            {
                                int result = xPeriod.EndElement!.ToDateTimeOffset(TimeSpan.Zero).CompareTo(yPeriod.StartElement!.ToDateTimeOffset(TimeSpan.Zero));
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }
                            }
                        }
                        break;

                    case Time xTime:
                        if (yFvp.FhirValue is Time yTime)
                        {
                            int result = xTime.Value?.CompareTo(yTime.Value) ?? 0;
                            if (result != 0)
                            {
                                return sr.Ascending ? result : -result;
                            }
                        }

                        break;

                    //case FhirString xString:
                    //    if (yFvp.FhirValue is FhirString yString)
                    //    {
                    //        int result = xString.Value?.CompareTo(yString.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Code yC)
                    //    {
                    //        int result = xString.Value?.CompareTo(yC.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirUrl yUl)
                    //    {
                    //        int result = xString.Value?.CompareTo(yUl.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirUri yUi)
                    //    {
                    //        int result = xString.Value?.CompareTo(yUi.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Id yI)
                    //    {
                    //        int result = xString.Value?.CompareTo(yI.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Canonical yCan)
                    //    {
                    //        int result = xString.Value?.CompareTo(yCan.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Oid yO)
                    //    {
                    //        int result = xString.Value?.CompareTo(yO.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Canonical yU)
                    //    {
                    //        int result = xString.Value?.CompareTo(yU.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }

                    //    break;

                    //case Code xCode:
                    //    if (yFvp.FhirValue is Code yCode)
                    //    {
                    //        int result = xCode.Value?.CompareTo(yCode.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirString yS)
                    //    {
                    //        int result = xCode.Value?.CompareTo(yS.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirUrl yUl)
                    //    {
                    //        int result = xCode.Value?.CompareTo(yUl.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirUri yUi)
                    //    {
                    //        int result = xCode.Value?.CompareTo(yUi.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Id yI)
                    //    {
                    //        int result = xCode.Value?.CompareTo(yI.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Canonical yCan)
                    //    {
                    //        int result = xCode.Value?.CompareTo(yCan.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Oid yO)
                    //    {
                    //        int result = xCode.Value?.CompareTo(yO.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Canonical yU)
                    //    {
                    //        int result = xCode.Value?.CompareTo(yU.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }

                    //    break;


                    //case Id xId:
                    //    if (yFvp.FhirValue is Id yId)
                    //    {
                    //        int result = xId.Value?.CompareTo(yId.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirString yS)
                    //    {
                    //        int result = xId.Value?.CompareTo(yS.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Code yC)
                    //    {
                    //        int result = xId.Value?.CompareTo(yC.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirUrl yUl)
                    //    {
                    //        int result = xId.Value?.CompareTo(yUl.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirUri yUi)
                    //    {
                    //        int result = xId.Value?.CompareTo(yUi.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Canonical yCan)
                    //    {
                    //        int result = xId.Value?.CompareTo(yCan.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Oid yO)
                    //    {
                    //        int result = xId.Value?.CompareTo(yO.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Canonical yU)
                    //    {
                    //        int result = xId.Value?.CompareTo(yU.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }

                    //    break;


                    //case FhirUrl xUrl:
                    //    if (yFvp.FhirValue is FhirUrl yUrl)
                    //    {
                    //        int result = xUrl.Value?.CompareTo(yUrl.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirString yS)
                    //    {
                    //        int result = xUrl.Value?.CompareTo(yS.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Code yC)
                    //    {
                    //        int result = xUrl.Value?.CompareTo(yC.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirUri yUi)
                    //    {
                    //        int result = xUrl.Value?.CompareTo(yUi.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Id yI)
                    //    {
                    //        int result = xUrl.Value?.CompareTo(yI.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Canonical yCan)
                    //    {
                    //        int result = xUrl.Value?.CompareTo(yCan.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Oid yO)
                    //    {
                    //        int result = xUrl.Value?.CompareTo(yO.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Canonical yU)
                    //    {
                    //        int result = xUrl.Value?.CompareTo(yU.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }

                    //    break;

                    //case FhirUri xUri:
                    //    if (yFvp.FhirValue is FhirUri yUri)
                    //    {
                    //        int result = xUri.Value?.CompareTo(yUri.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirString yS)
                    //    {
                    //        int result = xUri.Value?.CompareTo(yS.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Code yC)
                    //    {
                    //        int result = xUri.Value?.CompareTo(yC.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirUrl yUl)
                    //    {
                    //        int result = xUri.Value?.CompareTo(yUl.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Id yI)
                    //    {
                    //        int result = xUri.Value?.CompareTo(yI.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Canonical yCan)
                    //    {
                    //        int result = xUri.Value?.CompareTo(yCan.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Oid yO)
                    //    {
                    //        int result = xUri.Value?.CompareTo(yO.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Canonical yU)
                    //    {
                    //        int result = xUri.Value?.CompareTo(yU.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }

                    //    break;

                    //case Canonical xCanonical:
                    //    if (yFvp.FhirValue is Canonical yCanonical)
                    //    {
                    //        int result = xCanonical.Value?.CompareTo(yCanonical.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirUri yUi)
                    //    {
                    //        int result = xCanonical.Value?.CompareTo(yUi.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirString yS)
                    //    {
                    //        int result = xCanonical.Value?.CompareTo(yS.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Code yC)
                    //    {
                    //        int result = xCanonical.Value?.CompareTo(yC.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirUrl yUl)
                    //    {
                    //        int result = xCanonical.Value?.CompareTo(yUl.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Id yI)
                    //    {
                    //        int result = xCanonical.Value?.CompareTo(yI.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Oid yO)
                    //    {
                    //        int result = xCanonical.Value?.CompareTo(yO.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }

                    //    break;

                    //case Oid xOid:
                    //    if (yFvp.FhirValue is Oid yOid)
                    //    {
                    //        int result = xOid.Value?.CompareTo(yOid.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Canonical yCan)
                    //    {
                    //        int result = xOid.Value?.CompareTo(yCan.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirUri yUi)
                    //    {
                    //        int result = xOid.Value?.CompareTo(yUi.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirString yS)
                    //    {
                    //        int result = xOid.Value?.CompareTo(yS.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Code yC)
                    //    {
                    //        int result = xOid.Value?.CompareTo(yC.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is FhirUrl yUl)
                    //    {
                    //        int result = xOid.Value?.CompareTo(yUl.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }
                    //    else if (yFvp.FhirValue is Id yI)
                    //    {
                    //        int result = xOid.Value?.CompareTo(yI.Value) ?? 0;
                    //        if (result != 0)
                    //        {
                    //            return sr.Ascending ? result : -result;
                    //        }
                    //    }

                    //    break;

                    case Quantity xQuantity:
                        if (yFvp.FhirValue is Quantity yQuantity)
                        {
                            // ignore units
                            int result = xQuantity.Value?.CompareTo(yQuantity.Value) ?? 0;
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
                        {
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

                            if ((xValue.GetValue() is IComparable xVComp) &&
                                (yValue.GetValue() is IComparable yVComp))
                            {
                                int result = xVComp.CompareTo(yVComp);
                                if (result != 0)
                                {
                                    return sr.Ascending ? result : -result;
                                }

                                continue;
                            }

                            string? xs = xFvp.FhirValue.ToString();
                            string? ys = yFvp.FhirValue.ToString();

                            if ((xs is not null) &&
                                (!xs.Contains(xFvp.GetType().Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                if ((ys is not null) &&
                                    (!ys.Contains(yFvp.GetType().Name, StringComparison.OrdinalIgnoreCase)))
                                {
                                    int result = xs.CompareTo(ys);
                                    if (result != 0)
                                    {
                                        return sr.Ascending ? result : -result;
                                    }
                                }
                            }
                        }

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

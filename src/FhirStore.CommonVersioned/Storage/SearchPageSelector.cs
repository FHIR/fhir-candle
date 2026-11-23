// <copyright file="SearchPageSelector.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using FhirCandle.Search;
using Hl7.Fhir.Model;

namespace FhirCandle.Storage;

internal sealed class SearchPageSelector
{
    internal readonly record struct Identity(string Type, string Id);
    private sealed record Row(Identity Identity, int Ordinal, FhirSortComparer.Key?[] Keys);

    internal List<Identity> Matches { get; } = [];
    internal int? Total { get; }
    internal int? NextOffset { get; }

    internal SearchPageSelector(IEnumerable<Resource> candidates, ParsedResultParameters parameters, VersionedFhirStore store)
    {
        int offset = parameters.Offset ?? 0;
        bool countOnly = parameters.SummaryFlag == "count" || parameters.PageMatchCount == 0;
        bool needTotal = countOnly || parameters.TotalMode != "none";
        long end = Math.Min(parameters.MaxResults ?? long.MaxValue, (long)offset + (parameters.PageMatchCount ?? int.MaxValue));
        FhirSortComparer? comparer = !countOnly && offset < end && parameters.SortRequests.Length != 0
            ? new(store, parameters.SortRequests) : null;
        List<Row> ordered = [];
        HashSet<Identity> seen = [];
        foreach (Resource resource in candidates)
        {
            if (string.IsNullOrEmpty(resource.Id))
            {
                continue;
            }
            Identity id = new(resource.TypeName, resource.Id);
            if (!seen.Add(id) || countOnly)
            {
                continue;
            }
            int ordinal = seen.Count - 1;
            if (comparer is not null)
            {
                ordered.Add(new(id, ordinal, comparer.Project(resource)));
            }
            else if (ordinal >= offset && ordinal < end)
            {
                Matches.Add(id);
            }
            else if (!needTotal && ordinal >= end)
            {
                break;
            }
        }
        if (comparer is not null)
        {
            ordered.Sort((x, y) =>
            {
                int result = comparer.CompareKeys(x.Keys, y.Keys);
                return result == 0 ? x.Ordinal.CompareTo(y.Ordinal) : result;
            });
            Matches.AddRange(ordered.Skip(offset).Take((int)Math.Min(int.MaxValue, end - offset)).Select(r => r.Identity));
        }
        Total = needTotal ? seen.Count : null;
        if (Matches.Count != 0 && end < seen.Count && end < parameters.MaxResults.GetValueOrDefault(long.MaxValue))
        {
            NextOffset = (int)end;
        }
    }
}

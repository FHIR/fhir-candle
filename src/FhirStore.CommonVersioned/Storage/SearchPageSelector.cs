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
    internal readonly record struct Identity(string Type, string StoreKey);
    internal readonly record struct Candidate(Identity Identity, Resource Body);
    private sealed record Row(Identity Identity, int Ordinal, FhirSortComparer.Key?[] Keys);

    internal List<Identity> Matches { get; } = [];
    internal int? Total { get; }
    internal int? NextOffset { get; }

    // The visible candidate stream contains each (type, store key) at most once.
    internal SearchPageSelector(IEnumerable<Candidate> candidates, ParsedResultParameters parameters, VersionedFhirStore store,
        ParsedResultParameters.SortRequest[] sort, IReadOnlyDictionary<string, ParsedResultParameters> byType, Action<int>? observePeak)
    {
        int offset = parameters.Offset ?? 0;
        bool countOnly = parameters.SummaryFlag == "count" || parameters.PageMatchCount == 0;
        bool needTotal = countOnly || parameters.TotalMode != "none";
        long end = Math.Min(parameters.MaxResults ?? long.MaxValue, (long)offset + (parameters.PageMatchCount ?? int.MaxValue));
        FhirSortComparer? comparer = !countOnly && offset < end && sort.Length != 0 ? new(store, sort) : null;
        Dictionary<string, (FhirSortComparer comparer, int[] positions)> projections = [];
        if (comparer is not null)
        {
            // The same search code may have a different expression (or none) on each type.
            foreach ((string type, ParsedResultParameters result) in byType)
            {
                projections[type] = (new(store, result.SortRequests), result.SortRequests
                    .Select(sr => Array.FindIndex(sort, s => s.RequestLiteral == sr.RequestLiteral)).ToArray());
            }
        }
        List<Row> ordered = [];
        int visible = 0;
        int peak = 0;
        foreach ((Identity id, Resource resource) in candidates)
        {
            int ordinal = visible++;
            if (countOnly)
            {
                continue;
            }
            if (comparer is not null)
            {
                (FhirSortComparer projection, int[] positions) = projections[id.Type];
                FhirSortComparer.Key?[] keys = new FhirSortComparer.Key?[sort.Length];
                FhirSortComparer.Key?[] projected = projection.Project(resource);
                for (int i = 0; i < positions.Length; i++)
                {
                    keys[positions[i]] = projected[i];
                }
                ordered.Add(new(id, ordinal, keys));
            }
            else if (ordinal >= offset && ordinal < end)
            {
                Matches.Add(id);
            }
            else if (!needTotal && ordinal >= end)
            {
                break;
            }
            if (observePeak is not null)
            {
                peak = Math.Max(peak, Matches.Count + ordered.Count);
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
        observePeak?.Invoke(Math.Max(peak, Matches.Count + ordered.Count));
        Total = needTotal ? visible : null;
        if (Matches.Count != 0 && end < visible && end < parameters.MaxResults.GetValueOrDefault(long.MaxValue))
        {
            NextOffset = (int)end;
        }
    }
}

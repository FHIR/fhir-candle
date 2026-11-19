// <copyright file="VersionedFhirStore.BundleAdmission.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Support;

namespace FhirCandle.Storage;

public partial class VersionedFhirStore
{
    /// <summary>An entry occurrence, independent of its resource identity or full URL.</summary>
    private readonly record struct TransactionEntryOccurrence(int InputIndex, Bundle.EntryComponent Entry);

    /// <summary>A POST identity reserved without binding or changing the resource's current ID.</summary>
    private readonly record struct TransactionIdentityReservation(int InputIndex, string Id);

    /// <summary>Creates a freshly parsed child context, retaining the executor's initialization order and defaults.</summary>
    private static FhirRequestContext CreateBundleEntryContext(
        FhirRequestContext ctx,
        Bundle.EntryComponent entry,
        string? effectiveUrl)
    {
        Bundle.RequestComponent request = entry.Request!;
        return new()
        {
            TenantName = ctx.TenantName,
            Store = ctx.Store,
            Authorization = ctx.Authorization,
            RequestHeaders = ctx.RequestHeaders,
            Forwarded = ctx.Forwarded,
            HttpMethod = request.Method?.ToString() ?? string.Empty,
            Url = effectiveUrl ?? string.Empty,
            IfMatch = request.IfMatch ?? string.Empty,
            IfModifiedSince = request.IfModifiedSince?.ToFhirDateTime() ?? string.Empty,
            IfNoneMatch = request.IfNoneMatch ?? string.Empty,
            IfNoneExist = request.IfNoneExist ?? string.Empty,

            SourceObject = entry.Resource,
        };
    }

    /// <summary>Yields supported method groups and then missing requests, retaining input occurrence indices.</summary>
    private static IEnumerable<TransactionEntryOccurrence> EnumerateTransactionEntryOccurrences(Bundle transaction)
    {
        for (int group = 0; group < 5; group++)
        {
            for (int index = 0; index < transaction.Entry.Count; index++)
            {
                Bundle.EntryComponent entry = transaction.Entry[index];
                bool include = group switch
                {
                    0 => entry.Request?.Method == Bundle.HTTPVerb.DELETE,
                    1 => entry.Request?.Method == Bundle.HTTPVerb.POST,
                    2 => (entry.Request?.Method == Bundle.HTTPVerb.PUT) || (entry.Request?.Method == Bundle.HTTPVerb.PATCH),
                    3 => (entry.Request?.Method == Bundle.HTTPVerb.GET) || (entry.Request?.Method == Bundle.HTTPVerb.HEAD),
                    _ => entry.Request is null,
                };

                if (include)
                {
                    yield return new(index, entry);
                }
            }
        }
    }

    /// <summary>Reserves one immutable GUID per eligible POST occurrence, in input order.</summary>
    private static IReadOnlyList<TransactionIdentityReservation> ReserveTransactionIdentities(Bundle transaction)
    {
        List<TransactionIdentityReservation> reservations = [];

        if (transaction.Type != Bundle.BundleType.Transaction)
        {
            return reservations;
        }

        for (int index = 0; index < transaction.Entry.Count; index++)
        {
            Bundle.EntryComponent entry = transaction.Entry[index];
            if ((entry.Request?.Method == Bundle.HTTPVerb.POST) && (entry.Resource is not null))
            {
                reservations.Add(new(index, Guid.NewGuid().ToString()));
            }
        }

        return reservations;
    }

    /// <summary>Projects the executor's literal URL replacement, including query and first-match semantics.</summary>
    private static string? ProjectTransactionRequestUrl(
        string? requestUrl,
        ILookup<string, TransactionResourceIdLookupRec> originalIdLookup,
        out bool hasOriginalIdMatch)
    {
        string idSegment = requestUrl?.Split('/')[^1].Split('?')[0] ?? string.Empty;
        hasOriginalIdMatch = originalIdLookup.Contains(idSegment);
        return hasOriginalIdMatch
            ? requestUrl!.Replace(idSegment, originalIdLookup[idSegment].First().Id)
            : requestUrl;
    }
}

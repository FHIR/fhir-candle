// <copyright file="VersionedFhirStore.BundleAdmission.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using FhirCandle.Models;
using FhirCandle.Serialization;
using Hl7.Fhir.Model;
using Hl7.Fhir.Support;

namespace FhirCandle.Storage;

public partial class VersionedFhirStore
{
    /// <summary>An entry occurrence, independent of its resource identity or full URL.</summary>
    private readonly record struct TransactionEntryOccurrence(int InputIndex, Bundle.EntryComponent Entry);

    /// <summary>A POST identity reserved without binding or changing the resource's current ID.</summary>
    private readonly record struct TransactionIdentityReservation(int InputIndex, string Id);

    /// <summary>An immutable ordinal address; repeated objects and URLs remain separate occurrences.</summary>
    private readonly record struct BundleOccurrencePath(string Value)
    {
        public BundleOccurrencePath Child(int index) => new($"{Value}.entry[{index}]");
    }

    private enum BundleEntryAdmission
    {
        Unparsed,
        Authorized,
        Denied,
    }

    /// <summary>Authorization facts only, never an executable command or a resource snapshot.</summary>
    private sealed record BundleEntryDecision(
        BundleOccurrencePath Path,
        BundleEntryAdmission Admission,
        string? SubmittedUrl,
        string? DenialDiagnostic,
        BundleAdmissionScope? ChildScope);

    /// <summary>Private approval for one invocation's structural occurrences.</summary>
    private sealed class BundleAdmissionScope(BundleOccurrencePath path)
    {
        public BundleOccurrencePath Path { get; } = path;

        public Dictionary<BundleOccurrencePath, BundleEntryDecision> Entries { get; } = [];

        public BundleEntryDecision Entry(int index) => Entries[Path.Child(index)];

        public IEnumerable<BundleEntryDecision> Denials => Entries.Values
            .SelectMany(e => (e.Admission == BundleEntryAdmission.Denied ? new[] { e } : [])
                .Concat(e.ChildScope?.Denials ?? []));
    }

    /// <summary>Authorizes submitted occurrences without preparing resources or predicting execution outcomes.</summary>
    private static BundleAdmissionScope AdmitBundle(
        FhirRequestContext ctx,
        Bundle bundle,
        BundleOccurrencePath path)
    {
        BundleAdmissionScope scope = new(path);

        IEnumerable<TransactionEntryOccurrence> occurrences = bundle.Type == Bundle.BundleType.Transaction
            ? EnumerateTransactionEntryOccurrences(bundle)
            : bundle.Entry.Select((entry, index) => new TransactionEntryOccurrence(index, entry));
        foreach (TransactionEntryOccurrence occurrence in occurrences)
        {
            Bundle.EntryComponent entry = occurrence.Entry;
            BundleOccurrencePath entryPath = path.Child(occurrence.InputIndex);
            BundleEntryAdmission admission = BundleEntryAdmission.Unparsed;
            string? diagnostic = null;
            BundleAdmissionScope? childScope = null;
            if (entry.Request is not null)
            {
                FhirRequestContext child = CreateBundleEntryContext(ctx, entry, entry.Request.Url);
                if (child.Interaction is not null)
                {
                    admission = child.IsAuthorized() ? BundleEntryAdmission.Authorized : BundleEntryAdmission.Denied;
                    if (admission == BundleEntryAdmission.Denied)
                    {
                        diagnostic = UnauthorizedBundleEntryDiagnostic(child);
                    }

                    if (IsExecutableBundle(child, out Bundle? nested))
                    {
                        childScope = AdmitBundle(child, nested, entryPath);
                    }
                }
            }

            scope.Entries.Add(entryPath, new(entryPath, admission, entry.Request?.Url, diagnostic, childScope));
        }

        return scope;
    }

    private static bool IsExecutableBundle(FhirRequestContext ctx, out Bundle bundle)
    {
        if ((ctx.Interaction == Common.StoreInteractionCodes.SystemBundle) &&
            (ctx.SourceObject is Bundle nested) &&
            ((nested.Type == Bundle.BundleType.Batch) || (nested.Type == Bundle.BundleType.Transaction)))
        {
            bundle = nested;
            return true;
        }

        bundle = null!;
        return false;
    }

    private static string UnauthorizedBundleEntryDiagnostic(FhirRequestContext ctx) =>
        $"Unauthorized request: {ctx.HttpMethod} {ctx.Url}, parsed interaction: {ctx.Interaction}";

    private static Bundle.EntryComponent RefusedBundleEntry(Bundle.EntryComponent entry, string diagnostic) => new()
    {
        FullUrl = entry.FullUrl,
        Response = new()
        {
            Status = getResponseStatus(HttpStatusCode.Unauthorized),
            Outcome = SerializationUtils.BuildOutcomeForRequest(
                HttpStatusCode.Unauthorized, diagnostic, OperationOutcome.IssueType.Forbidden),
        },
    };

    /// <summary>Preserves the executor's supported positions and malformed errors without executing any entry.</summary>
    private static void RefuseTransaction(Bundle transaction, Bundle response, BundleAdmissionScope scope)
    {
        foreach (TransactionEntryOccurrence occurrence in EnumerateTransactionEntryOccurrences(transaction))
        {
            Bundle.EntryComponent entry = occurrence.Entry;
            BundleEntryDecision decision = scope.Entry(occurrence.InputIndex);
            if ((entry.Request is null) || (decision.Admission == BundleEntryAdmission.Unparsed))
            {
                bool missing = entry.Request is null;
                response.Entry.Add(new()
                {
                    FullUrl = entry.FullUrl,
                    Response = new()
                    {
                        Status = getResponseStatus(missing ? HttpStatusCode.BadRequest : HttpStatusCode.InternalServerError),
                        Outcome = SerializationUtils.BuildOutcomeForRequest(
                            missing ? HttpStatusCode.BadRequest : HttpStatusCode.NotImplemented,
                            missing ? "Entry is missing a request" :
                                $"Request could not be parsed to known interaction: {entry.Request!.Method} {decision.SubmittedUrl}",
                            missing ? OperationOutcome.IssueType.Required : OperationOutcome.IssueType.NotSupported),
                    },
                });
                continue;
            }

            Bundle.EntryComponent refused = RefusedBundleEntry(entry, decision.DenialDiagnostic ??
                "Entry not executed because transaction authorization failed");
            if (refused.Response?.Outcome is OperationOutcome outcome)
            {
                foreach (BundleEntryDecision denial in decision.ChildScope?.Denials ?? [])
                {
                    outcome.Issue.Add(new()
                    {
                        Severity = OperationOutcome.IssueSeverity.Error,
                        Code = OperationOutcome.IssueType.Forbidden,
                        Diagnostics = $"Denied descendant {denial.Path.Value}: {denial.DenialDiagnostic}",
                    });
                }
            }

            response.Entry.Add(refused);
        }
    }

    /// <summary>Ordinary batches admit singleton units at their original live-state positions.</summary>
    private void ProcessAdmittedBatch(FhirRequestContext ctx, Bundle batch, Bundle response)
    {
        foreach (Bundle.EntryComponent entry in batch.Entry)
        {
            if (entry.Request is not null)
            {
                FhirRequestContext child = CreateBundleEntryContext(ctx, entry, entry.Request.Url);
                if ((child.Interaction is not null) && !child.IsAuthorized())
                {
                    response.Entry.Add(RefusedBundleEntry(entry, UnauthorizedBundleEntryDiagnostic(child)));
                    continue;
                }
            }

            // This is a view, not a copy: preparation/execution still see the original objects.
            ProcessBatch(ctx, new Bundle() { Type = Bundle.BundleType.Batch, Entry = [entry] }, response);
        }
    }

    /// <summary>Continues an approved nested scope without admitting again after parent writes.</summary>
    private bool DispatchBundleEntry(
        FhirRequestContext ctx,
        BundleAdmissionScope? approvedScope,
        out FhirResponseContext response,
        bool serializeReturn,
        bool forceAllowExistingId = false)
    {
        if ((approvedScope is null) || !IsExecutableBundle(ctx, out Bundle? nested))
        {
            return PerformInteraction(ctx, out response, serializeReturn, forceAllowExistingId);
        }

        bool success = DoProcessBundle(ctx, nested, out response, approvedScope);
        if (serializeReturn)
        {
            response = response with
            {
                MimeType = ctx.DestinationFormat,
                SerializedResource = response.Resource is Resource resource
                    ? SerializationUtils.SerializeFhir(resource, ctx.DestinationFormat, ctx.SerializePretty) : string.Empty,
                SerializedOutcome = response.Outcome is Resource outcome
                    ? SerializationUtils.SerializeFhir(outcome, ctx.DestinationFormat, ctx.SerializePretty) : string.Empty,
            };
        }

        return success;
    }

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

    /// <summary>Remaps only a parsed ordinary instance ID, preserving all other submitted URL text.</summary>
    private static string? RemapTransactionInstanceRequestUrl(
        FhirRequestContext ctx,
        Bundle.EntryComponent entry,
        ILookup<string, TransactionResourceIdLookupRec> originalIdLookup)
    {
        string? requestUrl = entry.Request?.Url;
        if (requestUrl is null)
        {
            return requestUrl;
        }

        FhirRequestContext child = CreateBundleEntryContext(ctx, entry, requestUrl);
        if (string.IsNullOrEmpty(child.Id) ||
            (child.UrlPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length != 2) ||
            (child.Interaction is not (Common.StoreInteractionCodes.InstanceRead or
                Common.StoreInteractionCodes.InstanceDelete or
                Common.StoreInteractionCodes.InstanceUpdate or
                Common.StoreInteractionCodes.InstanceUpdateConditional or
                Common.StoreInteractionCodes.InstancePatch or
                Common.StoreInteractionCodes.InstancePatchConditional)))
        {
            return requestUrl;
        }

        int queryStart = requestUrl.IndexOf('?');
        int pathEnd = queryStart < 0 ? requestUrl.Length : queryStart;
        if ((pathEnd == 0) || (requestUrl[pathEnd - 1] == '/'))
        {
            return requestUrl;
        }

        int idStart = requestUrl.LastIndexOf('/', pathEnd - 1) + 1;
        if (!requestUrl[idStart..pathEnd].Equals(child.Id, StringComparison.Ordinal) ||
            !originalIdLookup.Contains(child.Id))
        {
            return requestUrl;
        }

        return requestUrl[..idStart] + originalIdLookup[child.Id].First().Id + requestUrl[pathEnd..];
    }
}

// <copyright file="R4TestsSearchStrict.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Microsoft.Extensions.Primitives;
using Shouldly;
using System.Net;

namespace fhir.candle.Tests;

/// <summary>
/// Phase 3 — strict search handling tests. Validates that <c>Prefer: handling=strict</c>
/// (either implicit via tenant <c>--strict</c> or explicit via header) causes the
/// server to reject unknown / malformed search parameters with a 400 + multi-issue
/// <c>OperationOutcome</c> carrying the canonical FHIR spec URL, while lenient mode
/// silently drops them as it does today. Parameterized across R4, R4B, and R5.
/// </summary>
public class StrictSearchHandlingTests
{
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    private readonly Dictionary<FhirReleases.FhirSequenceCodes, IFhirStore> _strictStores = new();
    private readonly Dictionary<FhirReleases.FhirSequenceCodes, IFhirStore> _lenientStores = new();

    public StrictSearchHandlingTests()
    {
        foreach (object[] row in Configurations)
        {
            FhirReleases.FhirSequenceCodes version = (FhirReleases.FhirSequenceCodes)row[0];
            _strictStores[version] = MakeStore(version, $"{version.ToString().ToLowerInvariant()}-strict-sx", strict: true);
            _lenientStores[version] = MakeStore(version, $"{version.ToString().ToLowerInvariant()}-lenient-sx", strict: false);
        }
    }

    private static IFhirStore MakeStore(FhirReleases.FhirSequenceCodes version, string tenant, bool strict)
    {
        TenantConfiguration cfg = new()
        {
            FhirVersion = version,
            ControllerName = tenant,
            BaseUrl = $"http://localhost/fhir/{tenant}",
            Strict = strict,
        };

        IFhirStore store = version switch
        {
            FhirReleases.FhirSequenceCodes.R4 => new candleR4::FhirCandle.Storage.VersionedFhirStore(),
            FhirReleases.FhirSequenceCodes.R4B => new candleR4B::FhirCandle.Storage.VersionedFhirStore(),
            FhirReleases.FhirSequenceCodes.R5 => new candleR5::FhirCandle.Storage.VersionedFhirStore(),
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        };
        store.Init(cfg);
        return store;
    }

    private static FhirRequestContext SearchCtx(
        IFhirStore store,
        string queryString,
        string resourceType = "Patient",
        string? preferHandling = null)
    {
        Dictionary<string, StringValues> headers = new();
        if (!string.IsNullOrEmpty(preferHandling))
        {
            headers["Prefer"] = new StringValues($"handling={preferHandling}");
        }

        return new FhirRequestContext
        {
            TenantName = store.Config.ControllerName,
            Store = store,
            HttpMethod = "GET",
            Url = $"{store.Config.BaseUrl}/{resourceType}?{queryString}",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
            RequestHeaders = headers,
        };
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void SearchUnknownParameterStrictRejectsWith400(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = _strictStores[version];

        bool ok = store.TypeSearch(SearchCtx(store, "bogus=1"), out FhirResponseContext response);

        ok.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.SerializedOutcome.ShouldContain("bogus");
        response.SerializedOutcome.ShouldContain("search.html#errors");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void SearchMultipleUnknownParametersStrictEmitsAllIssues(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = _strictStores[version];

        bool ok = store.TypeSearch(SearchCtx(store, "bogus=1&bogus2=2"), out FhirResponseContext response);

        ok.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.SerializedOutcome.ShouldContain("bogus");
        response.SerializedOutcome.ShouldContain("bogus2");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void SearchMalformedDateStrictRejectsWith400(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = _strictStores[version];

        bool ok = store.TypeSearch(SearchCtx(store, "birthdate=not-a-date"), out FhirResponseContext response);

        ok.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.SerializedOutcome.ShouldContain("birthdate");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void SearchPartiallyMalformedMultiValueStrictRejectsWith400(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = _strictStores[version];

        bool ok = store.TypeSearch(
            SearchCtx(store, "birthdate=2020-01-01,not-a-date"),
            out FhirResponseContext response);

        ok.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.SerializedOutcome.ShouldContain("birthdate");
        response.SerializedOutcome.ShouldContain("not-a-date");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void SearchExplicitPreferLenientOverridesStrictTenant(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = _strictStores[version];

        bool ok = store.TypeSearch(
            SearchCtx(store, "bogus=1", preferHandling: "lenient"),
            out FhirResponseContext response);

        ok.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void SearchExplicitPreferStrictOverridesLenientTenant(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = _lenientStores[version];

        bool ok = store.TypeSearch(
            SearchCtx(store, "bogus=1", preferHandling: "strict"),
            out FhirResponseContext response);

        ok.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.SerializedOutcome.ShouldContain("bogus");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void SearchUnknownParameterLenientReturns200(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = _lenientStores[version];

        bool ok = store.TypeSearch(SearchCtx(store, "bogus=1"), out FhirResponseContext response);

        ok.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        // Bundle self-link should not contain the ignored param.
        if (!string.IsNullOrEmpty(response.SerializedResource))
        {
            response.SerializedResource.ShouldNotContain("\"url\":\"" + store.Config.BaseUrl + "/Patient?bogus=1\"");
        }
    }
}

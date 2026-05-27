extern alias candleR4;
extern alias coreR4;

using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using fhir.candle.Tests.Models;
using System.Text.Json;
using Xunit.Abstractions;
using candleR4::FhirCandle.Storage;
using fhir.candle.Tests.Extensions;
using Shouldly;
using System.Net;
using Hl7.FhirPath;
using fhir.candle.Services;
using static FhirCandle.Storage.Common;

namespace fhir.candle.Tests;

public class FromIssueTestsR4
{
    /// <summary>
    /// Tests to ensure transaction response bundles use the correct formatting
    /// of status codes.
    /// For issue: https://github.com/FHIR/fhir-candle/issues/26
    /// Fixed by: https://github.com/FHIR/fhir-candle/commit/e8e0df0ced298ce62f005830e5ec4c751aca419f
    /// </summary>
    [Fact]
    public void TransactionResponseStatusMissingCode()
    {
        TenantConfiguration config = new()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R4,
            ControllerName = "r4",
            BaseUrl = "http://localhost/fhir/r4",
            LoadDirectory = null,
            AllowExistingId = true,
            AllowCreateAsUpdate = true,
        };

        IFhirStore store = new VersionedFhirStore();
        store.Init(config);

        string json = """
            {
              "resourceType": "Bundle",
              "type": "transaction",
              "entry": [
                {
                  "request": {
                    "method": "POST",
                    "url": "Patient"
                  },
                  "resource": {
                    "resourceType": "Patient",
                    "id": "example"
                  }
                }
              ]
            }
            """;

        FhirRequestContext ctx;
        FhirResponseContext response;
        bool success;

        ctx = new()
        {
            TenantName = store.Config.ControllerName,
            Store = store,
            HttpMethod = "POST",
            Url = store.Config.BaseUrl,
            Forwarded = null,
            Authorization = null,
            SourceContent = json,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        success = store.ProcessBundle(ctx, out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();

        MinimalBundle? bundle = JsonSerializer.Deserialize<MinimalBundle>(response.SerializedResource);
        bundle.ShouldNotBeNull();
        bundle.Entries.ShouldNotBeNullOrEmpty();
        bundle.Entries.Count().ShouldBe(1);

        MinimalBundle.MinimalEntry entry = bundle.Entries.First();

        entry.Response.ShouldNotBeNull();
        entry.Response.Status.ShouldBe("201 Created");
    }

    /// <summary>
    /// Tests that repeated occurrences of the same search parameter in the query
    /// string are combined with AND semantics (per FHIR R4 §3.1.1.5.7) rather than
    /// being collapsed into a comma-separated OR list.
    ///
    /// Reproduction: create two Organizations and search with both
    /// `?name:contains=Foo&amp;name:contains=ZZZNoMatchExpected`. No Organization
    /// has a name containing both substrings, so the result should be empty.
    /// Currently candle returns the Foo-matching Organization, indicating the
    /// repeated parameter is being treated as OR.
    /// </summary>
    [Fact]
    public void RepeatedSearchParameterShouldUseAndSemantics()
    {
        TenantConfiguration config = new()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R4,
            ControllerName = "r4",
            BaseUrl = "http://localhost/fhir/r4",
            LoadDirectory = null,
            AllowExistingId = true,
            AllowCreateAsUpdate = true,
        };

        IFhirStore store = new VersionedFhirStore();
        store.Init(config);

        // Seed two Organizations with disjoint name tokens.
        foreach (string orgJson in new[]
        {
            """{"resourceType":"Organization","id":"org-foo-bar","name":"Foo Bar Hospital"}""",
            """{"resourceType":"Organization","id":"org-baz","name":"Baz Hospital"}""",
        })
        {
            FhirRequestContext seedCtx = new()
            {
                TenantName = store.Config.ControllerName,
                Store = store,
                HttpMethod = "PUT",
                Url = store.Config.BaseUrl + "/Organization",
                Forwarded = null,
                Authorization = null,
                SourceContent = orgJson,
                SourceFormat = "application/fhir+json",
                DestinationFormat = "application/fhir+json",
                ResourceType = "Organization",
            };
            store.InstanceUpdate(seedCtx, out _).ShouldBeTrue();
        }

        // Query with two repeated `name:contains` constraints. No organization
        // satisfies both, so the searchset bundle should have total = 0.
        FhirRequestContext searchCtx = new()
        {
            TenantName = store.Config.ControllerName,
            Store = store,
            HttpMethod = "GET",
            Url = store.Config.BaseUrl + "/Organization",
            UrlQuery = "name:contains=Foo&name:contains=ZZZNoMatchExpected",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
            ResourceType = "Organization",
        };

        store.TypeSearch(searchCtx, out FhirResponseContext searchResponse).ShouldBeTrue();
        searchResponse.SerializedResource.ShouldNotBeNullOrEmpty();

        MinimalBundle? bundle = JsonSerializer.Deserialize<MinimalBundle>(searchResponse.SerializedResource);
        bundle.ShouldNotBeNull();
        bundle.BundleType.ShouldBe("searchset");
        bundle.Total.ShouldBe(
            0,
            "repeated `name:contains` parameters must be combined with AND (FHIR R4 §3.1.1.5.7); no Organization contains both 'Foo' and 'ZZZNoMatchExpected'");
    }
}

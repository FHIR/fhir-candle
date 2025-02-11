// <copyright file="FhirStoreTestsR4.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;

using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using Shouldly;
using System.Net;
using System.Text.Json;
using Xunit.Abstractions;
using candleR4::FhirCandle.Storage;

namespace fhir.candle.Tests;

/// <summary>Unit tests core FhirStore R4 functionality.</summary>
public class FhirStoreTestsR4: IDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The configuration.</summary>
    private static readonly TenantConfiguration _config = new()
    {
        FhirVersion = FhirReleases.FhirSequenceCodes.R4,
        ControllerName = "r4",
        BaseUrl = "http://localhost/fhir/r4",
        AllowExistingId = true,
        AllowCreateAsUpdate = true,
    };

    private const int _expectedRestResources = 146;

    private const int _hospitalOrganizationCount = 272;
    private const int _hospitalLocationCount = 273;
    private const int _practitionerCount = 272;
    private const int _practitionerRoleCount = 272;
    private const int _patientBundleCount = 343;
    private const int _patientCount = 1;
    private const int _patientConditionCount = 12;
    private const int _patientClaimCount = 25;
    private const int _patientDiagnosticReportCount = 21;
    private const int _patientDocumentReferenceCount = 20;
    private const int _patientEncounterCount = 20;
    private const int _patientExplanationOfBenefitCount = 25;
    private const int _patientImmunizationCount = 32;
    private const int _patientObservationCount = 159;
    private const int _patientProcedureCount = 15;


    /// <summary>
    /// Initializes a new instance of the <see cref="FhirStoreTestsR4B"/> class.
    /// </summary>
    /// <param name="testOutputHelper">The test output helper.</param>
    public FhirStoreTestsR4(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged
    /// resources.
    /// </summary>
    public void Dispose()
    {
        // cleanup
    }

    [Theory]
    [Trait("RunningTime", "Long")]
    [FileData("data/r4-synthea/Bundle-Patient-01.json", "data/r4-synthea/Bundle-Hospital-01.json", "data/r4-synthea/Bundle-Practitioner-01.json")]
    public void SyntheaBundles(string patientJson, string hospitalJson, string practitionerJson)
    {
        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        FhirRequestContext ctx;
        FhirResponseContext response;
        bool success;

        // post the hospital bundle (batch of organizations and locations)
        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = fhirStore.Config.BaseUrl,
            Forwarded = null,
            Authorization = null,
            SourceContent = hospitalJson,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        success = fhirStore.ProcessBundle(ctx, out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();

        MinimalBundle? hospitalResponseBundle = JsonSerializer.Deserialize<MinimalBundle>(response.SerializedResource);
        hospitalResponseBundle.ShouldNotBeNull();
        hospitalResponseBundle.Entries.ShouldNotBeNullOrEmpty();
        hospitalResponseBundle.Entries.Count().ShouldBe(_hospitalOrganizationCount + _hospitalLocationCount);

        hospitalResponseBundle.Entries.All(validateEntries).ShouldBeTrue();

        // post the practitioner bundle (batch)
        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = fhirStore.Config.BaseUrl,
            Forwarded = null,
            Authorization = null,
            SourceContent = practitionerJson,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        success = fhirStore.ProcessBundle(ctx, out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();

        MinimalBundle? practitionerResponseBundle = JsonSerializer.Deserialize<MinimalBundle>(response.SerializedResource);
        practitionerResponseBundle.ShouldNotBeNull();
        practitionerResponseBundle.Entries.ShouldNotBeNullOrEmpty();
        practitionerResponseBundle.Entries.Count().ShouldBe(_practitionerCount + _practitionerRoleCount);

        practitionerResponseBundle.Entries.All(validateEntries).ShouldBeTrue();

        // post the practitioner bundle (transaction)
        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = fhirStore.Config.BaseUrl,
            Forwarded = null,
            Authorization = null,
            SourceContent = patientJson,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        success = fhirStore.ProcessBundle(ctx, out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();

        MinimalBundle? patientResponseBundle = JsonSerializer.Deserialize<MinimalBundle>(response.SerializedResource);
        patientResponseBundle.ShouldNotBeNull();
        patientResponseBundle.Entries.ShouldNotBeNullOrEmpty();
        patientResponseBundle.Entries.Count().ShouldBe(_patientBundleCount);

        patientResponseBundle.Entries.All(validateEntries).ShouldBeTrue();

        return;

        bool validateEntries(MinimalBundle.MinimalEntry entry)
        {
            entry.FullUrl.ShouldNotBeNullOrEmpty();

            entry.Resource.ShouldNotBeNull();
            entry.Resource.ResourceType.ShouldNotBeNullOrEmpty();
            entry.Resource.Id.ShouldNotBeNullOrEmpty();

            entry.Response.ShouldNotBeNull();
            entry.Response.Status.ShouldBeOneOf(["200 OK", "201 Created"]);
            entry.Response.Location.ShouldEndWith(entry.Resource.Id);

            entry.Response.Outcome.ShouldNotBeNull();
            entry.Response.Outcome.Issues.ShouldHaveCount(1);
            entry.Response.Outcome.Issues.First().Severity.ShouldBe("information");
            entry.Response.Outcome.Issues.First().Code.ShouldBe("success");

            return true;
        }
    }

    [Theory]
    [FileData("data/r4/searchparameter-patient-multiplebirth.json")]
    public void ResourceCreateSearchParameter(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = fhirStore.Config.BaseUrl + "/SearchParameter",
            Forwarded = null,
            Authorization = null,
            SourceContent = json,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceCreate(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created, response.SerializedOutcome);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"1\"");
        response.Location.ShouldContain("SearchParameter/");

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = fhirStore.Config.BaseUrl + "/SearchParameter/Patient-multiplebirth",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        success = fhirStore.InstanceRead(
            ctx,
            out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, response.SerializedOutcome);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"1\"");
        response.Location.ShouldEndWith("SearchParameter/Patient-multiplebirth");
        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData("data/r4/searchparameter-patient-multiplebirth.json")]
    public void CreateSearchParameterCapabilityCount(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = fhirStore.Config.BaseUrl + "/metadata",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        // read the metadata
        bool success = fhirStore.GetMetadata(
            ctx,
            out FhirResponseContext response);

        success.ShouldBe(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();

        MinimalCapabilities? capabilities = JsonSerializer.Deserialize<MinimalCapabilities>(response.SerializedResource);

        capabilities.ShouldNotBeNull();
        capabilities!.Rest.ShouldNotBeNullOrEmpty();

        MinimalCapabilities.MinimalRest rest = capabilities!.Rest!.First();
        rest.Mode.ShouldBe("server");
        rest.Resources.ShouldNotBeNullOrEmpty();

        int spCount = 0;
        foreach (MinimalCapabilities.MinimalResource r in rest.Resources!)
        {
            if (r.ResourceType != "Patient")
            {
                continue;
            }

            spCount = r.SearchParams?.Count() ?? 0;
            break;
        }

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = fhirStore.Config.BaseUrl + "/SearchParameter",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        // add a search parameter for the patient resource
        success = fhirStore.InstanceCreate(
            ctx,
            out response);

        success.ShouldBe(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = fhirStore.Config.BaseUrl + "/metadata",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        // read the metadata again
        success = fhirStore.GetMetadata(
            ctx,
            out response);

        success.ShouldBe(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();

        capabilities = JsonSerializer.Deserialize<MinimalCapabilities>(response.SerializedResource);

        capabilities.ShouldNotBeNull();
        capabilities!.Rest.ShouldNotBeNullOrEmpty();

        rest = capabilities!.Rest!.First();
        rest.Mode.ShouldBe("server");
        rest.Resources.ShouldNotBeNullOrEmpty();

        foreach (MinimalCapabilities.MinimalResource r in rest.Resources!)
        {
            if (r.ResourceType != "Patient")
            {
                continue;
            }

            (r.SearchParams?.Count() ?? 0).ShouldBe(spCount + 1);
            break;
        }
    }
}

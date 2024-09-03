// <copyright file="FhirStoreTestsR4B.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4B;

using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FluentAssertions;
using System.Net;
using System.Text.Json;
using Xunit.Abstractions;
using candleR4B::FhirCandle.Models;
using candleR4B::FhirCandle.Storage;

namespace fhir.candle.Tests;

/// <summary>Unit tests core FhirStore R4 functionality.</summary>
public class FhirStoreTestsR4B: IDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The configuration.</summary>
    private static readonly TenantConfiguration _config = new()
    {
        FhirVersion = FhirReleases.FhirSequenceCodes.R4B,
        ControllerName = "r4b",
        BaseUrl = "http://localhost/fhir/r4b",
        AllowExistingId = true,
        AllowCreateAsUpdate = true,
    };

    private const int _expectedRestResources = 140;

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirStoreTestsR4B"/> class.
    /// </summary>
    /// <param name="testOutputHelper">The test output helper.</param>
    public FhirStoreTestsR4B(ITestOutputHelper testOutputHelper)
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
    [FileData("data/r4b/searchparameter-patient-multiplebirth.json")]
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
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceCreate(
            ctx,
            out FhirResponseContext response);

        success.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.Created, response.SerializedOutcome);
        response.SerializedResource.Should().NotBeNullOrEmpty();
        response.SerializedOutcome.Should().NotBeNullOrEmpty();
        response.ETag.Should().Be("W/\"1\"");
        response.Location.Should().Contain("SearchParameter/");

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

        success.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.OK, response.SerializedOutcome);
        response.SerializedResource.Should().NotBeNullOrEmpty();
        response.SerializedOutcome.Should().NotBeNullOrEmpty();
        response.ETag.Should().Be("W/\"1\"");
        response.Location.Should().EndWith("SearchParameter/Patient-multiplebirth");
        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData("data/r4b/searchparameter-patient-multiplebirth.json")]
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

        success.Should().Be(true);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.SerializedResource.Should().NotBeNullOrEmpty();
        response.SerializedOutcome.Should().NotBeNullOrEmpty();

        MinimalCapabilities? capabilities = JsonSerializer.Deserialize<MinimalCapabilities>(response.SerializedResource);

        capabilities.Should().NotBeNull();
        capabilities!.Rest.Should().NotBeNullOrEmpty();

        MinimalCapabilities.MinimalRest rest = capabilities!.Rest!.First();
        rest.Mode.Should().Be("server");
        rest.Resources.Should().NotBeNullOrEmpty();

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

        success.Should().Be(true);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

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

        success.Should().Be(true);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.SerializedResource.Should().NotBeNullOrEmpty();
        response.SerializedOutcome.Should().NotBeNullOrEmpty();

        capabilities = JsonSerializer.Deserialize<MinimalCapabilities>(response.SerializedResource);

        capabilities.Should().NotBeNull();
        capabilities!.Rest.Should().NotBeNullOrEmpty();

        rest = capabilities!.Rest!.First();
        rest.Mode.Should().Be("server");
        rest.Resources.Should().NotBeNullOrEmpty();

        foreach (MinimalCapabilities.MinimalResource r in rest.Resources!)
        {
            if (r.ResourceType != "Patient")
            {
                continue;
            }

            (r.SearchParams?.Count() ?? 0).Should().Be(spCount + 1);
            break;
        }
    }
}

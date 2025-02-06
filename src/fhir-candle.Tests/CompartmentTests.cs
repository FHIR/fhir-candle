extern alias candleR4;
extern alias coreR4;

using System.Net;
using candleR4::FhirCandle.Storage;
using Hl7.Fhir.Model;
using fhir.candle.Tests.Extensions;
using FhirCandle.Utils;
using Xunit.Abstractions;
using FhirRequestContext = FhirCandle.Models.FhirRequestContext;
using FhirResponseContext = FhirCandle.Models.FhirResponseContext;
using Resource = Hl7.Fhir.Model.Resource;
using TenantConfiguration = FhirCandle.Models.TenantConfiguration;
using Shouldly;
using static FhirCandle.Storage.Common;

namespace fhir.candle.Tests;


public class AuthCompartmentTests: IDisposable
{

    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirStoreTestsR4B"/> class.
    /// </summary>
    /// <param name="testOutputHelper">The test output helper.</param>
    public AuthCompartmentTests(ITestOutputHelper testOutputHelper)
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
    [FileData("data/r4/CompartmentDefinition-patient.json")]
    public void TestCompartmentTypeSearch(string json)
    {
        // load compartment
        var jsonParser = new coreR4::Hl7.Fhir.Serialization.FhirJsonParser();
        var compartmentDefinition = jsonParser.Parse(json) as coreR4::Hl7.Fhir.Model.CompartmentDefinition;
        compartmentDefinition.ShouldNotBeNull();

        string path = Path.GetRelativePath(Directory.GetCurrentDirectory(), "data/r4");
        DirectoryInfo? loadDirectory = null;

        // FHIR server
        if (Directory.Exists(path))
        {
            loadDirectory = new DirectoryInfo(path);
        }

        TenantConfiguration config = new()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R4,
            ControllerName = "r4",
            BaseUrl = "http://localhost/fhir/r4",
            LoadDirectory = loadDirectory,
            AllowExistingId = true,
            AllowCreateAsUpdate = true,
        };
        var versionedFhirStore = new VersionedFhirStore();
        versionedFhirStore.Init(config);

        // add compartment to store
        this.putResource(versionedFhirStore, compartmentDefinition!);

        // all observations
        var searchAllBundle = SearchResource(versionedFhirStore, "Observation");
        searchAllBundle.ShouldNotBeNull();
        searchAllBundle.Entry.ShouldNotBeNullOrEmpty();
        searchAllBundle.Entry.Count.ShouldBeGreaterThan(0);
        var searchAllBundleCount = searchAllBundle.Entry.Count;

        // all observations for patient example
        var searchBundle = SearchResource(versionedFhirStore, "Observation?subject=example");
        searchBundle.ShouldNotBeNull();
        searchBundle.Entry.ShouldNotBeNullOrEmpty();
        searchBundle.Entry.Count.ShouldBeGreaterThan(0);
        var searchBundleCount = searchBundle.Entry.Count;

        // all observations for patient example using compartment
        var compartmentBundle = SearchResource(versionedFhirStore, "Patient/example/Observation");
        compartmentBundle.ShouldNotBeNull();
        compartmentBundle.Entry.ShouldNotBeNullOrEmpty();
        compartmentBundle.Entry.Count.ShouldBeGreaterThan(0);
        var compartmentBundleCount = compartmentBundle.Entry.Count;

        // check if direct and compartment search returns equal numbers
        compartmentBundleCount.ShouldBe(searchBundleCount);
    }

    private Bundle SearchResource(VersionedFhirStore versionedFhirStore, String search )
    {
        FhirRequestContext ctx = new()
        {
            TenantName = versionedFhirStore.Config.ControllerName,
            Store = versionedFhirStore,
            HttpMethod = "GET",
            Url = versionedFhirStore.Config.BaseUrl + "/" + search,
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        FhirResponseContext response;

        bool success = ctx.Interaction switch
        {
            StoreInteractionCodes.CompartmentSearch => versionedFhirStore.CompartmentSearch(ctx, out response),
            StoreInteractionCodes.CompartmentTypeSearch => versionedFhirStore.CompartmentTypeSearch(ctx, out response),
            _ => versionedFhirStore.TypeSearch(ctx, out response)
        };

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();

        response.Resource.ShouldNotBeNull();
        var result = response.Resource;
        result.GetType().ToString().ShouldBe("Hl7.Fhir.Model.Bundle");

        var bundle = result as Bundle;
        return bundle!;
    }

    private void putResource(VersionedFhirStore versionedFhirStore, Resource resource)
    {
        FhirRequestContext ctx = new FhirRequestContext
        {
            TenantName = "r4",
            Store = versionedFhirStore,
            HttpMethod = "PUT",
            SourceObject = resource,
            Url = string.Empty,
            Authorization = null,
        };
        versionedFhirStore.InstanceUpdate(ctx, out FhirResponseContext opResponse);
    }


}

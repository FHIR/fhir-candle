extern alias candleR4;
extern alias coreR4;

using System.Net;
using candleR4::FhirCandle.Models;
using candleR4::FhirCandle.Storage;
using Hl7.Fhir.Model;
using fhir.candle.Tests.Extensions;
using FhirCandle.Utils;
using FluentAssertions;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Specification;
using Xunit.Abstractions;
using FhirRequestContext = FhirCandle.Models.FhirRequestContext;
using FhirResponseContext = FhirCandle.Models.FhirResponseContext;
using Resource = Hl7.Fhir.Model.Resource;
using TenantConfiguration = FhirCandle.Models.TenantConfiguration;

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
        compartmentDefinition.Should().NotBeNull();

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
        searchAllBundle.Should().NotBeNull();
        searchAllBundle.Entry.Should().NotBeNullOrEmpty();
        searchAllBundle.Entry.Count.Should().BeGreaterThan(0);
        var searchAllBundleCount = searchAllBundle.Entry.Count;

        // all observations for patient example
        var searchBundle = SearchResource(versionedFhirStore, "Observation?subject=example");
        searchBundle.Should().NotBeNull();
        searchBundle.Entry.Should().NotBeNullOrEmpty();
        searchBundle.Entry.Count.Should().BeGreaterThan(0);
        var searchBundleCount = searchBundle.Entry.Count;

        // all observations for patient example using compartment
        var compartmentBundle = SearchResource(versionedFhirStore, "Patient/example/Observation");
        compartmentBundle.Should().NotBeNull();
        compartmentBundle.Entry.Should().NotBeNullOrEmpty();
        compartmentBundle.Entry.Count.Should().BeGreaterThan(0);
        var compartmentBundleCount = compartmentBundle.Entry.Count;

        // check if direct and compartment search returns equal numbers
        compartmentBundleCount.Should().Be(searchBundleCount);
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

        bool success = versionedFhirStore.TypeSearch(
            ctx,
            out FhirResponseContext response);

        success.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.SerializedResource.Should().NotBeNullOrEmpty();

        response.Resource.Should().NotBeNull();
        var result = response.Resource;
        result.GetType().ToString().Should().Be("Hl7.Fhir.Model.Bundle");

        var bundle = result as Bundle;
        return bundle;
    }

    private void putResource(VersionedFhirStore versionedFhirStore, Resource resource)
    {
        FhirRequestContext ctx = new FhirRequestContext
        {
            TenantName = "r4",
            Store = versionedFhirStore,
            HttpMethod = "PUT",
            SourceObject = resource,
            Url = null,
            Authorization = null,
        };
        versionedFhirStore.InstanceUpdate(ctx, out FhirResponseContext opResponse);
    }


}

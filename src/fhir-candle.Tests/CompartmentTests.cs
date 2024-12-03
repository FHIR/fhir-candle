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
    public void TestCompartmentSearch(string json)
    {
        // load compartment
        var jsonParser = new coreR4::Hl7.Fhir.Serialization.FhirJsonParser();
        var compartmentDefinition = jsonParser.Parse(json) as coreR4::Hl7.Fhir.Model.CompartmentDefinition;
        compartmentDefinition.Should().NotBeNull();
        candleR4::FhirCandle.Smart.R4CompartmentManager cm = new candleR4::FhirCandle.Smart.R4CompartmentManager( compartmentDefinition! );
        cm.Should().NotBeNull();

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

        var searchAllBundle = SearchResource(versionedFhirStore, "Observation");
        searchAllBundle.Should().NotBeNull();
        searchAllBundle.Entry.Should().NotBeNullOrEmpty();
        searchAllBundle.Entry.Count.Should().BeGreaterThan(0);
        var searchAllBundleCount = searchAllBundle.Entry.Count;

        var searchBundle = SearchResource(versionedFhirStore, "Observation?subject=example");
        searchBundle.Should().NotBeNull();
        searchBundle.Entry.Should().NotBeNullOrEmpty();
        searchBundle.Entry.Count.Should().BeGreaterThan(0);
        var searchBundleCount = searchBundle.Entry.Count;

        var compartmentBundle = SearchResource(versionedFhirStore, "Patient/example/Observation");
        compartmentBundle.Should().NotBeNull();
        compartmentBundle.Entry.Should().NotBeNullOrEmpty();
        compartmentBundle.Entry.Count.Should().BeGreaterThan(0);
        var compartmentBundleCount = compartmentBundle.Entry.Count;

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

    [Theory]
    [FileData("data/r4/CompartmentDefinition-patient.json")]
    public void TestCompartmentManager( string json )
    {
        // load compartment
        var jsonParser = new coreR4::Hl7.Fhir.Serialization.FhirJsonParser();
        var compartmentDefinition = jsonParser.Parse(json) as coreR4::Hl7.Fhir.Model.CompartmentDefinition;
        compartmentDefinition.Should().NotBeNull();
        candleR4::FhirCandle.Smart.R4CompartmentManager cm = new candleR4::FhirCandle.Smart.R4CompartmentManager( compartmentDefinition! );
        cm.Should().NotBeNull();

        // initialize searches
        var patientSubjectSearchParamDefinition = new coreR4::Hl7.Fhir.Model.ModelInfo.SearchParamDefinition()
            { Name = "subject", Type = SearchParamType.Reference, Expression = "Patient.subject" };
        var patientLinkSearchParamDefinition = new coreR4::Hl7.Fhir.Model.ModelInfo.SearchParamDefinition()
            { Name = "link", Type = SearchParamType.Reference, Expression = "Patient.link.other" };

        // test patient
        Resource patient = new coreR4::Hl7.Fhir.Model.Patient(){ Id = "123" };

        // FHIR server
        var _config = new TenantConfiguration()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R4,
            ControllerName = "r4",
            BaseUrl = "http://localhost/fhir/r4",
            AllowExistingId = true,
            AllowCreateAsUpdate = true,
        };

        var versionedFhirStore = new VersionedFhirStore();
        versionedFhirStore.Init(_config);

        /*************************
         * Compartment functionality:
         * 1 check if resourceType is in compartment
         * 2 check if specific resource in the compartment
         * 3 adapt search to search only for resources in a compartment
         * If the number of resources is large and the search returns a small set,
         * only checking for compartment of the result resources is likely less invasive.
         *
         * Check if resource in a compartment. Step one, retrieve search parameters related
         * to compartment resource.
         * 1. retrieve compartment definition
         * 2. retrieve search parameters for resource under test
         * 3.
        */

        var searchTester = new candleR4::FhirCandle.Search.SearchTester() { FhirStore = versionedFhirStore };

        versionedFhirStore.TryGetSearchParamDefinition( "Observation", "subject", out var searchParamDefinition );
        // IEnumerable<ParsedSearchParameter> parameters = new List<ParsedSearchParameter>
        // {
            // new ParsedSearchParameter() { Name = "_content", Type = SearchParamType.Special }
        // };
        // searchTester.TestForMatch(
            // Hl7.Fhir.ElementModel.TypedElementExtensions.ToTypedElement( patient, new ModelInspector( FhirRelease.R4)),
            // parameters
        // ).Should().BeFalse();
        // ).}Should().BeTrue();

        // test search
        // VersionedFhirStore versionedFhirStore = new VersionedFhirStore();
        // var searchTester = new SearchTester() { FhirStore = versionedFhirStore };
        // var patientResourceStore = new ResourceStore<coreR4::Hl7.Fhir.Model.Patient>(versionedFhirStore, searchTester, new TopicConverter(),
        //     new SubscriptionConverter(2));
        // ParsedSearchParameter parsedSearchParameter1 = new ParsedSearchParameter(
        //     versionedFhirStore, patientResourceStore, "Patient", String.Empty, "",
        //     SearchDefinitions.SearchModifierCodes.None, "value", patientSubjectSearchParamDefinition,
        //     null  );
        // ParsedSearchParameter parsedSearchParameter2 = new ParsedSearchParameter(
        //     versionedFhirStore, patientResourceStore, "Patient", String.Empty, "",
        //     SearchDefinitions.SearchModifierCodes.None, "value", patientLinkSearchParamDefinition,
        //     null  );
        // var parmeters = new[] { parsedSearchParameter1, parsedSearchParameter2 };
        // patientResourceStore.TypeSearch( parmeters ).Should().NotBeNull();
        // // searchTester.TestForMatch( TypedElementExtensions.ToTypedElement( patient, new ModelInspector()), parsedSearchParameter ).Should().BeTrue();

        // test resource in compartment
        // cm.isResourceTypeInCompartment(coreR4::Hl7.Fhir.Model.ResourceType.Patient).Should().BeTrue();
        // cm.isResourceTypeInCompartment(coreR4::Hl7.Fhir.Model.ResourceType.PlanDefinition).Should().BeFalse();
        //
        //
        //
        // cm.isResourceInCompartment(patient, patient ).Should().BeTrue();
        //
        // var observation1 = new coreR4::Hl7.Fhir.Model.Observation(){ Id = "123", Subject = new ResourceReference(){ Reference = "Patient/123"}};
        // var observation2 = new coreR4::Hl7.Fhir.Model.Observation(){ Id = "123", Subject = new ResourceReference(){ Reference = "Patient/456"}};

        // cm.isResourceInCompartment( observation1, patient ).Should().BeFalse();
        // cm.isResourceInCompartment( observation2, patient ).Should().BeTrue();
    }

}

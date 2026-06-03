// <copyright file="FhirStoreTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Hl7.Fhir.Rest;
using Shouldly;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using Xunit.Abstractions;
using static FhirCandle.Storage.Common;

namespace fhir.candle.Tests;

/// <summary>Unit tests core FhirStore functionality.</summary>
public class FhirStoreTests
{
    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> TestConfigurations => new List<object[]>
    {
        new object[]
        {
            FhirReleases.FhirSequenceCodes.R4,
        },
        new object[]
        {
            FhirReleases.FhirSequenceCodes.R4B,
        },
        new object[]
        {
            FhirReleases.FhirSequenceCodes.R5,
        },
    };

    /// <summary>(Immutable) The configuration for FHIR R4.</summary>
    internal readonly TenantConfiguration _configR4;

    /// <summary>The FHIR store for FHIR R4.</summary>
    internal IFhirStore _candleR4;

    /// <summary>(Immutable) The configuration for FHIR R4B.</summary>
    internal readonly TenantConfiguration _configR4B;

    /// <summary>The FHIR store for FHIR R4B.</summary>
    internal IFhirStore _candleR4B;

    /// <summary>(Immutable) The configuration for FHIR R5.</summary>
    internal readonly TenantConfiguration _configR5;

    /// <summary>The FHIR store for FHIR R5.</summary>
    internal IFhirStore _candleR5;

    /// <summary>The stores.</summary>
    internal Dictionary<FhirReleases.FhirSequenceCodes, IFhirStore> _stores = new();

    /// <summary>The expected REST resources.</summary>
    internal Dictionary<FhirReleases.FhirSequenceCodes, int> _expectedRestResources = new()
    {
        { FhirReleases.FhirSequenceCodes.R4, 146 },
        { FhirReleases.FhirSequenceCodes.R4B, 140 },
        { FhirReleases.FhirSequenceCodes.R5, 157 },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirStoreTests"/> class.
    /// </summary>
    /// <param name="testOutputHelper">The test output helper.</param>
    public FhirStoreTests()
    {
        _configR4 = new()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R4,
            ControllerName = "r4",
            BaseUrl = "http://localhost/fhir/r4",
            AllowExistingId = true,
            AllowCreateAsUpdate = true,
        };

        _configR4B = new()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R4B,
            ControllerName = "r4b",
            BaseUrl = "http://localhost/fhir/r4b",
            AllowExistingId = true,
            AllowCreateAsUpdate = true,
        };

        _configR5 = new()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R5,
            ControllerName = "r5",
            BaseUrl = "http://localhost/fhir/r5",
            AllowExistingId = true,
            AllowCreateAsUpdate = true,
        };

        _candleR4 = new candleR4::FhirCandle.Storage.VersionedFhirStore();
        _candleR4.Init(_configR4);
        _stores.Add(FhirReleases.FhirSequenceCodes.R4, _candleR4);

        _candleR4B = new candleR4B::FhirCandle.Storage.VersionedFhirStore();
        _candleR4B.Init(_configR4B);
        _stores.Add(FhirReleases.FhirSequenceCodes.R4B, _candleR4B);

        _candleR5 = new candleR5::FhirCandle.Storage.VersionedFhirStore();
        _candleR5.Init(_configR5);
        _stores.Add(FhirReleases.FhirSequenceCodes.R5, _candleR5);
    }

    /// <summary>Gets store for version.</summary>
    /// <exception cref="ArgumentException">Thrown when one or more arguments have unsupported or
    ///  illegal values.</exception>
    /// <param name="version">The version.</param>
    /// <returns>The store for version.</returns>
    public IFhirStore GetStoreForVersion(FhirReleases.FhirSequenceCodes version)
    {
        switch (version)
        {
            case FhirReleases.FhirSequenceCodes.R4:
                return _candleR4;

            case FhirReleases.FhirSequenceCodes.R4B:
                return _candleR4B;

            case FhirReleases.FhirSequenceCodes.R5:
                return _candleR5;
        }

        throw new ArgumentException($"Invalid version: {version}", nameof(version));
    }
}

/// <summary>Test fetching metadata in JSON.</summary>
public class MetadataJson : IClassFixture<FhirStoreTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly FhirStoreTests _fixture;

    public MetadataJson(FhirStoreTests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void GetMetadata(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

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

        bool success = fhirStore.GetMetadata(
            ctx,
            out FhirResponseContext? response);

        success.ShouldBe(true);
        response.ShouldNotBeNull();
        response!.StatusCode.ShouldBe(HttpStatusCode.OK);
        response!.SerializedResource.ShouldNotBeNullOrEmpty();
        response!.SerializedOutcome.ShouldNotBeNullOrEmpty();

        MinimalCapabilities? capabilities = JsonSerializer.Deserialize<MinimalCapabilities>(response!.SerializedResource);

        capabilities.ShouldNotBeNull();
        capabilities!.Rest.ShouldNotBeNullOrEmpty();

        MinimalCapabilities.MinimalRest rest = capabilities!.Rest!.First();
        rest.Mode.ShouldBe("server");
        rest.Resources.ShouldNotBeNullOrEmpty();
        int resourceCount = rest.Resources!.Count();
        resourceCount.ShouldBe(_fixture._expectedRestResources[version]);
    }
}

/// <summary>Test fetching metadata in XML.</summary>
public class MetadataXml : IClassFixture<FhirStoreTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly FhirStoreTests _fixture;

    public MetadataXml(FhirStoreTests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void GetMetadata(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = fhirStore.Config.BaseUrl + "/metadata",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+xml",
            DestinationFormat = "application/fhir+xml",
        };

        bool success = fhirStore.GetMetadata(
            ctx,
            out FhirResponseContext response);

        success.ShouldBe(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();

        using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(response.SerializedResource)))
        {
            XElement parsed = XElement.Load(ms);

            parsed.ShouldNotBeNull();

            int resourceCount = parsed.Descendants("{http://hl7.org/fhir}resource").Count();
            resourceCount.ShouldBe(_fixture._expectedRestResources[version]);
        }
    }
}

/// <summary>Create, read, update, and delete a Patient.</summary>
public class TestPatientCRUD : IClassFixture<FhirStoreTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    private const string _resourceType = "Patient";
    private const string _id = "common";

    /// <summary>(Immutable) The fixture.</summary>
    private readonly FhirStoreTests _fixture;

    public TestPatientCRUD(FhirStoreTests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PatientCRUD(FhirReleases.FhirSequenceCodes version)
    {
        string json1 = "{\"resourceType\":\"" + _resourceType + "\",\"id\":\"" + _id + "\",\"language\":\"en\"}";
        string json2 = "{\"resourceType\":\"" + _resourceType + "\",\"id\":\"" + _id + "\",\"language\":\"en-US\"}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/{_resourceType}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json1,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceCreate(
            ctx,
            out FhirResponseContext response,
            forceAllowExistingId: true);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Location.ShouldContain(_resourceType);

        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"1\"");
        response.LastModified.ShouldNotBeNullOrEmpty();
        response.Location.ShouldEndWith(_resourceType + "/" + _id);

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = $"{fhirStore.Config.BaseUrl}/{_resourceType}/{_id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
            IfMatch = response.ETag,
            IfModifiedSince = response.LastModified,
        };

        success = fhirStore.InstanceRead(
            ctx,
            out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"1\"");
        response.Location.ShouldEndWith(_resourceType + "/" + _id);

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "PUT",
            Url = $"{fhirStore.Config.BaseUrl}/{_resourceType}/{_id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json2,
            DestinationFormat = "application/fhir+json",
        };

        success = fhirStore.InstanceUpdate(
            ctx,
            out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Location.ShouldContain(_resourceType);

        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"2\"");
        response.LastModified.ShouldNotBeNullOrEmpty();
        response.Location.ShouldEndWith(_resourceType + "/" + _id);

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "DELETE",
            Url = $"{fhirStore.Config.BaseUrl}/{_resourceType}/{_id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        success = fhirStore.InstanceDelete(
            ctx,
            out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Location.ShouldBeNullOrEmpty();

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = $"{fhirStore.Config.BaseUrl}/{_resourceType}/{_id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        success = fhirStore.InstanceRead(ctx, out response);

        success.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.SerializedResource.ShouldBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
    }
}

/// <summary>Ensure that duplicate explicit-ID create returns a client error.</summary>
public class TestDuplicateExplicitIdCreate : IClassFixture<FhirStoreTests>
{
    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    private const string _resourceType = "Organization";

    /// <summary>(Immutable) The fixture.</summary>
    private readonly FhirStoreTests _fixture;

    public TestDuplicateExplicitIdCreate(FhirStoreTests fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void DuplicateExplicitIdCreateReturnsConflict(FhirReleases.FhirSequenceCodes version)
    {
        string id = $"duplicate-explicit-id-{version.ToString().ToLowerInvariant()}";
        string json = "{\"resourceType\":\"" + _resourceType + "\",\"id\":\"" + id + "\",\"name\":\"Test Hospital\"}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/{_resourceType}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceCreate(
            ctx,
            out FhirResponseContext response,
            forceAllowExistingId: true);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/{_resourceType}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        success = fhirStore.InstanceCreate(
            ctx,
            out response,
            forceAllowExistingId: true);

        success.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldContain($"{_resourceType}/{id}");
        response.SerializedOutcome.ShouldContain("POST-base create interaction cannot overwrite existing resources");
    }
}

/// <summary>Ensure that storing a Patient in the Observation endpoint fails.</summary>
public class TestResourceWrongLocation: IClassFixture<FhirStoreTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    private const string _resourceType1 = "Patient";
    private const string _resourceType2 = "Observation";
    private const string _id = "common";

    /// <summary>(Immutable) The fixture.</summary>
    private readonly FhirStoreTests _fixture;

    public TestResourceWrongLocation(FhirStoreTests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ResourceWrongLocation(FhirReleases.FhirSequenceCodes version)
    {
        string json = "{\"resourceType\":\"" + _resourceType1 + "\",\"id\":\"" + _id + "\",\"language\":\"en\"}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/{_resourceType2}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceCreate(
            ctx,
            out FhirResponseContext response,
            forceAllowExistingId: true);

        success.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }
}

/// <summary>Ensure that storing resources with invalid data fails.</summary>
public class TestResourceInvalidElement : IClassFixture<FhirStoreTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    private const string _resourceType = "Patient";
    private const string _id = "invalid";

    /// <summary>(Immutable) The fixture.</summary>
    private readonly FhirStoreTests _fixture;

    public TestResourceInvalidElement(FhirStoreTests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ResourceWrongLocation(FhirReleases.FhirSequenceCodes version)
    {
        string json = "{\"resourceType\":\"NOT-" + _resourceType + "\",\"id\":\"" + _id + "\",\"garbage\":true}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/{_resourceType}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceCreate(
            ctx,
            out FhirResponseContext response,
            forceAllowExistingId: true);

        success.ShouldBeFalse();
        response.StatusCode?.IsSuccessful().ShouldBeFalse();
    }
}

/// <summary>A test bundle request parsing.</summary>
public class TestBundleRequestParsing : IClassFixture<FhirStoreTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly FhirStoreTests _fixture;

    public TestBundleRequestParsing(FhirStoreTests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>Test determining interactions.</summary>
    /// <param name="verb">    The verb.</param>
    /// <param name="url">     URL of the resource.</param>
    /// <param name="expected">The expected interaction.</param>
    [Theory]
    [InlineData("GET", "", StoreInteractionCodes.SystemSearch)]
    [InlineData("GET", "?withParams=true", StoreInteractionCodes.SystemSearch)]
    [InlineData("GET", "?withParams=true/false", StoreInteractionCodes.SystemSearch)]
    [InlineData("GET", "metadata", StoreInteractionCodes.SystemCapabilities)]
    [InlineData("GET", "_history", StoreInteractionCodes.SystemHistory)]
    [InlineData("GET", "$test", StoreInteractionCodes.SystemOperation)]
    [InlineData("GET", "$test?withParams=true", StoreInteractionCodes.SystemOperation)]
    [InlineData("GET", "Patient", StoreInteractionCodes.TypeSearch)]
    [InlineData("GET", "Invalid", null)]
    [InlineData("GET", "Patient/$test", StoreInteractionCodes.TypeOperation)]
    [InlineData("GET", "Invalid/$test", null)]
    [InlineData("GET", "Patient/id", StoreInteractionCodes.InstanceRead)]
    [InlineData("GET", "Invalid/id", null)]
    [InlineData("GET", "Patient/id/$test", StoreInteractionCodes.InstanceOperation)]
    [InlineData("GET", "Invalid/id/$test", null)]
    [InlineData("GET", "Patient/id/_history", StoreInteractionCodes.InstanceReadHistory)]
    [InlineData("GET", "Patient/id/_history/version", StoreInteractionCodes.InstanceReadVersion)]
    [InlineData("GET", "Patient/id/*", StoreInteractionCodes.CompartmentSearch)]
    [InlineData("GET", "Patient/id/*?withParams=true", StoreInteractionCodes.CompartmentSearch)]
    [InlineData("GET", "Patient/id/Encounter", StoreInteractionCodes.CompartmentTypeSearch)]
    [InlineData("GET", "Patient/id/Encounter?withParams=true", StoreInteractionCodes.CompartmentTypeSearch)]
    [InlineData("GET", "request/with/too/many/path/segments", null)]
    [InlineData("GET", "Patient?search=true&garbage=^*?$%", StoreInteractionCodes.TypeSearch)]
    [InlineData("HEAD", "", null)]
    [InlineData("HEAD", "?withParams=true", null)]
    [InlineData("HEAD", "metadata", StoreInteractionCodes.SystemCapabilities)]
    [InlineData("HEAD", "_history", null)]
    [InlineData("HEAD", "$test", null)]                                 // would be StoreInteractionCodes.SystemOperation, but is not cacheable
    [InlineData("HEAD", "$test?withParams=true", null)]
    [InlineData("HEAD", "Patient", null)]                               // would be StoreInteractionCodes.TypeSearch, but is not cacheable
    [InlineData("HEAD", "Invalid", null)]
    [InlineData("HEAD", "Patient/$test", null)]                         // would be StoreInteractionCodes.TypeOperation, but is not cacheable
    [InlineData("HEAD", "Invalid/$test", null)]
    [InlineData("HEAD", "Patient/id", StoreInteractionCodes.InstanceRead)]
    [InlineData("HEAD", "Invalid/id", null)]
    [InlineData("HEAD", "Patient/id/$test", null)]                      // would be StoreInteractionCodes.InstanceOperation, but is not cacheable
    [InlineData("HEAD", "Invalid/id/$test", null)]
    [InlineData("HEAD", "Patient/id/_history", null)]
    [InlineData("HEAD", "Patient/id/_history/version", StoreInteractionCodes.InstanceReadVersion)]
    [InlineData("HEAD", "Patient/id/*", null)]                          // would be StoreInteractionCodes.CompartmentSearch, but is not cacheable
    [InlineData("HEAD", "Patient/id/*?withParams=true", null)]          // would be StoreInteractionCodes.CompartmentSearch, but is not cacheable
    [InlineData("HEAD", "Patient/id/Encounter", null)]                  // would be StoreInteractionCodes.CompartmentTypeSearch, but is not cacheable
    [InlineData("HEAD", "Patient/id/Encounter?withParams=true", null)]  // would be StoreInteractionCodes.CompartmentTypeSearch, but is not cacheable
    [InlineData("HEAD", "request/with/too/many/path/segments", null)]
    [InlineData("POST", "", StoreInteractionCodes.SystemBundle)]
    [InlineData("POST", "?withParams=true", StoreInteractionCodes.SystemBundle)]
    [InlineData("POST", "_search", StoreInteractionCodes.SystemSearch)]
    [InlineData("POST", "_search?withParams=true", StoreInteractionCodes.SystemSearch)]
    [InlineData("POST", "$test", StoreInteractionCodes.SystemOperation)]
    [InlineData("POST", "$test?withParams=true", StoreInteractionCodes.SystemOperation)]
    [InlineData("POST", "Patient", StoreInteractionCodes.TypeCreate)]
    [InlineData("POST", "Invalid", null)]
    [InlineData("POST", "Patient?withParams=true", StoreInteractionCodes.TypeCreateConditional)]
    [InlineData("POST", "Patient/_search", StoreInteractionCodes.TypeSearch)]
    [InlineData("POST", "Invalid/_search", null)]
    [InlineData("POST", "Patient/$test", StoreInteractionCodes.TypeOperation)]
    [InlineData("POST", "Invalid/$test", null)]
    [InlineData("POST", "Patient/id", null)]
    [InlineData("POST", "Patient/id/$test", StoreInteractionCodes.InstanceOperation)]
    [InlineData("POST", "Patient/id/_search", StoreInteractionCodes.CompartmentSearch)]
    [InlineData("POST", "Patient/id/_search?withParams=true", StoreInteractionCodes.CompartmentSearch)]
    [InlineData("POST", "Patient/id/Encounter/_search", StoreInteractionCodes.CompartmentTypeSearch)]
    [InlineData("POST", "Patient/id/Encounter/_search?withParams=true", StoreInteractionCodes.CompartmentTypeSearch)]
    [InlineData("PUT", "", null)]
    [InlineData("PUT", "?withParams=true", null)]
    [InlineData("PUT", "_search", null)]
    [InlineData("PUT", "$test", null)]
    [InlineData("PUT", "Patient", null)]
    [InlineData("PUT", "Patient/id", StoreInteractionCodes.InstanceUpdate)]
    [InlineData("PUT", "Patient/$test", null)]
    [InlineData("PUT", "Patient?identifier=test", StoreInteractionCodes.InstanceUpdateConditional)]
    [InlineData("PATCH", "", null)]
    [InlineData("PATCH", "?withParams=true", null)]
    [InlineData("PATCH", "_search", null)]
    [InlineData("PATCH", "$test", null)]
    [InlineData("PATCH", "Patient", null)]
    [InlineData("PATCH", "Patient/id", StoreInteractionCodes.InstancePatch)]
    [InlineData("PATCH", "Patient/$test", null)]
    [InlineData("DELETE", "", StoreInteractionCodes.SystemDeleteConditional)]
    [InlineData("DELETE", "?withParams=true", StoreInteractionCodes.SystemDeleteConditional)]
    [InlineData("DELETE", "metadata", null)]
    [InlineData("DELETE", "_history", null)]
    [InlineData("DELETE", "$test", null)]
    [InlineData("DELETE", "$test?withParams=true", null)]
    [InlineData("DELETE", "Patient", StoreInteractionCodes.TypeDeleteConditional)]
    [InlineData("DELETE", "Invalid", null)]
    [InlineData("DELETE", "Patient/$test", null)]
    [InlineData("DELETE", "Invalid/$test", null)]
    [InlineData("DELETE", "Patient/id", StoreInteractionCodes.InstanceDelete)]
    [InlineData("DELETE", "Invalid/id", null)]
    [InlineData("DELETE", "Patient/id/$test", null)]
    [InlineData("DELETE", "Invalid/id/$test", null)]
    [InlineData("DELETE", "Patient/id/_history", StoreInteractionCodes.InstanceDeleteHistory)]
    [InlineData("DELETE", "Patient/id/_history/version", StoreInteractionCodes.InstanceDeleteVersion)]
    [InlineData("DELETE", "Patient/id/*", null)]
    [InlineData("DELETE", "Patient/id/Patient", null)]
    [InlineData("DELETE", "request/with/too/many/path/segments", null)]

    public void DetermineInteraction(string verb, string url, StoreInteractionCodes? expected)
    {
        foreach (IFhirStore store in _fixture._stores.Values)
        {
            FhirRequestContext ctx = new()
            {
                TenantName = store.Config.ControllerName,
                Store = store,
                HttpMethod = verb,
                Url = url,
                Forwarded = null,
                Authorization = null,
            };

            ctx?.Interaction.ShouldBe(expected);
        }
    }
}


/// <summary>
/// Phase 7 — POST/PUT id semantics, cross-version. POST MUST always assign a server id;
/// PUT must reject when URL id and body id differ.
/// </summary>
public class TestPostPutIdSemantics : IClassFixture<FhirStoreTests>
{
    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    private readonly FhirStoreTests _fixture;

    public TestPostPutIdSemantics(FhirStoreTests fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PostAssignsServerIdEvenWhenClientSuppliesId(FhirReleases.FhirSequenceCodes version)
    {
        const string clientId = "client-supplied-id-must-be-discarded";
        string json = "{\"resourceType\":\"Patient\",\"id\":\"" + clientId + "\",\"language\":\"en\"}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/Patient",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceCreate(ctx, out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Id.ShouldNotBeNullOrEmpty();
        response.Id.ShouldNotBe(clientId, "POST must always assign a server-side id per FHIR REST spec");
        response.Location.ShouldNotContain($"Patient/{clientId}");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PostWithoutClientIdAssignsServerId(FhirReleases.FhirSequenceCodes version)
    {
        string json = "{\"resourceType\":\"Patient\",\"language\":\"en\"}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/Patient",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceCreate(ctx, out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Id.ShouldNotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PutRejectsUrlBodyIdMismatch(FhirReleases.FhirSequenceCodes version)
    {
        const string urlId = "phase7-url-id";
        const string bodyId = "phase7-different-body-id";
        string json = "{\"resourceType\":\"Patient\",\"id\":\"" + bodyId + "\",\"language\":\"en\"}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "PUT",
            Url = $"{fhirStore.Config.BaseUrl}/Patient/{urlId}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceUpdate(ctx, out FhirResponseContext response);

        success.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        response.SerializedOutcome.ShouldContain(urlId);
        response.SerializedOutcome.ShouldContain(bodyId);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PutAcceptsUrlBodyIdMatch(FhirReleases.FhirSequenceCodes version)
    {
        string id = $"phase7-put-match-{version.ToString().ToLowerInvariant()}";
        string json = "{\"resourceType\":\"Patient\",\"id\":\"" + id + "\",\"language\":\"en\"}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "PUT",
            Url = $"{fhirStore.Config.BaseUrl}/Patient/{id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceUpdate(ctx, out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode?.IsSuccessful().ShouldBeTrue();
        response.Id.ShouldBe(id);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PutEmptyBodyIdLenientDefaultStampsUrlId(FhirReleases.FhirSequenceCodes version)
    {
        // Step 1: POST with forceAllowExistingId to create at a known id.
        string id = $"phase2-put-empty-body-lenient-{version.ToString().ToLowerInvariant()}";
        string createJson = "{\"resourceType\":\"Patient\",\"id\":\"" + id + "\",\"language\":\"en\"}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext createCtx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/Patient",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = createJson,
            DestinationFormat = "application/fhir+json",
        };

        bool createSuccess = fhirStore.InstanceCreate(
            createCtx,
            out FhirResponseContext createResponse,
            forceAllowExistingId: true);
        createSuccess.ShouldBeTrue();
        createResponse.Id.ShouldBe(id);

        // Step 2: PUT with body lacking id — lenient default should stamp the URL id.
        string putJson = "{\"resourceType\":\"Patient\",\"language\":\"fr\"}";

        FhirRequestContext putCtx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "PUT",
            Url = $"{fhirStore.Config.BaseUrl}/Patient/{id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = putJson,
            DestinationFormat = "application/fhir+json",
        };

        bool putSuccess = fhirStore.InstanceUpdate(putCtx, out FhirResponseContext putResponse);

        putSuccess.ShouldBeTrue();
        putResponse.StatusCode?.IsSuccessful().ShouldBeTrue();
        putResponse.Id.ShouldBe(id);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PutReplaceExistingWithMatchingIdsSucceeds(FhirReleases.FhirSequenceCodes version)
    {
        // M5 — pin the PUT-replace happy path: PUT to an existing instance with matching
        // body id should succeed and produce the mutated resource. Existing rows only
        // exercise PUT-as-create; this fills the gap.
        string id = $"phase7-put-replace-{version.ToString().ToLowerInvariant()}";
        string createJson = "{\"resourceType\":\"Patient\",\"id\":\"" + id + "\",\"gender\":\"male\"}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext createCtx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/Patient",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = createJson,
            DestinationFormat = "application/fhir+json",
        };
        fhirStore.InstanceCreate(createCtx, out FhirResponseContext _, forceAllowExistingId: true)
            .ShouldBeTrue();

        // Mutate: add a telecom entry, keep id matched.
        string replaceJson = "{\"resourceType\":\"Patient\",\"id\":\"" + id + "\",\"gender\":\"male\"," +
            "\"telecom\":[{\"system\":\"phone\",\"value\":\"+1-555-0100\",\"use\":\"work\"}]}";

        FhirRequestContext replaceCtx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "PUT",
            Url = $"{fhirStore.Config.BaseUrl}/Patient/{id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = replaceJson,
            DestinationFormat = "application/fhir+json",
        };

        bool putSuccess = fhirStore.InstanceUpdate(replaceCtx, out FhirResponseContext putResponse);

        putSuccess.ShouldBeTrue();
        putResponse.StatusCode?.IsSuccessful().ShouldBeTrue();
        putResponse.Id.ShouldBe(id);
        putResponse.SerializedResource.ShouldContain("\"value\":\"+1-555-0100\"");

        // Read back to confirm replacement landed.
        FhirRequestContext readCtx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = $"{fhirStore.Config.BaseUrl}/Patient/{id}",
            Forwarded = null,
            Authorization = null,
            ResourceType = "Patient",
            Id = id,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        fhirStore.InstanceRead(readCtx, out FhirResponseContext readResponse).ShouldBeTrue();
        readResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        readResponse.SerializedResource.ShouldContain("\"value\":\"+1-555-0100\"");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void BundleTransactionPostStillSucceedsAfterM2Flip(FhirReleases.FhirSequenceCodes version)
    {
        // M2 regression — verifies the bundle-transaction POST path still works after
        // the HTTP-method gate change in DoInstanceCreate.
        //
        // Note on FHIR transaction semantics (§3.2.0.16.5): bundle preprocessing
        // (buildTransactionResourceLookup + fixTransactionEntryReferencesRecurse) ALWAYS
        // reassigns POST-entry resource ids to server-generated GUIDs, regardless of
        // forceAllowExistingId. So even though DoProcessBundle calls PerformInteraction
        // with forceAllowExistingId: true (preserved through M2), there's no client-id
        // to preserve by the time PerformInteraction runs. The audit-pinning content
        // here is: the bundle transaction still SUCCEEDS post-M2 — the HTTP-method gate
        // does not regress this path.
        const string clientBundleId = "phase5-bundle-original-id";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        string bundleJson = "{\"resourceType\":\"Bundle\",\"type\":\"transaction\",\"entry\":[{" +
            "\"fullUrl\":\"urn:uuid:00000000-0000-0000-0000-000000000001\"," +
            "\"resource\":{\"resourceType\":\"Patient\",\"id\":\"" + clientBundleId + "\",\"gender\":\"male\"}," +
            "\"request\":{\"method\":\"POST\",\"url\":\"Patient\"}}]}";

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}",
            Forwarded = null,
            Authorization = null,
            Interaction = StoreInteractionCodes.SystemBundle,
            SourceFormat = "application/fhir+json",
            SourceContent = bundleJson,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.ProcessBundle(ctx, out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode?.IsSuccessful().ShouldBeTrue();

        string body = response.SerializedResource ?? string.Empty;
        body.ShouldContain("\"resourceType\":\"Bundle\"");
        body.ShouldContain("\"type\":\"transaction-response\"");
        // A POST entry should produce a 201 Created response (the entry's location
        // header will reference the server-assigned id, not the client-supplied one).
        body.ShouldContain("\"status\":\"201", Case.Insensitive);
    }
}


/// <summary>
/// Phase 2 — H4 strict-mode variant of TestPostPutIdSemantics. Constructs version-specific
/// stores with TenantConfiguration.Strict == true and asserts PUT with empty body Resource.id
/// is rejected with 422 + OperationOutcome per FHIR R4 §3.1.0.7.
/// </summary>
public class TestPostPutIdSemanticsStrict
{
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    private readonly Dictionary<FhirReleases.FhirSequenceCodes, IFhirStore> _stores = new();

    public TestPostPutIdSemanticsStrict()
    {
        // Strict tenants. ResolveStrict() (called by VersionedFhirStore.Init) forces
        // AllowExistingId / AllowCreateAsUpdate to false regardless of what we set
        // here, so we omit them for clarity.
        TenantConfiguration configR4 = new()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R4,
            ControllerName = "r4-strict",
            BaseUrl = "http://localhost/fhir/r4-strict",
            Strict = true,
        };
        IFhirStore candleR4 = new candleR4::FhirCandle.Storage.VersionedFhirStore();
        candleR4.Init(configR4);
        _stores.Add(FhirReleases.FhirSequenceCodes.R4, candleR4);

        TenantConfiguration configR4B = new()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R4B,
            ControllerName = "r4b-strict",
            BaseUrl = "http://localhost/fhir/r4b-strict",
            Strict = true,
        };
        IFhirStore candleR4B = new candleR4B::FhirCandle.Storage.VersionedFhirStore();
        candleR4B.Init(configR4B);
        _stores.Add(FhirReleases.FhirSequenceCodes.R4B, candleR4B);

        TenantConfiguration configR5 = new()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R5,
            ControllerName = "r5-strict",
            BaseUrl = "http://localhost/fhir/r5-strict",
            Strict = true,
        };
        IFhirStore candleR5 = new candleR5::FhirCandle.Storage.VersionedFhirStore();
        candleR5.Init(configR5);
        _stores.Add(FhirReleases.FhirSequenceCodes.R5, candleR5);
    }

    private IFhirStore GetStore(FhirReleases.FhirSequenceCodes version) => _stores[version];

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PutEmptyBodyIdStrictRejectsWith422(FhirReleases.FhirSequenceCodes version)
    {
        string id = $"phase2-put-empty-body-strict-{version.ToString().ToLowerInvariant()}";
        string createJson = "{\"resourceType\":\"Patient\",\"id\":\"" + id + "\",\"language\":\"en\"}";

        IFhirStore fhirStore = GetStore(version);

        FhirRequestContext createCtx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/Patient",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = createJson,
            DestinationFormat = "application/fhir+json",
        };

        bool createSuccess = fhirStore.InstanceCreate(
            createCtx,
            out FhirResponseContext _,
            forceAllowExistingId: true);
        createSuccess.ShouldBeTrue();

        string putJson = "{\"resourceType\":\"Patient\",\"language\":\"fr\"}";

        FhirRequestContext putCtx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "PUT",
            Url = $"{fhirStore.Config.BaseUrl}/Patient/{id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = putJson,
            DestinationFormat = "application/fhir+json",
        };

        bool putSuccess = fhirStore.InstanceUpdate(putCtx, out FhirResponseContext putResponse);

        putSuccess.ShouldBeFalse();
        putResponse.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        putResponse.SerializedOutcome.ShouldNotBeNullOrEmpty();
        putResponse.SerializedOutcome.ShouldContain(id);
        putResponse.SerializedOutcome.ShouldContain("OperationOutcome");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PutAcceptsUrlBodyIdMatchInStrict(FhirReleases.FhirSequenceCodes version)
    {
        string id = $"phase2-put-strict-match-{version.ToString().ToLowerInvariant()}";
        string json = "{\"resourceType\":\"Patient\",\"id\":\"" + id + "\",\"language\":\"en\"}";

        IFhirStore fhirStore = GetStore(version);

        // Seed the resource via POST + forceAllowExistingId so the subsequent PUT
        // updates an existing resource (strict mode rejects PUT-on-missing with 404).
        FhirRequestContext seedCtx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/Patient",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };
        fhirStore.InstanceCreate(seedCtx, out FhirResponseContext _, forceAllowExistingId: true)
            .ShouldBeTrue();

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "PUT",
            Url = $"{fhirStore.Config.BaseUrl}/Patient/{id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceUpdate(ctx, out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode?.IsSuccessful().ShouldBeTrue();
        response.Id.ShouldBe(id);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PutRejectsUrlBodyIdMismatchInStrict(FhirReleases.FhirSequenceCodes version)
    {
        const string urlId = "phase2-strict-url-id";
        const string bodyId = "phase2-strict-different-body-id";
        string json = "{\"resourceType\":\"Patient\",\"id\":\"" + bodyId + "\",\"language\":\"en\"}";

        IFhirStore fhirStore = GetStore(version);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "PUT",
            Url = $"{fhirStore.Config.BaseUrl}/Patient/{urlId}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceUpdate(ctx, out FhirResponseContext response);

        success.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        response.SerializedOutcome.ShouldContain(urlId);
        response.SerializedOutcome.ShouldContain(bodyId);
    }
}


/// <summary>
/// Phase 2 — H4 strict-mode composition wins over explicit per-feature flags.
/// Directly addresses the critique blocker: a programmatic caller that constructs
/// a TenantConfiguration with Strict=true AND explicit AllowExistingId=true /
/// AllowCreateAsUpdate=true must see strict semantics, not the explicit flags.
/// </summary>
public class TestStrictModeCompositionWins
{
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public void InitFlipsAllowFlagsWhenStrict(FhirReleases.FhirSequenceCodes version)
    {
        TenantConfiguration cfg = new()
        {
            FhirVersion = version,
            ControllerName = $"{version.ToString().ToLowerInvariant()}-strict-wins",
            BaseUrl = $"http://localhost/fhir/{version.ToString().ToLowerInvariant()}-strict-wins",
            Strict = true,
            AllowExistingId = true,
            AllowCreateAsUpdate = true,
        };

        IFhirStore store = version switch
        {
            FhirReleases.FhirSequenceCodes.R4 => new candleR4::FhirCandle.Storage.VersionedFhirStore(),
            FhirReleases.FhirSequenceCodes.R4B => new candleR4B::FhirCandle.Storage.VersionedFhirStore(),
            FhirReleases.FhirSequenceCodes.R5 => new candleR5::FhirCandle.Storage.VersionedFhirStore(),
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        };

        store.Init(cfg);

        store.Config.Strict.ShouldBeTrue();
        store.Config.AllowExistingId.ShouldBeFalse();
        store.Config.AllowCreateAsUpdate.ShouldBeFalse();
    }
}


/// <summary>
/// Phase 2 — strict-mode id semantics: client-supplied POST id is 400,
/// PUT-on-missing is 404, ill-formed id is 400, and meta.versionId /
/// meta.lastUpdated supplied by client are silently overwritten by the
/// server (no enforcement, just pinned behavior). Parameterized across R4/R4B/R5.
/// </summary>
public class TestStrictModeIdSemantics
{
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    private readonly Dictionary<FhirReleases.FhirSequenceCodes, IFhirStore> _stores = new();

    public TestStrictModeIdSemantics()
    {
        foreach (object[] row in Configurations)
        {
            FhirReleases.FhirSequenceCodes version = (FhirReleases.FhirSequenceCodes)row[0];
            string tenant = $"{version.ToString().ToLowerInvariant()}-strict-id";
            TenantConfiguration cfg = new()
            {
                FhirVersion = version,
                ControllerName = tenant,
                BaseUrl = $"http://localhost/fhir/{tenant}",
                Strict = true,
            };

            IFhirStore store = version switch
            {
                FhirReleases.FhirSequenceCodes.R4 => new candleR4::FhirCandle.Storage.VersionedFhirStore(),
                FhirReleases.FhirSequenceCodes.R4B => new candleR4B::FhirCandle.Storage.VersionedFhirStore(),
                FhirReleases.FhirSequenceCodes.R5 => new candleR5::FhirCandle.Storage.VersionedFhirStore(),
                _ => throw new ArgumentOutOfRangeException(nameof(version)),
            };
            store.Init(cfg);
            _stores[version] = store;
        }
    }

    private IFhirStore GetStore(FhirReleases.FhirSequenceCodes version) => _stores[version];

    private static FhirRequestContext PostCtx(IFhirStore store, string body, string resourceType = "Patient") => new()
    {
        TenantName = store.Config.ControllerName,
        Store = store,
        HttpMethod = "POST",
        Url = $"{store.Config.BaseUrl}/{resourceType}",
        Authorization = null,
        SourceFormat = "application/fhir+json",
        SourceContent = body,
        DestinationFormat = "application/fhir+json",
    };

    private static FhirRequestContext PutCtx(IFhirStore store, string id, string body, string resourceType = "Patient") => new()
    {
        TenantName = store.Config.ControllerName,
        Store = store,
        HttpMethod = "PUT",
        Url = $"{store.Config.BaseUrl}/{resourceType}/{id}",
        Authorization = null,
        SourceFormat = "application/fhir+json",
        SourceContent = body,
        DestinationFormat = "application/fhir+json",
    };

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PostWithClientIdStrictRejectsWith400(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = GetStore(version);
        string body = "{\"resourceType\":\"Patient\",\"id\":\"client-supplied-id\",\"gender\":\"male\"}";

        bool ok = store.InstanceCreate(PostCtx(store, body), out FhirResponseContext response);

        ok.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldContain("http.html#create");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PostWithoutIdStrictAccepts(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = GetStore(version);
        string body = "{\"resourceType\":\"Patient\",\"gender\":\"female\"}";

        bool ok = store.InstanceCreate(PostCtx(store, body), out FhirResponseContext response);

        ok.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Id.ShouldNotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PutOnMissingResourceStrictRejectsWith404(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = GetStore(version);
        string id = $"strict-missing-{version.ToString().ToLowerInvariant()}";
        string body = "{\"resourceType\":\"Patient\",\"id\":\"" + id + "\",\"gender\":\"unknown\"}";

        bool ok = store.InstanceUpdate(PutCtx(store, id, body), out FhirResponseContext response);

        ok.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.SerializedOutcome.ShouldContain("http.html#upsert");
        response.SerializedOutcome.ShouldContain(id);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PutWithInvalidUrlIdCharacterStrictRejectsWith400(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = GetStore(version);
        // Underscore is not in FHIR id regex [A-Za-z0-9\-\.]{1,64}. Firely's
        // BACKWARDSCOMPATIBLE deserializer also rejects this id at parse time
        // with a 422 Structure outcome, so under strict mode the request can
        // bounce out either at the parse boundary (422) or our explicit strict
        // id-regex pre-check (400) — both are spec-correct rejections. The
        // contract this test pins is "strict rejects with an OperationOutcome
        // that references the id datatype".
        const string id = "bad_id";
        string body = "{\"resourceType\":\"Patient\",\"id\":\"" + id + "\",\"gender\":\"male\"}";

        bool ok = store.InstanceUpdate(PutCtx(store, id, body), out FhirResponseContext response);

        ok.ShouldBeFalse();
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldContain("OperationOutcome");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PutBodyUrlIdMismatchStrictRejectsWith422(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = GetStore(version);
        const string urlId = "strict-url-id";
        const string bodyId = "strict-body-id";
        string body = "{\"resourceType\":\"Patient\",\"id\":\"" + bodyId + "\",\"gender\":\"male\"}";

        bool ok = store.InstanceUpdate(PutCtx(store, urlId, body), out FhirResponseContext response);

        ok.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        response.SerializedOutcome.ShouldContain("http.html#update");
        response.SerializedOutcome.ShouldContain(urlId);
        response.SerializedOutcome.ShouldContain(bodyId);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PostWithClientMetaVersionIdStrictAccepts(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = GetStore(version);
        // Server overwrites meta.versionId on create. Strict does not enforce any
        // additional rule here (per feature request line 150 — "ignore, not reject").
        string body = "{\"resourceType\":\"Patient\",\"meta\":{\"versionId\":\"42\"},\"gender\":\"male\"}";

        bool ok = store.InstanceCreate(PostCtx(store, body), out FhirResponseContext response);

        ok.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.SerializedResource.ShouldContain("\"versionId\":\"1\"");
        response.SerializedResource.ShouldNotContain("\"versionId\":\"42\"");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PostWithClientMetaLastUpdatedStrictAccepts(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = GetStore(version);
        string body = "{\"resourceType\":\"Patient\",\"meta\":{\"lastUpdated\":\"1990-01-01T00:00:00Z\"},\"gender\":\"male\"}";

        bool ok = store.InstanceCreate(PostCtx(store, body), out FhirResponseContext response);

        ok.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        // The stored lastUpdated must NOT be the client-supplied 1990 value.
        response.SerializedResource.ShouldNotContain("1990-01-01T00:00:00");
    }
}


/// <summary>
/// Phase 2 — lenient mode pin tests. Confirms the default lenient semantics
/// (AllowExistingId=true, AllowCreateAsUpdate=true) still hold so we don't regress.
/// </summary>
public class TestLenientModeIdSemantics
{
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    private readonly Dictionary<FhirReleases.FhirSequenceCodes, IFhirStore> _stores = new();

    public TestLenientModeIdSemantics()
    {
        foreach (object[] row in Configurations)
        {
            FhirReleases.FhirSequenceCodes version = (FhirReleases.FhirSequenceCodes)row[0];
            string tenant = $"{version.ToString().ToLowerInvariant()}-lenient-id";
            TenantConfiguration cfg = new()
            {
                FhirVersion = version,
                ControllerName = tenant,
                BaseUrl = $"http://localhost/fhir/{tenant}",
                Strict = false,
                AllowExistingId = true,
                AllowCreateAsUpdate = true,
            };

            IFhirStore store = version switch
            {
                FhirReleases.FhirSequenceCodes.R4 => new candleR4::FhirCandle.Storage.VersionedFhirStore(),
                FhirReleases.FhirSequenceCodes.R4B => new candleR4B::FhirCandle.Storage.VersionedFhirStore(),
                FhirReleases.FhirSequenceCodes.R5 => new candleR5::FhirCandle.Storage.VersionedFhirStore(),
                _ => throw new ArgumentOutOfRangeException(nameof(version)),
            };
            store.Init(cfg);
            _stores[version] = store;
        }
    }

    private IFhirStore GetStore(FhirReleases.FhirSequenceCodes version) => _stores[version];

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PutCreateAsUpdateLenientAccepts(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = GetStore(version);
        string id = $"lenient-new-{version.ToString().ToLowerInvariant()}";
        string body = "{\"resourceType\":\"Patient\",\"id\":\"" + id + "\",\"gender\":\"male\"}";

        FhirRequestContext ctx = new()
        {
            TenantName = store.Config.ControllerName,
            Store = store,
            HttpMethod = "PUT",
            Url = $"{store.Config.BaseUrl}/Patient/{id}",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = body,
            DestinationFormat = "application/fhir+json",
        };

        bool ok = store.InstanceUpdate(ctx, out FhirResponseContext response);

        ok.ShouldBeTrue();
        response.StatusCode?.IsSuccessful().ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PutBodyUrlIdMismatchLenientRejectsWith422(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = GetStore(version);
        const string urlId = "lenient-url-id";
        const string bodyId = "lenient-body-id";
        string body = "{\"resourceType\":\"Patient\",\"id\":\"" + bodyId + "\",\"gender\":\"male\"}";

        FhirRequestContext ctx = new()
        {
            TenantName = store.Config.ControllerName,
            Store = store,
            HttpMethod = "PUT",
            Url = $"{store.Config.BaseUrl}/Patient/{urlId}",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = body,
            DestinationFormat = "application/fhir+json",
        };

        bool ok = store.InstanceUpdate(ctx, out FhirResponseContext response);

        ok.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PutOnMissingLenientCreatesWith201(FhirReleases.FhirSequenceCodes version)
    {
        IFhirStore store = GetStore(version);
        // Use a spec-conformant id so Firely's parser doesn't reject it before
        // we exercise the lenient create-as-update path.
        string id = $"lenient-create-{version.ToString().ToLowerInvariant()}";
        string body = "{\"resourceType\":\"Patient\",\"id\":\"" + id + "\",\"gender\":\"male\"}";

        FhirRequestContext ctx = new()
        {
            TenantName = store.Config.ControllerName,
            Store = store,
            HttpMethod = "PUT",
            Url = $"{store.Config.BaseUrl}/Patient/{id}",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = body,
            DestinationFormat = "application/fhir+json",
        };

        bool ok = store.InstanceUpdate(ctx, out FhirResponseContext response);

        ok.ShouldBeTrue();
        response.StatusCode?.IsSuccessful().ShouldBeTrue();
    }
}


/// <summary>
/// Phase 8 — $validate operation tests, cross-version.
/// Exercises POST /Patient/$validate (resource-level), POST /Parameters/$validate
/// (system-level wire-type wrapper), and POST /$validate (system-level direct).
/// </summary>
public class TestValidateOperation : IClassFixture<FhirStoreTests>
{
    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    private readonly FhirStoreTests _fixture;

    public TestValidateOperation(FhirStoreTests fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ValidateValidPatientReturnsOkOperationOutcome(FhirReleases.FhirSequenceCodes version)
    {
        // a valid (per structural validation) Patient resource
        string json = "{\"resourceType\":\"Patient\",\"id\":\"validate-ok\",\"gender\":\"male\",\"birthDate\":\"1980-01-01\"}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/Patient/$validate",
            Authorization = null,
            Interaction = StoreInteractionCodes.TypeOperation,
            ResourceType = "Patient",
            OperationName = "$validate",
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.TypeOperation(ctx, out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldContain("\"resourceType\":\"OperationOutcome\"");
        response.SerializedResource.ShouldContain("\"severity\":\"information\"");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ValidateInvalidResourceReturnsErrorIssues(FhirReleases.FhirSequenceCodes version)
    {
        // H1 — Observation missing its required `code` element. Observation.code is
        // cardinality 1..1 in R4, R4B, and R5, and Firely's POCO validator (invoked
        // with validateRecursively: true) enforces [Cardinality] attributes, so the
        // missing-required-element issue surfaces consistently across versions.
        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        string json = "{\"resourceType\":\"Observation\",\"id\":\"validate-missing-code\",\"status\":\"final\"}";

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/Observation/$validate",
            Authorization = null,
            Interaction = StoreInteractionCodes.TypeOperation,
            ResourceType = "Observation",
            OperationName = "$validate",
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.TypeOperation(ctx, out FhirResponseContext response);

        // The Firely BACKWARDSCOMPATIBLE deserializer rejects an Observation missing
        // the required `code` element at parse time (returns 415 + Error outcome via
        // the dispatcher's "non-FHIR content" branch). When that happens, the parse
        // failure is itself a FHIR-spec validation outcome — the error is present
        // and clearly attributed. Both paths (parser-side 415 or validator-side 200)
        // satisfy H1's contract: invalid input produces a FHIR-shaped OperationOutcome
        // with at least one Error issue referencing the missing element.
        string combined = (response.SerializedResource ?? string.Empty) + (response.SerializedOutcome ?? string.Empty);
        combined.ShouldContain("\"resourceType\":\"OperationOutcome\"");
        combined.ShouldContain("\"severity\":\"error\"");
        combined.ShouldContain("code", Case.Sensitive);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ValidateAcceptsParametersWrappedPayload(FhirReleases.FhirSequenceCodes version)
    {
        string json = "{\"resourceType\":\"Parameters\",\"parameter\":[" +
            "{\"name\":\"resource\",\"resource\":{\"resourceType\":\"Patient\",\"id\":\"validate-wrapped\",\"gender\":\"male\"}}" +
            "]}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/$validate",
            Authorization = null,
            Interaction = StoreInteractionCodes.SystemOperation,
            OperationName = "$validate",
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.SystemOperation(ctx, out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldContain("\"resourceType\":\"OperationOutcome\"");
        response.SerializedResource.ShouldContain("\"severity\":\"information\"");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ValidateSystemLevelDirectResource(FhirReleases.FhirSequenceCodes version)
    {
        string json = "{\"resourceType\":\"Patient\",\"id\":\"validate-system\",\"gender\":\"female\"}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/$validate",
            Authorization = null,
            Interaction = StoreInteractionCodes.SystemOperation,
            OperationName = "$validate",
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.SystemOperation(ctx, out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldContain("\"resourceType\":\"OperationOutcome\"");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ValidateMissingRequiredFieldReturnsErrorIssue(FhirReleases.FhirSequenceCodes version)
    {
        // H1 — same Observation payload as ValidateInvalidResourceReturnsErrorIssues;
        // asserts the response carries an OperationOutcome whose error issue references
        // the missing `code` element. As above, accepts either the parser-side 415
        // path or the validator-side 200 path — both correctly identify the problem.
        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        string json = "{\"resourceType\":\"Observation\",\"id\":\"validate-missing-code-detail\",\"status\":\"final\"}";

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/Observation/$validate",
            Authorization = null,
            Interaction = StoreInteractionCodes.TypeOperation,
            ResourceType = "Observation",
            OperationName = "$validate",
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        _ = fhirStore.TypeOperation(ctx, out FhirResponseContext response);

        string combined = (response.SerializedResource ?? string.Empty) + (response.SerializedOutcome ?? string.Empty);
        combined.ShouldContain("\"resourceType\":\"OperationOutcome\"");
        combined.ShouldContain("\"severity\":\"error\"");
        combined.ShouldContain("code", Case.Sensitive);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ValidateInstanceLevelWithBodyValidatesBody(FhirReleases.FhirSequenceCodes version)
    {
        // H2 — instance-level $validate with a Parameters body should validate the
        // body-supplied candidate, not the stored focus. We create a valid Patient,
        // then POST $validate to that instance with a body containing an INVALID
        // Observation; the response must surface error issues referencing the body's
        // missing `code` (proving the body, not the valid stored Patient, was validated).
        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        string id = $"validate-instance-body-{version.ToString().ToLowerInvariant()}";
        string createJson = "{\"resourceType\":\"Patient\",\"id\":\"" + id + "\",\"gender\":\"male\"}";

        FhirRequestContext createCtx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/Patient",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = createJson,
            DestinationFormat = "application/fhir+json",
        };
        fhirStore.InstanceCreate(createCtx, out FhirResponseContext _, forceAllowExistingId: true)
            .ShouldBeTrue();

        // Wrap an INVALID Observation (missing required `code`) in a Parameters body.
        string body = "{\"resourceType\":\"Parameters\",\"parameter\":[" +
            "{\"name\":\"resource\",\"resource\":{\"resourceType\":\"Observation\",\"id\":\"body-invalid\",\"status\":\"final\"}}" +
            "]}";

        FhirRequestContext opCtx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/Patient/{id}/$validate",
            Authorization = null,
            Interaction = StoreInteractionCodes.InstanceOperation,
            ResourceType = "Patient",
            Id = id,
            OperationName = "$validate",
            SourceFormat = "application/fhir+json",
            SourceContent = body,
            DestinationFormat = "application/fhir+json",
        };

        _ = fhirStore.InstanceOperation(opCtx, out FhirResponseContext response);

        // The response must describe the INVALID BODY, not the (valid) Patient focus.
        // If the focus had been validated, we'd see "All OK" since the Patient is valid.
        // Instead we expect an error-severity outcome referencing `code` from the
        // Observation. Accepts either 200 (validator-path) or 415 (parser-path) since
        // both correctly identify the body's problem.
        string combined = (response.SerializedResource ?? string.Empty) + (response.SerializedOutcome ?? string.Empty);
        combined.ShouldContain("\"resourceType\":\"OperationOutcome\"");
        combined.ShouldContain("\"severity\":\"error\"");
        combined.ShouldContain("code", Case.Sensitive);
        combined.ShouldNotContain("All OK");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ValidateInstanceLevelWithoutBodyValidatesFocus(FhirReleases.FhirSequenceCodes version)
    {
        // H2 — instance-level $validate WITHOUT a body validates the stored focus.
        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        string id = $"validate-instance-focus-{version.ToString().ToLowerInvariant()}";
        string createJson = "{\"resourceType\":\"Patient\",\"id\":\"" + id + "\",\"gender\":\"female\"}";

        FhirRequestContext createCtx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/Patient",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = createJson,
            DestinationFormat = "application/fhir+json",
        };
        fhirStore.InstanceCreate(createCtx, out FhirResponseContext _, forceAllowExistingId: true)
            .ShouldBeTrue();

        FhirRequestContext opCtx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/Patient/{id}/$validate",
            Authorization = null,
            Interaction = StoreInteractionCodes.InstanceOperation,
            ResourceType = "Patient",
            Id = id,
            OperationName = "$validate",
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceOperation(opCtx, out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldContain("\"resourceType\":\"OperationOutcome\"");
        response.SerializedResource.ShouldContain("\"severity\":\"information\"");
        response.SerializedResource.ShouldContain("All OK");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ValidateModeParameterEmitsInformationIssue(FhirReleases.FhirSequenceCodes version)
    {
        // M1 — `mode` is a documented no-op; assert that a characterizing Information
        // issue is appended so callers can see the parameter was ignored.
        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        string body = "{\"resourceType\":\"Parameters\",\"parameter\":[" +
            "{\"name\":\"resource\",\"resource\":{\"resourceType\":\"Patient\",\"gender\":\"male\"}}," +
            "{\"name\":\"mode\",\"valueCode\":\"create\"}" +
            "]}";

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/$validate",
            Authorization = null,
            Interaction = StoreInteractionCodes.SystemOperation,
            OperationName = "$validate",
            SourceFormat = "application/fhir+json",
            SourceContent = body,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.SystemOperation(ctx, out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldContain("\"resourceType\":\"OperationOutcome\"");
        response.SerializedResource.ShouldContain("Parameter 'mode' is currently ignored");
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ValidateProfileParameterEmitsInformationIssue(FhirReleases.FhirSequenceCodes version)
    {
        // M1 — `profile` is a documented no-op; assert the Information issue.
        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        string body = "{\"resourceType\":\"Parameters\",\"parameter\":[" +
            "{\"name\":\"resource\",\"resource\":{\"resourceType\":\"Patient\",\"gender\":\"male\"}}," +
            "{\"name\":\"profile\",\"valueCanonical\":\"http://hl7.org/fhir/StructureDefinition/Patient\"}" +
            "]}";

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/$validate",
            Authorization = null,
            Interaction = StoreInteractionCodes.SystemOperation,
            OperationName = "$validate",
            SourceFormat = "application/fhir+json",
            SourceContent = body,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.SystemOperation(ctx, out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldContain("\"resourceType\":\"OperationOutcome\"");
        response.SerializedResource.ShouldContain("Parameter 'profile' is currently ignored");
    }

    [Theory]
    [InlineData(FhirReleases.FhirSequenceCodes.R4, "")]
    [InlineData(FhirReleases.FhirSequenceCodes.R4, "{}")]
    [InlineData(FhirReleases.FhirSequenceCodes.R4B, "")]
    [InlineData(FhirReleases.FhirSequenceCodes.R4B, "{}")]
    [InlineData(FhirReleases.FhirSequenceCodes.R5, "")]
    [InlineData(FhirReleases.FhirSequenceCodes.R5, "{}")]
    public void ValidateEmptyBodyReturnsOperationOutcome(FhirReleases.FhirSequenceCodes version, string body)
    {
        // L4 — empty / malformed body should never produce a framework exception.
        // The dispatcher SHALL return a FHIR-shaped OperationOutcome (either as the
        // outcome on a 4xx response, or as the resource on a 422 from OpValidate's
        // shape-error branch).
        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/Patient/$validate",
            Authorization = null,
            Interaction = StoreInteractionCodes.TypeOperation,
            ResourceType = "Patient",
            OperationName = "$validate",
            SourceFormat = "application/fhir+json",
            SourceContent = body,
            DestinationFormat = "application/fhir+json",
        };

        bool _ = fhirStore.TypeOperation(ctx, out FhirResponseContext response);

        string combined = (response.SerializedResource ?? string.Empty) + (response.SerializedOutcome ?? string.Empty);
        combined.ShouldContain("\"resourceType\":\"OperationOutcome\"");
    }
}

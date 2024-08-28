// <copyright file="FhirStoreTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using fhir.candle.Tests.Models;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using FluentAssertions;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Org.BouncyCastle.Utilities.Collections;
using System.Net;
using System.Security.AccessControl;
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
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.GetMetadata(
            ctx,
            out FhirResponseContext? response);

        success.Should().Be(true);
        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(HttpStatusCode.OK);
        response!.SerializedResource.Should().NotBeNullOrEmpty();
        response!.SerializedOutcome.Should().NotBeNullOrEmpty();

        MinimalCapabilities? capabilities = JsonSerializer.Deserialize<MinimalCapabilities>(response!.SerializedResource);

        capabilities.Should().NotBeNull();
        capabilities!.Rest.Should().NotBeNullOrEmpty();

        MinimalCapabilities.MinimalRest rest = capabilities!.Rest!.First();
        rest.Mode.Should().Be("server");
        rest.Resources.Should().NotBeNullOrEmpty();
        int resourceCount = rest.Resources!.Count();
        resourceCount.Should().Be(_fixture._expectedRestResources[version]);
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
            Authorization = null,
            SourceFormat = "application/fhir+xml",
            DestinationFormat = "application/fhir+xml",
        };

        bool success = fhirStore.GetMetadata(
            ctx,
            out FhirResponseContext response);

        success.Should().Be(true);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.SerializedResource.Should().NotBeNullOrEmpty();
        response.SerializedOutcome.Should().NotBeNullOrEmpty();

        using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(response.SerializedResource)))
        {
            XElement parsed = XElement.Load(ms);

            parsed.Should().NotBeNull();

            int resourceCount = parsed.Descendants("{http://hl7.org/fhir}resource").Count();
            resourceCount.Should().Be(_fixture._expectedRestResources[version]);
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
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json1,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceCreate(
            ctx,
            out FhirResponseContext response);

        success.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Location.Should().Contain(_resourceType);

        response.SerializedResource.Should().NotBeNullOrEmpty();
        response.SerializedOutcome.Should().NotBeNullOrEmpty();
        response.ETag.Should().Be("W/\"1\"");
        response.LastModified.Should().NotBeNullOrEmpty();
        response.Location.Should().EndWith(_resourceType + "/" + _id);

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = $"{fhirStore.Config.BaseUrl}/{_resourceType}/{_id}",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
            IfMatch = response.ETag,
            IfModifiedSince = response.LastModified,
        };

        success = fhirStore.InstanceRead(
            ctx,
            out response);

        success.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.SerializedResource.Should().NotBeNullOrEmpty();
        response.SerializedOutcome.Should().NotBeNullOrEmpty();
        response.ETag.Should().Be("W/\"1\"");
        response.Location.Should().EndWith(_resourceType + "/" + _id);

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "PUT",
            Url = $"{fhirStore.Config.BaseUrl}/{_resourceType}/{_id}",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json2,
            DestinationFormat = "application/fhir+json",
        };

        success = fhirStore.InstanceUpdate(
            ctx,
            out response);

        success.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Location.Should().Contain(_resourceType);

        response.SerializedResource.Should().NotBeNullOrEmpty();
        response.SerializedOutcome.Should().NotBeNullOrEmpty();
        response.ETag.Should().Be("W/\"2\"");
        response.LastModified.Should().NotBeNullOrEmpty();
        response.Location.Should().EndWith(_resourceType + "/" + _id);

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "DELETE",
            Url = $"{fhirStore.Config.BaseUrl}/{_resourceType}/{_id}",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        success = fhirStore.InstanceDelete(
            ctx,
            out response);

        success.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Location.Should().BeNullOrEmpty();

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = $"{fhirStore.Config.BaseUrl}/{_resourceType}/{_id}",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        success = fhirStore.InstanceRead(ctx, out response);

        success.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.SerializedResource.Should().BeNullOrEmpty();
        response.SerializedOutcome.Should().NotBeNullOrEmpty();
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
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceCreate(
            ctx,
            out FhirResponseContext response);

        success.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
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
        string json = "{\"resourceType\":\"" + _resourceType + "\",\"id\":\"" + _id + "\",\"garbage\":true}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/{_resourceType}",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceCreate(
            ctx,
            out FhirResponseContext response);

        success.Should().BeFalse();
        response.StatusCode?.IsSuccessful().Should().BeFalse();
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
    [InlineData("GET", "Patient/id/Patient", StoreInteractionCodes.CompartmentTypeSearch)]
    [InlineData("GET", "request/with/too/many/path/segments", null)]
    [InlineData("GET", "Patient?search=true&garbage=^*?$%", StoreInteractionCodes.TypeSearch)]
    [InlineData("HEAD", "", null)]
    [InlineData("HEAD", "?withParams=true", null)]
    [InlineData("HEAD", "metadata", StoreInteractionCodes.SystemCapabilities)]
    [InlineData("HEAD", "_history", null)]
    [InlineData("HEAD", "$test", null)]
    [InlineData("HEAD", "$test?withParams=true", null)]
    [InlineData("HEAD", "Patient", null)]
    [InlineData("HEAD", "Invalid", null)]
    [InlineData("HEAD", "Patient/$test", null)]
    [InlineData("HEAD", "Invalid/$test", null)]
    [InlineData("HEAD", "Patient/id", StoreInteractionCodes.InstanceRead)]
    [InlineData("HEAD", "Invalid/id", null)]
    [InlineData("HEAD", "Patient/id/$test", null)]
    [InlineData("HEAD", "Invalid/id/$test", null)]
    [InlineData("HEAD", "Patient/id/_history", null)]
    [InlineData("HEAD", "Patient/id/_history/version", StoreInteractionCodes.InstanceReadVersion)]
    [InlineData("HEAD", "Patient/id/*", null)]
    [InlineData("HEAD", "Patient/id/Patient", null)]
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
    [InlineData("PUT", "", null)]
    [InlineData("PUT", "?withParams=true", null)]
    [InlineData("PUT", "_search", null)]
    [InlineData("PUT", "$test", null)]
    [InlineData("PUT", "Patient", null)]
    [InlineData("PUT", "Patient/id", StoreInteractionCodes.InstanceUpdate)]
    [InlineData("PUT", "Patient/$test", null)]
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
                Authorization = null,
            };

            ctx?.Interaction.Should().Be(expected);
        }
    }
}

﻿// <copyright file="FhirStoreTestsR4BResource.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4B;
extern alias coreR4B;

using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using fhir.candle.Tests.Models;
using Shouldly;
using System.Text.Json;
using Xunit.Abstractions;
using candleR4B::FhirCandle.Storage;
using fhir.candle.Tests.Extensions;
using System.Net;

namespace fhir.candle.Tests;

/// <summary>Unit tests for FHIR R4B.</summary>
public class R4BTests
{
    /// <summary>The FHIR store.</summary>
    internal IFhirStore _store;

    /// <summary>(Immutable) The configuration.</summary>
    internal readonly TenantConfiguration _config;

    /// <summary>
    /// (Immutable) The authorizations.
    /// </summary>
    internal readonly Dictionary<string, AuthorizationInfo> _authorizations;

    /// <summary>(Immutable) The total number of patients.</summary>
    internal const int _patientCount = 5;

    /// <summary>(Immutable) The number of patients coded as male.</summary>
    internal const int _patientsMale = 3;

    /// <summary>(Immutable) The number of patients coded as female.</summary>
    internal const int _patientsFemale = 1;

    /// <summary>(Immutable) The total number of observations.</summary>
    internal const int _observationCount = 6;

    /// <summary>(Immutable) The number of observations that are vital signs.</summary>
    internal const int _observationsVitalSigns = 3;

    /// <summary>(Immutable) The number of observations with the subject 'example'.</summary>
    internal const int _observationsWithSubjectExample = 4;

    /// <summary>Initializes a new instance of the <see cref="R4BTests"/> class.</summary>
    public R4BTests()
    {
        string path = Path.GetRelativePath(Directory.GetCurrentDirectory(), "data/r4b");
        DirectoryInfo? loadDirectory = null;

        if (Directory.Exists(path))
        {
            loadDirectory = new DirectoryInfo(path);
        }

        _config = new()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R4B,
            ControllerName = "r4b",
            BaseUrl = "http://localhost/fhir/r4b",
            LoadDirectory = loadDirectory,
        };

        _store = new VersionedFhirStore();
        _store.Init(_config);


        _authorizations = [];
        _authorizations.Add("PatientExampleFull", new()
        {
            Tenant = _config.ControllerName,
            RequestParameters = new()
            {
                ResponseType = "code",
                ClientId = "client_id",
                Launch = "launch/patient",
                Scope = "fhirUser launch/patient patient/*.*",
                Audience = _config.BaseUrl,
            },
            RemoteIpAddress = "127.0.0.1",
            LaunchPatient = "Patient/example",
            Scopes = new()
                    {
                        { "fhirUser", true },
                        { "launch/patient", true },
                        { "patient/*.*", true },
                    },
            PatientScopes = ["*.*"],
        });

        _authorizations.Add("PatientExamplePatientOnly", new()
        {
            Tenant = _config.ControllerName,
            RequestParameters = new()
            {
                ResponseType = "code",
                ClientId = "client_id",
                Launch = "launch/patient",
                Scope = "fhirUser launch/patient patient/Patient.*",
                Audience = _config.BaseUrl,
            },
            RemoteIpAddress = "127.0.0.1",
            LaunchPatient = "Patient/example",
            Scopes = new()
                    {
                        { "fhirUser", true },
                        { "launch/patient", true },
                        { "patient/Patient.*", true },
                    },
            PatientScopes = ["Patient.*"],
        });

        _authorizations.Add("PatientDoesNotExistFull", new()
        {
            Tenant = _config.ControllerName,
            RequestParameters = new()
            {
                ResponseType = "code",
                ClientId = "client_id",
                Launch = "launch/patient",
                Scope = "fhirUser launch/patient patient/*.*",
                Audience = _config.BaseUrl,
            },
            RemoteIpAddress = "127.0.0.1",
            LaunchPatient = "Patient/a-patient-that-does-not-exist",
            Scopes = new()
                    {
                        { "fhirUser", true },
                        { "launch/patient", true },
                        { "patient/*.*", true },
                    },
            PatientScopes = ["*.*"],
        });
    }
}

/// <summary>Test R5 patient looped.</summary>
public class R4BTestsPatientLooped : IClassFixture<R4BTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4BTests _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="R5TestsPatientLooped"/> class.
    /// </summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">(Immutable) The test output helper.</param>
    public R4BTestsPatientLooped(R4BTests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("_id=example", 100)]
    public void LoopedPatientsSearch(string search, int loopCount)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "GET",
            Url = _fixture._store.Config.BaseUrl + "/Patient?" + search,
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        for (int i = 0; i < loopCount; i++)
        {
            _fixture._store.TypeSearch(ctx, out _).ShouldBeTrue();
        }
    }
}

/// <summary>Test R4B Observation searches.</summary>
public class R4BTestsObservation : IClassFixture<R4BTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4BTests _fixture;

    /// <summary>Initializes a new instance of the <see cref="R5TestsObservation"/> class.</summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">The test output helper.</param>
    public R4BTestsObservation(R4BTests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData(null, "_id:not=example", (R4BTests._observationCount - 1))]
    [InlineData(null, "_id=AnIdThatDoesNotExist", 0)]
    [InlineData(null, "_id=example", 1)]
    [InlineData(null, "_id=example&_include=Observation:patient", 1, 2)]
    [InlineData(null, "code-value-quantity=http://loinc.org|29463-7$185|http://unitsofmeasure.org|[lb_av]", 1)]
    [InlineData(null, "code-value-quantity=http://loinc.org|29463-7,http://example.org|testing$185|http://unitsofmeasure.org|[lb_av]", 1)]
    [InlineData(null, "code-value-quantity=http://loinc.org|29463-7,urn:iso:std:iso:11073:10101|152584$185|http://unitsofmeasure.org|[lb_av],820|urn:iso:std:iso:11073:10101|265201", 2)]
    [InlineData(null, "value-quantity=185|http://unitsofmeasure.org|[lb_av]", 1)]
    [InlineData(null, "value-quantity=185|http://unitsofmeasure.org|lbs", 1)]
    [InlineData(null, "value-quantity=185||[lb_av]", 1)]
    [InlineData(null, "value-quantity=185||lbs", 1)]
    [InlineData(null, "value-quantity=185", 1)]
    [InlineData(null, "value-quantity=ge185|http://unitsofmeasure.org|[lb_av]", 1)]
    [InlineData(null, "value-quantity=ge185||[lb_av]", 1)]
    [InlineData(null, "value-quantity=ge185||lbs", 1)]
    [InlineData(null, "value-quantity=ge185", 2)]
    [InlineData(null, "value-quantity=gt185|http://unitsofmeasure.org|[lb_av]", 0)]
    [InlineData(null, "value-quantity=gt185||[lb_av]", 0)]
    [InlineData(null, "value-quantity=gt185||lbs", 0)]
    [InlineData(null, "value-quantity=84.1|http://unitsofmeasure.org|[kg]", 0)]       // test unit conversion
    [InlineData(null, "value-quantity=820|urn:iso:std:iso:11073:10101|265201", 1)]
    [InlineData(null, "value-quantity=820|urn:iso:std:iso:11073:10101|cL/s", 1)]
    [InlineData(null, "value-quantity=820|urn:iso:std:iso:11073:10101|cl/s", 1)]
    [InlineData(null, "value-quantity=820||265201", 1)]
    [InlineData(null, "value-quantity=820||cL/s", 1)]
    [InlineData(null, "subject=Patient/example", R4BTests._observationsWithSubjectExample)]
    [InlineData(null, "subject:Patient=Patient/example", R5Tests._observationsWithSubjectExample)]
    [InlineData(null, "subject:Device=Patient/example", 0)]
    [InlineData(null, "subject=Patient/UnknownPatientId", 0)]
    [InlineData(null, "subject=example", R4BTests._observationsWithSubjectExample)]
    [InlineData(null, "code=http://loinc.org|9272-6", 1)]
    [InlineData(null, "code=http://snomed.info/sct|169895004", 1)]
    [InlineData(null, "code=http://snomed.info/sct|9272-6", 0)]
    [InlineData(null, "_profile=http://hl7.org/fhir/StructureDefinition/vitalsigns", R4BTests._observationsVitalSigns)]
    [InlineData(null, "_profile:missing=true", (R4BTests._observationCount - R4BTests._observationsVitalSigns))]
    [InlineData(null, "_profile:missing=false", R4BTests._observationsVitalSigns)]
    [InlineData(null, "subject.name=peter", R4BTests._observationsWithSubjectExample)]
    [InlineData(null, "subject:Patient.name=peter", R4BTests._observationsWithSubjectExample)]
    [InlineData(null, "subject._id=example", R4BTests._observationsWithSubjectExample)]
    [InlineData(null, "subject:Patient._id=example", R4BTests._observationsWithSubjectExample)]
    [InlineData(null, "subject._id=example&_include=Observation:patient", R4BTests._observationsWithSubjectExample, R4BTests._observationsWithSubjectExample + 1)]
    [InlineData("PatientExampleFull", "subject=Patient/example", R4Tests._observationsWithSubjectExample)]
    [InlineData("PatientDoesNotExistFull", "subject=Patient/example", 0)]
    [InlineData("PatientExamplePatientOnly", "subject=Patient/example", 0)]
    public void ObservationSearch(string? authKey, string search, int matchCount, int? entryCount = null)
    {
        AuthorizationInfo? authInfo = null;

        if ((authKey != null) &&
            !_fixture._authorizations.TryGetValue(authKey, out authInfo))
        {
            throw new InvalidOperationException($"Authorization key '{authKey}' not found");
        }

        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "GET",
            Url = _fixture._store.Config.BaseUrl + "/Observation?" + search,
            Forwarded = null,
            Authorization = authInfo,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        bool success = _fixture._store.TypeSearch(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(response.SerializedResource);

        results.ShouldNotBeNull();
        results!.Total.ShouldBe(matchCount);
        if (entryCount != null)
        {
            results!.Entries.ShouldHaveCount((int)entryCount);
        }

        results!.Links.ShouldNotBeNullOrEmpty();
        string selfLink = results!.Links!.Where(l => l.Relation.Equals("self"))?.Select(l => l.Url).First() ?? string.Empty;
        selfLink.ShouldNotBeNullOrEmpty();
        selfLink.ShouldStartWith(_fixture._config.BaseUrl + "/Observation?");
        foreach (string searchPart in search.Split('&'))
        {
            selfLink.ShouldContain(searchPart);
        }

        //_testOutputHelper.WriteLine(bundle);

        ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = _fixture._store.Config.BaseUrl + "/Observation/_search",
            Forwarded = null,
            Authorization = authInfo,
            SourceContent = search,
            SourceFormat = "application/x-www-form-urlencoded",
            DestinationFormat = "application/fhir+json",
        };

        success = _fixture._store.TypeSearch(
            ctx,
            out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();

        results = JsonSerializer.Deserialize<MinimalBundle>(response.SerializedResource);

        results.ShouldNotBeNull();
        results!.Total.ShouldBe(matchCount);
        if (entryCount != null)
        {
            results!.Entries.ShouldHaveCount((int)entryCount);
        }

        results!.Links.ShouldNotBeNullOrEmpty();
        selfLink = results!.Links!.Where(l => l.Relation.Equals("self"))?.Select(l => l.Url).First() ?? string.Empty;
        selfLink.ShouldNotBeNullOrEmpty();
        selfLink.ShouldStartWith(_fixture._config.BaseUrl + "/Observation?");
        foreach (string searchPart in search.Split('&'))
        {
            selfLink.ShouldContain(searchPart);
        }
    }
}

/// <summary>Test R4B Patient searches.</summary>
public class R4BTestsPatient : IClassFixture<R4BTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4BTests _fixture;

    /// <summary>Initializes a new instance of the <see cref="R4BTestsPatient"/> class.</summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">The test output helper.</param>
    public R4BTestsPatient(R4BTests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData(null, "_id:not=example", (R4BTests._patientCount - 1))]
    [InlineData(null, "_id=AnIdThatDoesNotExist", 0)]
    [InlineData(null, "_id=example", 1)]
    [InlineData(null, "_id=example&_revinclude=Observation:patient", 1, (R4BTests._observationsWithSubjectExample + 1))]
    [InlineData(null, "name=peter", 1)]
    [InlineData(null, "name=not-present,another-not-present", 0)]
    [InlineData(null, "name=peter,not-present", 1)]
    [InlineData(null, "name=not-present,peter", 1)]
    [InlineData(null, "name:contains=eter", 1)]
    [InlineData(null, "name:contains=zzrot", 0)]
    [InlineData(null, "name:exact=Peter", 1)]
    [InlineData(null, "name:exact=peter", 0)]
    [InlineData(null, "name:exact=Peterish", 0)]
    [InlineData(null, "_profile:missing=true", R4BTests._patientCount)]
    [InlineData(null, "_profile:missing=false", 0)]
    [InlineData(null, "multiplebirth=3", 1)]
    [InlineData(null, "multiplebirth=le3", 1)]
    [InlineData(null, "multiplebirth=lt3", 0)]
    [InlineData(null, "birthdate=1982-01-23", 1)]
    [InlineData(null, "birthdate=1982-01", 1)]
    [InlineData(null, "birthdate=1982", 2)]
    [InlineData(null, "gender=InvalidValue", 0)]
    [InlineData(null, "gender=male", R4BTests._patientsMale)]
    [InlineData(null, "gender=female", R4BTests._patientsFemale)]
    [InlineData(null, "gender=male,female", (R4BTests._patientsMale + R4BTests._patientsFemale))]
    [InlineData(null, "name-use=official", R4BTests._patientCount)]
    [InlineData(null, "name-use=invalid-name-use", 0)]
    [InlineData(null, "identifier=urn:oid:1.2.36.146.595.217.0.1|12345", 1)]
    [InlineData(null, "identifier=|12345", 1)]
    [InlineData(null, "identifier=urn:oid:1.2.36.146.595.217.0.1|ValueThatDoesNotExist", 0)]
    [InlineData(null, "active=true", R4BTests._patientCount)]
    [InlineData(null, "active=false", 0)]
    [InlineData(null, "active=garbage", 0)]
    [InlineData(null, "telecom=phone|(03) 5555 6473", 1)]
    [InlineData(null, "telecom=|(03) 5555 6473", 1)]
    [InlineData(null, "telecom=phone|", 1)]
    [InlineData(null, "_id=example&name=peter", 1)]
    [InlineData(null, "_id=example&name=not-present", 0)]
    [InlineData(null, "_id=example&_profile:missing=false", 0)]
    [InlineData(null, "_has:Observation:patient:_id=blood-pressure", 1)]
    [InlineData(null, "_has:Observation:subject:_id=blood-pressure", 1)]
    [InlineData("PatientExampleFull", "_id=example", 1)]
    [InlineData("PatientExampleFull", "_id:missing=false", 1)]
    [InlineData("PatientExampleFull", "_id:not=example", 0)]
    [InlineData("PatientExampleFull", "_id=AnIdThatDoesNotExist", 0)]
    [InlineData("PatientExampleFull", "_id=example&_revinclude=Observation:patient", 1, (R4BTests._observationsWithSubjectExample + 1))]
    [InlineData("PatientExampleFull", "name=peter", 1)]
    [InlineData("PatientExampleFull", "name=not-present,another-not-present", 0)]
    [InlineData("PatientExampleFull", "name=peter,not-present", 1)]
    [InlineData("PatientExampleFull", "name=not-present,peter", 1)]
    [InlineData("PatientExampleFull", "name:contains=eter", 1)]
    [InlineData("PatientExampleFull", "name:contains=zzrot", 0)]
    [InlineData("PatientExampleFull", "name:exact=Peter", 1)]
    [InlineData("PatientExampleFull", "name:exact=peter", 0)]
    [InlineData("PatientExampleFull", "name:exact=Peterish", 0)]
    [InlineData("PatientExampleFull", "_profile:missing=true", 1)]
    [InlineData("PatientExampleFull", "_profile:missing=false", 0)]
    [InlineData("PatientExampleFull", "_has:Observation:patient:_id=blood-pressure", 1)]
    [InlineData("PatientExampleFull", "_has:Observation:subject:_id=blood-pressure", 1)]
    [InlineData("PatientDoesNotExistFull", "_id=example", 0)]
    [InlineData("PatientDoesNotExistFull", "_id:missing=false", 0)]
    [InlineData("PatientDoesNotExistFull", "_id:not=example", 0)]
    [InlineData("PatientDoesNotExistFull", "_id=AnIdThatDoesNotExist", 0)]
    [InlineData("PatientDoesNotExistFull", "_id=example&_revinclude=Observation:patient", 0)]
    [InlineData("PatientDoesNotExistFull", "name=peter", 0)]
    [InlineData("PatientDoesNotExistFull", "name=not-present,another-not-present", 0)]
    [InlineData("PatientDoesNotExistFull", "name=peter,not-present", 0)]
    [InlineData("PatientDoesNotExistFull", "name=not-present,peter", 0)]
    [InlineData("PatientDoesNotExistFull", "name:contains=eter", 0)]
    [InlineData("PatientDoesNotExistFull", "name:contains=zzrot", 0)]
    [InlineData("PatientDoesNotExistFull", "name:exact=Peter", 0)]
    [InlineData("PatientDoesNotExistFull", "name:exact=peter", 0)]
    [InlineData("PatientDoesNotExistFull", "name:exact=Peterish", 0)]
    [InlineData("PatientDoesNotExistFull", "_profile:missing=true", 0)]
    [InlineData("PatientDoesNotExistFull", "_profile:missing=false", 0)]
    [InlineData("PatientDoesNotExistFull", "_has:Observation:patient:_id=blood-pressure", 0)]
    [InlineData("PatientDoesNotExistFull", "_has:Observation:subject:_id=blood-pressure", 0)]

    public void PatientSearch(string? authKey, string search, int matchCount, int? entryCount = null)
    {
        AuthorizationInfo? authInfo = null;

        if ((authKey != null) &&
            !_fixture._authorizations.TryGetValue(authKey, out authInfo))
        {
            throw new InvalidOperationException($"Authorization key '{authKey}' not found");
        }

        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "GET",
            Url = _fixture._store.Config.BaseUrl + "/Patient?" + search,
            Forwarded = null,
            Authorization = authInfo,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        bool success = _fixture._store.TypeSearch(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(response.SerializedResource);

        results.ShouldNotBeNull();
        results!.Total.ShouldBe(matchCount);
        if (entryCount != null)
        {
            results!.Entries.ShouldHaveCount((int)entryCount);
        }

        results!.Links.ShouldNotBeNullOrEmpty();
        string selfLink = results!.Links!.Where(l => l.Relation.Equals("self"))?.Select(l => l.Url).First() ?? string.Empty;
        selfLink.ShouldNotBeNullOrEmpty();
        selfLink.ShouldStartWith(_fixture._config.BaseUrl + "/Patient?");
        foreach (string searchPart in search.Split('&'))
        {
            selfLink.ShouldContain(searchPart);
        }

        //_testOutputHelper.WriteLine(bundle);


        ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = _fixture._store.Config.BaseUrl + "/Patient/_search",
            Forwarded = null,
            Authorization = authInfo,
            SourceContent = search,
            SourceFormat = "application/x-www-form-urlencoded",
            DestinationFormat = "application/fhir+json",
        };

        success = _fixture._store.TypeSearch(
            ctx,
            out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();

        results = JsonSerializer.Deserialize<MinimalBundle>(response.SerializedResource);

        results.ShouldNotBeNull();
        results!.Total.ShouldBe(matchCount);
        if (entryCount != null)
        {
            results!.Entries.ShouldHaveCount((int)entryCount);
        }

        results!.Links.ShouldNotBeNullOrEmpty();
        selfLink = results!.Links!.Where(l => l.Relation.Equals("self"))?.Select(l => l.Url).First() ?? string.Empty;
        selfLink.ShouldNotBeNullOrEmpty();
        selfLink.ShouldStartWith(_fixture._config.BaseUrl + "/Patient?");
        foreach (string searchPart in search.Split('&'))
        {
            selfLink.ShouldContain(searchPart);
        }
    }


    [Theory]
    [InlineData(null, "example", null, null, R4BTests._observationsWithSubjectExample)]
    [InlineData(null, "example", "Observation", null, R4BTests._observationsWithSubjectExample)]
    [InlineData(null, "example", "Observation", "_id=blood-pressure", 1)]
    [InlineData(null, "example", "Observation", "_id=656", 0)]
    [InlineData(null, "not-a-patient", null, null, 0)]
    [InlineData(null, "not-a-patient", "Observation", null, 0)]
    [InlineData(null, "not-a-patient", "Observation", "_id=blood-pressure", 0)]
    [InlineData("PatientExampleFull", "example", null, null, R4BTests._observationsWithSubjectExample)]
    [InlineData("PatientExampleFull", "example", "Observation", null, R4BTests._observationsWithSubjectExample)]
    [InlineData("PatientExampleFull", "example", "Observation", "_id=blood-pressure", 1)]
    [InlineData("PatientExampleFull", "example", "Observation", "_id=656", 0)]
    [InlineData("PatientDoesNotExistFull", "example", null, null, 0)]
    [InlineData("PatientDoesNotExistFull", "example", "Observation", null, 0)]
    [InlineData("PatientDoesNotExistFull", "example", "Observation", "_id=blood-pressure", 0)]
    [InlineData("PatientDoesNotExistFull", "example", "Observation", "_id=656", 0)]
    public void PatientCompartmentSearch(string? authKey, string id, string? resourceType, string? search, int matchCount, int? entryCount = null)
    {
        AuthorizationInfo? authInfo = null;

        if ((authKey != null) &&
            !_fixture._authorizations.TryGetValue(authKey, out authInfo))
        {
            throw new InvalidOperationException($"Authorization key '{authKey}' not found");
        }

        FhirResponseContext response;
        string getUrl;
        string postUrl;

        bool hasSearchParams = search != null;

        if (resourceType == null)
        {
            getUrl = hasSearchParams
                ? $"{_fixture._store.Config.BaseUrl}/Patient/{id}/*?{search}"
                : $"{_fixture._store.Config.BaseUrl}/Patient/{id}/*";

            postUrl = $"{_fixture._store.Config.BaseUrl}/Patient/{id}/_search";
        }
        else
        {
            getUrl = hasSearchParams
                ? $"{_fixture._store.Config.BaseUrl}/Patient/{id}/{resourceType}?{search}"
                : $"{_fixture._store.Config.BaseUrl}/Patient/{id}/{resourceType}";

            postUrl = $"{_fixture._store.Config.BaseUrl}/Patient/{id}/{resourceType}/_search";
        }

        search ??= string.Empty;

        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "GET",
            Url = getUrl,
            Forwarded = null,
            Authorization = authInfo,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        bool success = resourceType == null
            ? _fixture._store.CompartmentSearch(ctx, out response)
            : _fixture._store.CompartmentTypeSearch(ctx, out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(response.SerializedResource);

        results.ShouldNotBeNull();
        results!.Total.ShouldBe(matchCount);
        if (entryCount != null)
        {
            results!.Entries.ShouldHaveCount((int)entryCount);
        }

        results!.Links.ShouldNotBeNullOrEmpty();
        string selfLink = results!.Links!.Where(l => l.Relation.Equals("self"))?.Select(l => l.Url).First() ?? string.Empty;
        selfLink.ShouldNotBeNullOrEmpty();
        selfLink.ShouldStartWith(getUrl);
        foreach (string searchPart in search.Split('&'))
        {
            selfLink.ShouldContain(searchPart);
        }

        //_testOutputHelper.WriteLine(bundle);

        ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = postUrl,
            Forwarded = null,
            Authorization = authInfo,
            SourceContent = search,
            SourceFormat = "application/x-www-form-urlencoded",
            DestinationFormat = "application/fhir+json",
        };

        success = resourceType == null
            ? _fixture._store.CompartmentSearch(ctx, out response)
            : _fixture._store.CompartmentTypeSearch(ctx, out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();

        results = JsonSerializer.Deserialize<MinimalBundle>(response.SerializedResource);

        results.ShouldNotBeNull();
        results!.Total.ShouldBe(matchCount);
        if (entryCount != null)
        {
            results!.Entries.ShouldHaveCount((int)entryCount);
        }

        results!.Links.ShouldNotBeNullOrEmpty();
        selfLink = results!.Links!.Where(l => l.Relation.Equals("self"))?.Select(l => l.Url).First() ?? string.Empty;
        selfLink.ShouldNotBeNullOrEmpty();
        selfLink.ShouldStartWith(getUrl);
        foreach (string searchPart in search.Split('&'))
        {
            selfLink.ShouldContain(searchPart);
        }
    }
}

/// <summary>A test subscription internals.</summary>
public class R4BTestSubscriptions : IClassFixture<R4BTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4BTests _fixture;

    /// <summary>
    /// Initializes a new instance of the fhir.candle.Tests.TestSubscriptionInternals class.
    /// </summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">(Immutable) The test output helper.</param>
    public R4BTestSubscriptions(R4BTests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>Parse topic.</summary>
    /// <param name="json">The JSON.</param>
    [Theory]
    [FileData("data/r4b/SubscriptionTopic-encounter-complete.json")]
    [FileData("data/r4b/SubscriptionTopic-encounter-complete-qualified.json")]
    public void ParseTopic(string json)
    {
        HttpStatusCode sc = candleR4B.FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
            json,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? r,
            out _);

        sc.ShouldBe(HttpStatusCode.OK);
        r.ShouldNotBeNull();
        r!.TypeName.ShouldBe("SubscriptionTopic");
        candleR4B.FhirCandle.Subscriptions.TopicConverter converter = new candleR4B.FhirCandle.Subscriptions.TopicConverter();

        bool success = converter.TryParse(r, out ParsedSubscriptionTopic s);

        success.ShouldBeTrue();
        s.ShouldNotBeNull();
        s.Id.ShouldBe("encounter-complete");
        s.Url.ShouldBe("http://example.org/FHIR/SubscriptionTopic/encounter-complete");
        s.ResourceTriggers.ShouldHaveCount(1);
        s.ResourceTriggers.Keys.ShouldContain("Encounter");
        s.EventTriggers.ShouldBeEmpty();
        s.AllowedFilters.ShouldNotBeEmpty();
        s.AllowedFilters.Keys.ShouldContain("Encounter");
        s.NotificationShapes.ShouldNotBeEmpty();
        s.NotificationShapes.Keys.ShouldContain("Encounter");
    }

    [Theory]
    [FileData("data/r4b/Subscription-encounter-complete.json")]
    public void ParseSubscription(string json)
    {
        HttpStatusCode sc = candleR4B.FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
            json,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? r,
            out _);

        sc.ShouldBe(HttpStatusCode.OK);
        r.ShouldNotBeNull();
        r!.TypeName.ShouldBe("Subscription");
        candleR4B.FhirCandle.Subscriptions.SubscriptionConverter converter = new candleR4B.FhirCandle.Subscriptions.SubscriptionConverter(10);

        bool success = converter.TryParse(r, out ParsedSubscription s);

        success.ShouldBeTrue();
        s.ShouldNotBeNull();
        s.Id.ShouldBe("db4ce0bb-fa9c-4092-9f75-34772dc85590");
        s.TopicUrl.ShouldBe("http://example.org/FHIR/SubscriptionTopic/encounter-complete");
        s.Filters.ShouldHaveCount(1);
        s.Filters.Keys.ShouldContain("Encounter");
        s.ChannelCode.ShouldBe("rest-hook");
        s.Endpoint.ShouldBe("https://subscriptions.argo.run/fhir/r4b/$subscription-hook");
        s.HeartbeatSeconds.ShouldBe(120);
        s.TimeoutSeconds.ShouldBeNull();
        s.ContentType.ShouldBe("application/fhir+json");
        s.ContentLevel.ShouldBe("id-only");
        s.CurrentStatus.ShouldBe("active");
    }

    [Theory]
    [FileData("data/r4b/Bundle-notification-handshake.json")]
    public void ParseHandshake(string json)
    {
        HttpStatusCode sc = candleR4B.FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
            json,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? r,
            out _);

        sc.ShouldBe(HttpStatusCode.OK);
        r.ShouldNotBeNull();
        r!.TypeName.ShouldBe("Bundle");

        ParsedSubscriptionStatus? s = ((VersionedFhirStore)_fixture._store).ParseNotificationBundle((Hl7.Fhir.Model.Bundle)r);

        s.ShouldNotBeNull();
        s!.BundleId.ShouldBe("24dd1ba8-d569-418f-96d8-e304433f9424");
        s.SubscriptionReference.ShouldBe("https://subscriptions.argo.run/fhir/r4b/Subscription/db4ce0bb-fa9c-4092-9f75-34772dc85590");
        s.SubscriptionTopicCanonical.ShouldBe("http://example.org/FHIR/SubscriptionTopic/encounter-complete");
        s.Status.ShouldBe("active");
        s.NotificationType.ShouldBe(ParsedSubscription.NotificationTypeCodes.Handshake);
        s.NotificationEvents.ShouldBeEmpty();
        s.Errors.ShouldBeEmpty();
    }

    /// <summary>Tests an encounter subscription with no filters.</summary>
    /// <param name="fpCriteria">  The criteria.</param>
    /// <param name="onCreate">    True to on create.</param>
    /// <param name="createResult">True to create result.</param>
    /// <param name="onUpdate">    True to on update.</param>
    /// <param name="updateResult">True to update result.</param>
    /// <param name="onDelete">    True to on delete.</param>
    /// <param name="deleteResult">True to delete the result.</param>
    [Theory]
    [InlineData("(%previous.empty() or (%previous.status != 'finished')) and (%current.status = 'finished')", true, true, true, true, false, false)]
    [InlineData("(%previous.empty() | (%previous.status != 'finished')) and (%current.status = 'finished')", true, true, true, true, false, false)]
    [InlineData("(%previous.id.empty() or (%previous.status != 'finished')) and (%current.status = 'finished')", true, true, true, true, false, false)]
    [InlineData("(%previous.id.empty() | (%previous.status != 'finished')) and (%current.status = 'finished')", true, true, true, true, false, false)]
    public void TestSubEncounterNoFilters(
        string fpCriteria,
        bool onCreate,
        bool createResult,
        bool onUpdate,
        bool updateResult,
        bool onDelete,
        bool deleteResult)
    {
        VersionedFhirStore store = ((VersionedFhirStore)_fixture._store);
        ResourceStore<coreR4B.Hl7.Fhir.Model.Encounter> rs = (ResourceStore<coreR4B.Hl7.Fhir.Model.Encounter>)_fixture._store["Encounter"];

        string resourceType = "Encounter";
        string topicId = "test-topic";
        string topicUrl = "http://example.org/FHIR/TestTopic";
        string subId = "test-subscription";

        ParsedSubscriptionTopic topic = new()
        {
            Id = topicId,
            Url = topicUrl,
            ResourceTriggers = new()
            {
                {
                    resourceType,
                    new List<ParsedSubscriptionTopic.ResourceTrigger>()
                    {
                        new ParsedSubscriptionTopic.ResourceTrigger()
                        {
                            ResourceType = resourceType,
                            OnCreate = onCreate,
                            OnUpdate = onUpdate,
                            OnDelete = onDelete,
                            QueryPrevious = string.Empty,
                            CreateAutoPass = false,
                            CreateAutoFail = false,
                            QueryCurrent = string.Empty,
                            DeleteAutoPass = false,
                            DeleteAutoFail = false,
                            FhirPathCriteria = fpCriteria,
                        }
                    }
                },
            },
        };

        ParsedSubscription subscription = new()
        {
            Id = subId,
            TopicUrl = topicUrl,
            Filters = new()
            {
                { resourceType, new List<ParsedSubscription.SubscriptionFilter>() },
            },
            ExpirationTicks = DateTime.Now.AddMinutes(10).Ticks,
            ChannelSystem = string.Empty,
            ChannelCode = "rest-hook",
            ContentType = "application/fhir+json",
            ContentLevel = "full-resource",
            CurrentStatus = "active",
        };

        store.StoreProcessSubscriptionTopic(topic, false);
        store.StoreProcessSubscription(subscription, false);

        coreR4B.Hl7.Fhir.Model.Encounter previous = new()
        {
            Id = "object-under-test",
            Status = coreR4B.Hl7.Fhir.Model.Encounter.EncounterStatus.Planned,
        };
        coreR4B.Hl7.Fhir.Model.Encounter current = new()
        {
            Id = "object-under-test",
            Status = coreR4B.Hl7.Fhir.Model.Encounter.EncounterStatus.Finished,
        };

        // test create current
        if (onCreate)
        {
            rs.TestCreateAgainstSubscriptions(current);

            subscription.NotificationErrors.ShouldBeEmpty("Create test should not have errors");

            if (createResult)
            {
                subscription.GeneratedEvents.ShouldNotBeEmpty("Create test should have generated event");
                subscription.GeneratedEvents.ShouldHaveCount(1);
            }
            else
            {
                subscription.GeneratedEvents.ShouldBeEmpty("Create test should NOT have generated event");
            }

            subscription.ClearEvents();
        }

        // test update previous to current
        if (onUpdate)
        {
            rs.TestUpdateAgainstSubscriptions(current, previous);

            subscription.NotificationErrors.ShouldBeEmpty("Update test should not have errors");

            if (updateResult)
            {
                subscription.GeneratedEvents.ShouldNotBeEmpty("Update test should have generated event");
                subscription.GeneratedEvents.ShouldHaveCount(1);
            }
            else
            {
                subscription.GeneratedEvents.ShouldBeEmpty("Update test should NOT have generated event");
            }

            subscription.ClearEvents();
        }

        // test delete previous
        if (onDelete)
        {
            rs.TestDeleteAgainstSubscriptions(previous);
            subscription.NotificationErrors.ShouldBeEmpty("Delete test should not have errors");

            if (deleteResult)
            {
                subscription.GeneratedEvents.ShouldNotBeEmpty("Delete test should have generated event");
                subscription.GeneratedEvents.ShouldHaveCount(1);
            }
            else
            {
                subscription.GeneratedEvents.ShouldBeEmpty("Delete test should NOT have generated event");
            }

            subscription.ClearEvents();
        }
    }
}

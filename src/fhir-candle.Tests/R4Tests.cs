// <copyright file="FhirStoreTestsR4Resource.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

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

/// <summary>Unit tests for FHIR R4.</summary>
public class R4Tests
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
    internal const int _patientCount = 6;

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

    /// <summary>Initializes a new instance of the <see cref="R4Tests"/> class.</summary>
    public R4Tests()
    {
        string path = Path.GetRelativePath(Directory.GetCurrentDirectory(), "data/r4");
        DirectoryInfo? loadDirectory = null;

        if (Directory.Exists(path))
        {
            loadDirectory = new DirectoryInfo(path);
        }

        _config = new()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R4,
            ControllerName = "r4",
            BaseUrl = "http://localhost/fhir/r4",
            LoadDirectory = loadDirectory,
            AllowExistingId = true,
            AllowCreateAsUpdate = true,
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

/// <summary>Test R4 patient looped.</summary>
public class R4TestsPatientLooped : IClassFixture<R4Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4Tests _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="R4TestsPatientLooped"/> class.
    /// </summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">(Immutable) The test output helper.</param>
    public R4TestsPatientLooped(R4Tests fixture, ITestOutputHelper testOutputHelper)
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

/// <summary>Test R4 Observation searches.</summary>
public class R4TestsObservation : IClassFixture<R4Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4Tests _fixture;

    /// <summary>Initializes a new instance of the <see cref="R4TestsObservation"/> class.</summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">The test output helper.</param>
    public R4TestsObservation(R4Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData(null, "_id:not=example", (R4Tests._observationCount - 1))]
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
    [InlineData(null, "value-quantity=84.1|http://unitsofmeasure.org|[kg]", 0)]       // TODO: test unit conversion
    [InlineData(null, "value-quantity=820|urn:iso:std:iso:11073:10101|265201", 1)]
    [InlineData(null, "value-quantity=820|urn:iso:std:iso:11073:10101|cL/s", 1)]
    [InlineData(null, "value-quantity=820|urn:iso:std:iso:11073:10101|cl/s", 1)]
    [InlineData(null, "value-quantity=820||265201", 1)]
    [InlineData(null, "value-quantity=820||cL/s", 1)]
    [InlineData(null, "subject=Patient/example", R4Tests._observationsWithSubjectExample)]
    [InlineData(null, "subject:Patient=Patient/example", R5Tests._observationsWithSubjectExample)]
    [InlineData(null, "subject:Device=Patient/example", 0)]
    [InlineData(null, "subject=Patient/UnknownPatientId", 0)]
    [InlineData(null, "subject=example", R4Tests._observationsWithSubjectExample)]
    [InlineData(null, "code=http://loinc.org|9272-6", 1)]
    [InlineData(null, "code=http://snomed.info/sct|169895004", 1)]
    [InlineData(null, "code=http://snomed.info/sct|9272-6", 0)]
    [InlineData(null, "_profile=http://hl7.org/fhir/StructureDefinition/vitalsigns", R4Tests._observationsVitalSigns)]
    [InlineData(null, "_profile:missing=true", (R4Tests._observationCount - R4Tests._observationsVitalSigns))]
    [InlineData(null, "_profile:missing=false", R4Tests._observationsVitalSigns)]
    [InlineData(null, "subject.name=peter", R4Tests._observationsWithSubjectExample)]
    [InlineData(null, "subject:Patient.name=peter", R4Tests._observationsWithSubjectExample)]
    [InlineData(null, "subject._id=example", R4Tests._observationsWithSubjectExample)]
    [InlineData(null, "subject:Patient._id=example", R4Tests._observationsWithSubjectExample)]
    [InlineData(null, "subject._id=example&_include=Observation:patient", R4Tests._observationsWithSubjectExample, R4Tests._observationsWithSubjectExample + 1)]
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


        //_testOutputHelper.WriteLine(bundle);
    }
}

/// <summary>Test R4 Patient searches.</summary>
public class R4TestsPatient : IClassFixture<R4Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4Tests _fixture;

    /// <summary>Initializes a new instance of the <see cref="R4TestsPatient"/> class.</summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">The test output helper.</param>
    public R4TestsPatient(R4Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData(null, "_id:not=example", (R4Tests._patientCount - 1))]
    [InlineData(null, "_id=AnIdThatDoesNotExist", 0)]
    [InlineData(null, "_id=example", 1)]
    [InlineData(null, "_id=example&_revinclude=Observation:patient", 1, (R4Tests._observationsWithSubjectExample + 1))]
    [InlineData(null, "name=peter", 1)]
    [InlineData(null, "name=not-present,another-not-present", 0)]
    [InlineData(null, "name=peter,not-present", 1)]
    [InlineData(null, "name=not-present,peter", 1)]
    [InlineData(null, "name:contains=eter", 1)]
    [InlineData(null, "name:contains=zzrot", 0)]
    [InlineData(null, "name:exact=Peter", 1)]
    [InlineData(null, "name:exact=peter", 0)]
    [InlineData(null, "name:exact=Peterish", 0)]
    [InlineData(null, "_profile:missing=true", R4Tests._patientCount - 1)]
    [InlineData(null, "_profile:missing=false", 1)]
    [InlineData(null, "multiplebirth=3", 1)]
    [InlineData(null, "multiplebirth=le3", 1)]
    [InlineData(null, "multiplebirth=lt3", 0)]
    [InlineData(null, "birthdate=1982-01-23", 1)]
    [InlineData(null, "birthdate=1982-01", 1)]
    [InlineData(null, "birthdate=1982", 2)]
    [InlineData(null, "gender=InvalidValue", 0)]
    [InlineData(null, "gender=male", R4Tests._patientsMale)]
    [InlineData(null, "gender=female", R4Tests._patientsFemale)]
    [InlineData(null, "gender=male,female", (R4Tests._patientsMale + R4Tests._patientsFemale))]
    [InlineData(null, "name-use=official", R4Tests._patientCount - 1)]
    [InlineData(null, "name-use=invalid-name-use", 0)]
    [InlineData(null, "identifier=urn:oid:1.2.36.146.595.217.0.1|12345", 1)]
    [InlineData(null, "identifier=|12345", 1)]
    [InlineData(null, "identifier=urn:oid:1.2.36.146.595.217.0.1|ValueThatDoesNotExist", 0)]
    [InlineData(null, "identifier:of-type=http://terminology.hl7.org/CodeSystem/v2-0203|MR|12345", 1)]
    [InlineData(null, "identifier:of-type=http://terminology.hl7.org/CodeSystem/v2-0203|EXT|12345", 0)]
    [InlineData(null, "identifier:of-type=http://terminology.hl7.org/CodeSystem/v2-0203|MR|ABC", 0)]
    [InlineData(null, "active=true", R4Tests._patientCount - 1)]
    [InlineData(null, "active=false", 0)]
    [InlineData(null, "active:missing=true", 1)]
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
    [InlineData("PatientExampleFull", "_id=example&_revinclude=Observation:patient", 1, (R4Tests._observationsWithSubjectExample + 1))]
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
    [InlineData(null, "example", null, null, R4Tests._observationsWithSubjectExample)]
    [InlineData(null, "example", "Observation", null, R4Tests._observationsWithSubjectExample)]
    [InlineData(null, "example", "Observation", "_id=blood-pressure", 1)]
    [InlineData(null, "example", "Observation", "_id=656", 0)]
    [InlineData(null, "not-a-patient", null, null, 0)]
    [InlineData(null, "not-a-patient", "Observation", null, 0)]
    [InlineData(null, "not-a-patient", "Observation", "_id=blood-pressure", 0)]
    [InlineData("PatientExampleFull", "example", null, null, R4Tests._observationsWithSubjectExample)]
    [InlineData("PatientExampleFull", "example", "Observation", null, R4Tests._observationsWithSubjectExample)]
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


/// <summary>A 4 test conditionals.</summary>
public class R4TestConditionals : IClassFixture<R4Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    /// <summary>Information describing the conditional.</summary>
    public static IEnumerable<object[]> ConditionalData;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4Tests _fixture;

    /// <summary>
    /// Initializes a new instance of the fhir.candle.Tests.TestSubscriptionInternals class.
    /// </summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">(Immutable) The test output helper.</param>
    public R4TestConditionals(R4Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>
    /// Initializes static members of the fhir.candle.Tests.R4TestConditionals class.
    /// </summary>
    static R4TestConditionals()
    {
        ConditionalData = new List<object[]>()
        {
            new object[] { "Patient", GetContents("data/r4/patient-example.json") },
        };
    }

    /// <summary>Gets the contents.</summary>
    /// <exception cref="ArgumentException">Thrown when one or more arguments have unsupported or
    ///  illegal values.</exception>
    /// <param name="filePath">Full pathname of the file.</param>
    /// <returns>The contents.</returns>
    private static string GetContents(string filePath)
    {
        // Get the absolute path to the file
        string path = Path.IsPathRooted(filePath)
            ? filePath
            : Path.GetRelativePath(Directory.GetCurrentDirectory(), filePath);

        if (!File.Exists(path))
        {
            throw new ArgumentException($"Could not find file at path: {path}");
        }

        return File.ReadAllText(path);
    }

    /// <summary>Change identifier.</summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    /// <exception cref="ArgumentException">    Thrown when one or more arguments have unsupported or
    ///  illegal values.</exception>
    /// <param name="json">The JSON.</param>
    /// <param name="id">  The identifier.</param>
    /// <returns>A string.</returns>
    private static string ChangeId(string json, string id)
    {
        if (string.IsNullOrEmpty(json))
        {
            throw new ArgumentNullException(nameof(json));
        }

        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentNullException(nameof(id));
        }

        HttpStatusCode sc = candleR4.FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
            json,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? r,
            out _);

        if (sc != HttpStatusCode.OK)
        {
            throw new ArgumentException($"Could not deserialize json: {json}");
        }

        if (r == null)
        {
            throw new ArgumentException($"Could not deserialize json: {json}");
        }

        r.Id = id;

        return candleR4.FhirCandle.Serialization.SerializationUtils.SerializeFhir(r, "application/fhir+json", false);
    }

    /// <summary>Conditional create no match.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="json">        The JSON.</param>
    [Theory]
    [MemberData(nameof(ConditionalData))]
    public void ConditionalCreateNoMatch(string resourceType, string json)
    {
        string id = Guid.NewGuid().ToString();

        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = $"{_fixture._store.Config.BaseUrl}/{resourceType}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = ChangeId(json, id),
            DestinationFormat = "application/fhir+json",
            IfNoneExist = "_id=" + id,
        };

        // test conditional that has no matches
        bool success = _fixture._store.InstanceCreate(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"1\"");
        response.LastModified.ShouldNotBeNullOrEmpty();
        response.Location.ShouldContain($"{resourceType}/{id}");

        HttpStatusCode sc = candleR4.FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
            response.SerializedResource,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? r,
            out _);

        sc.ShouldBe(HttpStatusCode.OK);
        r.ShouldNotBeNull();
        r!.TypeName.ShouldBe(resourceType);
        r!.Id.ShouldBe(id);
    }

    /// <summary>Conditional create one match.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="json">        The JSON.</param>
    [Theory]
    [MemberData(nameof(ConditionalData))]
    public void ConditionalCreateOneMatch(string resourceType, string json)
    {
        string id = Guid.NewGuid().ToString();

        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = $"{_fixture._store.Config.BaseUrl}/{resourceType}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = ChangeId(json, id),
            DestinationFormat = "application/fhir+json",
        };

        // first, store our resource
        bool success = _fixture._store.InstanceCreate(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"1\"");
        response.LastModified.ShouldNotBeNullOrEmpty();
        response.Location.ShouldContain($"{resourceType}/{id}");

        ctx = ctx with
        {
            IfNoneExist = "_id=" + id,
        };

        // now, store it conditionally with a single match
        success = _fixture._store.InstanceCreate(
            ctx,
            out response);

        // all contents should match original - not a new version
        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"1\"");
        response.LastModified.ShouldNotBeNullOrEmpty();
        response.Location.ShouldContain($"{resourceType}/{id}");

        HttpStatusCode sc = candleR4.FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
            response.SerializedResource,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? r,
            out _);

        sc.ShouldBe(HttpStatusCode.OK);
        r.ShouldNotBeNull();
        r!.TypeName.ShouldBe(resourceType);
        r!.Id.ShouldBe(id);
    }

    /// <summary>Conditional create multiple matches.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="json">        The JSON.</param>
    [Theory]
    [MemberData(nameof(ConditionalData))]
    public void ConditionalCreateMultipleMatches(string resourceType, string json)
    {
        string id1 = Guid.NewGuid().ToString();
        string id2 = Guid.NewGuid().ToString();
        string id3 = Guid.NewGuid().ToString();

        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = $"{_fixture._store.Config.BaseUrl}/{resourceType}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = ChangeId(json, id1),
            DestinationFormat = "application/fhir+json",
        };

        // first, store our resource
        bool success = _fixture._store.InstanceCreate(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"1\"");
        response.LastModified.ShouldNotBeNullOrEmpty();
        response.Location.ShouldContain($"{resourceType}/{id1}");

        HttpStatusCode sc = candleR4.FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
            response.SerializedResource,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? r,
            out _);

        sc.ShouldBe(HttpStatusCode.OK);
        r.ShouldNotBeNull();
        r!.TypeName.ShouldBe(resourceType);
        r!.Id.ShouldBe(id1);

        ctx = ctx with
        {
            SourceContent = ChangeId(json, id2),
        };

        // now store the second resource
        success = _fixture._store.InstanceCreate(
            ctx,
            out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"1\"");
        response.LastModified.ShouldNotBeNullOrEmpty();
        response.Location.ShouldContain($"{resourceType}/{id2}");

        ctx = ctx with
        {
            SourceContent = ChangeId(json, id3),
            IfNoneExist = $"_id={id1},{id2}",
        };

        // now attempt to store with a conditional create that matches both
        success = _fixture._store.InstanceCreate(
            ctx,
            out response);

        // this should fail
        success.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        response.SerializedResource.ShouldBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBeNullOrEmpty();
        response.LastModified.ShouldBeNullOrEmpty();
        response.Location.ShouldBeNullOrEmpty();
    }
}

/// <summary>A test subscription internals.</summary>
public class R4TestSubscriptions : IClassFixture<R4Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4Tests _fixture;

    /// <summary>
    /// Initializes a new instance of the fhir.candle.Tests.TestSubscriptionInternals class.
    /// </summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">(Immutable) The test output helper.</param>
    public R4TestSubscriptions(R4Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>Parse topic.</summary>
    /// <param name="json">The JSON.</param>
    [Theory]
    [FileData("data/r4/Basic-topic-encounter-complete-qualified.json")]
    [FileData("data/r4/Basic-topic-encounter-complete.json")]
    public void ParseTopic(string json)
    {
        HttpStatusCode sc = candleR4.FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
            json,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? r,
            out _);

        sc.ShouldBe(HttpStatusCode.OK);
        r.ShouldNotBeNull();
        r!.TypeName.ShouldBe("Basic");

        candleR4.FhirCandle.Subscriptions.TopicConverter converter = new candleR4.FhirCandle.Subscriptions.TopicConverter();

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
    [FileData("data/r4/Subscription-encounter-complete.json")]
    public void ParseSubscription(string json)
    {
        HttpStatusCode sc = candleR4.FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
            json,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? r,
            out _);

        sc.ShouldBe(HttpStatusCode.OK);
        r.ShouldNotBeNull();
        r!.TypeName.ShouldBe("Subscription");

        candleR4.FhirCandle.Subscriptions.SubscriptionConverter converter = new candleR4.FhirCandle.Subscriptions.SubscriptionConverter(10);

        bool success = converter.TryParse(r, out ParsedSubscription s);

        success.ShouldBeTrue();
        s.ShouldNotBeNull();
        s.Id.ShouldBe("383c610b-8a8b-4173-b363-7b811509aadd");
        s.TopicUrl.ShouldBe("http://example.org/FHIR/SubscriptionTopic/encounter-complete");
        s.Filters.ShouldHaveCount(1);
        s.ChannelCode.ShouldBe("rest-hook");
        s.Endpoint.ShouldBe("https://subscriptions.argo.run/fhir/r4/$subscription-hook");
        s.HeartbeatSeconds.ShouldBe(120);
        s.TimeoutSeconds.ShouldBeNull();
        s.ContentType.ShouldBe("application/fhir+json");
        s.ContentLevel.ShouldBe("id-only");
        s.CurrentStatus.ShouldBe("active");
    }

    [Theory]
    [FileData("data/r4/Bundle-notification-handshake.json")]
    public void ParseHandshake(string json)
    {
        HttpStatusCode sc = candleR4.FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
            json,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? r,
            out _);

        sc.ShouldBe(HttpStatusCode.OK);
        r.ShouldNotBeNull();
        r!.TypeName.ShouldBe("Bundle");

        ParsedSubscriptionStatus? s = ((VersionedFhirStore)_fixture._store).ParseNotificationBundle((Hl7.Fhir.Model.Bundle)r);

        s.ShouldNotBeNull();
        s!.BundleId.ShouldBe("64578ab3-2bf6-497a-a873-7c29fa2090d6");
        s.SubscriptionReference.ShouldBe("https://subscriptions.argo.run/fhir/r4/Subscription/383c610b-8a8b-4173-b363-7b811509aadd");
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
        ResourceStore<coreR4.Hl7.Fhir.Model.Encounter> rs = (ResourceStore<coreR4.Hl7.Fhir.Model.Encounter>)_fixture._store["Encounter"];

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

        coreR4.Hl7.Fhir.Model.Encounter previous = new()
        {
            Id = "object-under-test",
            Status = coreR4.Hl7.Fhir.Model.Encounter.EncounterStatus.Planned,
        };
        coreR4.Hl7.Fhir.Model.Encounter current = new()
        {
            Id = "object-under-test",
            Status = coreR4.Hl7.Fhir.Model.Encounter.EncounterStatus.Finished,
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


/// <summary>Summary flags in R4 searches.</summary>
public class R4TestsSummary : IClassFixture<R4Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4Tests _fixture;

    /// <summary>Initializes a new instance of the <see cref="R4TestsPatient"/> class.</summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">The test output helper.</param>
    public R4TestsSummary(R4Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    private readonly record struct SearchResultInfo(
        string SummaryFlag,
        bool Success,
        int? Total,
        int MatchCount,
        int EntryCount,
        int ResponseLength,
        string SelfLink);

    private static string[] _summaryFlags = ["false", "true", "text", "data", "count"];

    [Theory]
    [InlineData("/Patient?_id:not=example", (R4Tests._patientCount - 1))]
    [InlineData("/Patient?_id=AnIdThatDoesNotExist", 0)]
    [InlineData("/Patient?_id=example", 1)]
    [InlineData("/Patient?name=peter", 1)]
    [InlineData("/Patient?name=not-present,another-not-present", 0)]
    [InlineData("/Patient?name=peter,not-present", 1)]
    [InlineData("/Patient?name=not-present,peter", 1)]
    [InlineData("/Patient?name:contains=eter", 1)]
    [InlineData("/Patient?name:contains=zzrot", 0)]
    [InlineData("/Patient?name:exact=Peter", 1)]
    [InlineData("/Patient?name:exact=peter", 0)]
    [InlineData("/Patient?name:exact=Peterish", 0)]
    [InlineData("/Patient?_profile:missing=true", R4Tests._patientCount - 1)]
    [InlineData("/Patient?_profile:missing=false", 1)]
    [InlineData("/Patient?multiplebirth=3", 1)]
    [InlineData("/Patient?multiplebirth=le3", 1)]
    [InlineData("/Patient?multiplebirth=lt3", 0)]
    [InlineData("/Patient?birthdate=1982-01-23", 1)]
    [InlineData("/Patient?birthdate=1982-01", 1)]
    [InlineData("/Patient?birthdate=1982", 2)]
    [InlineData("/Patient?gender=InvalidValue", 0)]
    [InlineData("/Patient?gender=male", R4Tests._patientsMale)]
    [InlineData("/Patient?gender=female", R4Tests._patientsFemale)]
    [InlineData("/Patient?gender=male,female", (R4Tests._patientsMale + R4Tests._patientsFemale))]
    [InlineData("/Patient?name-use=official", R4Tests._patientCount - 1)]
    [InlineData("/Patient?name-use=invalid-name-use", 0)]
    [InlineData("/Patient?identifier=urn:oid:1.2.36.146.595.217.0.1|12345", 1)]
    [InlineData("/Patient?identifier=|12345", 1)]
    [InlineData("/Patient?identifier=urn:oid:1.2.36.146.595.217.0.1|ValueThatDoesNotExist", 0)]
    [InlineData("/Patient?identifier:of-type=http://terminology.hl7.org/CodeSystem/v2-0203|MR|12345", 1)]
    [InlineData("/Patient?identifier:of-type=http://terminology.hl7.org/CodeSystem/v2-0203|EXT|12345", 0)]
    [InlineData("/Patient?identifier:of-type=http://terminology.hl7.org/CodeSystem/v2-0203|MR|ABC", 0)]
    [InlineData("/Patient?active=true", R4Tests._patientCount - 1)]
    [InlineData("/Patient?active=false", 0)]
    [InlineData("/Patient?active:missing=true", 1)]
    [InlineData("/Patient?active=garbage", 0)]
    [InlineData("/Patient?telecom=phone|(03) 5555 6473", 1)]
    [InlineData("/Patient?telecom=|(03) 5555 6473", 1)]
    [InlineData("/Patient?telecom=phone|", 1)]
    [InlineData("/Patient?_id=example&name=peter", 1)]
    [InlineData("/Patient?_id=example&name=not-present", 0)]
    [InlineData("/Patient?_id=example&_profile:missing=false", 0)]
    [InlineData("/Patient?_has:Observation:patient:_id=blood-pressure", 1)]
    [InlineData("/Patient?_has:Observation:subject:_id=blood-pressure", 1)]
    [InlineData("?_type=Patient", R4Tests._patientCount)]
    [InlineData("?_type=Patient,Observation", R4Tests._patientCount + R4Tests._observationCount)]
    [InlineData("/Patient/example/Observation?patient=example", R4Tests._observationsWithSubjectExample)]
    public void Search(string search, int matchCount)
    {
        Dictionary<string, SearchResultInfo> summaryResults = _summaryFlags.Select(sf => doSearch(sf)).ToDictionary(s => s.SummaryFlag);

        // first, make sure everything worked
        foreach ((string summaryFlag, SearchResultInfo sri) in summaryResults)
        {
            sri.Success.ShouldBeTrue($"Search with _summary={summaryFlag} failed");
            sri.ResponseLength.ShouldBeGreaterThan(0, $"Search with _summary={summaryFlag} returned no data");
        }

        // false should have the longest response
        summaryResults["false"].ResponseLength.ShouldBe(summaryResults.Values.Max(sri => sri.ResponseLength));

        // count should have no entries
        summaryResults["count"].EntryCount.ShouldBe(0);

        // total should match for all
        foreach ((string summaryFlag, SearchResultInfo sri) in summaryResults)
        {
            sri.Total.ShouldBe(matchCount, $"Search with _summary={summaryFlag} returned wrong total");
        }

        // all self links should include the summary flag
        foreach ((string summaryFlag, SearchResultInfo sri) in summaryResults)
        {
            sri.SelfLink.Contains($"_summary={summaryFlag}").ShouldBeTrue($"Search with _summary={summaryFlag} returned incorrect self link");
        }

        return;

        SearchResultInfo doSearch(string summaryFlag)
        {
            FhirRequestContext ctx = new()
            {
                TenantName = _fixture._store.Config.ControllerName,
                Store = _fixture._store,
                HttpMethod = "GET",
                Url = _fixture._store.Config.BaseUrl + search + "&_summary=" + summaryFlag,
                Forwarded = null,
                Authorization = null,
                SourceFormat = "application/fhir+json",
                DestinationFormat = "application/fhir+json",
            };

            FhirResponseContext response;
            bool success = ctx.Interaction switch
            {
                StoreInteractionCodes.CompartmentSearch => _fixture._store.CompartmentSearch(ctx, out response),
                StoreInteractionCodes.CompartmentTypeSearch => _fixture._store.CompartmentTypeSearch(ctx, out response),
                StoreInteractionCodes.TypeSearch => _fixture._store.TypeSearch(ctx, out response),
                StoreInteractionCodes.SystemSearch => _fixture._store.SystemSearch(ctx, out response),
                _ => _fixture._store.TypeSearch(ctx, out response)
            };

            MinimalBundle? results = null;

            if (success)
            {
                results = JsonSerializer.Deserialize<MinimalBundle>(response.SerializedResource);
            }

            return new()
            {
                SummaryFlag = summaryFlag,
                Success = success,
                Total = results?.Total,
                MatchCount = results?.Entries?.Count(e => e.Search?.Mode == "match") ?? 0,
                EntryCount = results?.Entries?.Count() ?? 0,
                ResponseLength = response.SerializedResource?.Length ?? 0,
                SelfLink = results?.Links?.Where(l => l.Relation.Equals("self"))?.Select(l => l.Url).First() ?? string.Empty,
            };

        }
    }
}

// /// <summary>A 4 test transactions.</summary>
// public class R4TestTransactions: IClassFixture<R4Tests>
// {
//     /// <summary>(Immutable) The test output helper.</summary>
//     private readonly ITestOutputHelper _testOutputHelper;

//     /// <summary>Gets the configurations.</summary>
//     public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

//     /// <summary>(Immutable) The fixture.</summary>
//     private readonly R4Tests _fixture;

//     /// <summary>
//     /// Initializes a new instance of the fhir.candle.Tests.TestSubscriptionInternals class.
//     /// </summary>
//     /// <param name="fixture">         (Immutable) The fixture.</param>
//     /// <param name="testOutputHelper">(Immutable) The test output helper.</param>
//     public R4TestTransactions(R4Tests fixture, ITestOutputHelper testOutputHelper)
//     {
//         _fixture = fixture;
//         _testOutputHelper = testOutputHelper;
//     }

//     /// <summary>Parse topic.</summary>
//     /// <param name="json">The JSON.</param>
//     [Theory]
//     // [FileData("data/r4/Bundle-transaction-cdex-load-payer.json")]
//     [FileData("data/r4/Bundle-pas-test-claim-01.json")]
//     public void ProcessBundle(string json)
//     {
//        HttpStatusCode sc = _fixture._store.ProcessBundle(
//            json,
//            "application/fhir+json",
//            "application/fhir+json",
//            false,
//            out string serializedResource,
//            out string serializedOutcome);

//        sc.ShouldBe(HttpStatusCode.OK);
//        serializedResource.ShouldNotBeNullOrEmpty();
//        serializedOutcome.ShouldNotBeNullOrEmpty();

//        sc = candleR4.FhirCandle.Serialization.Utils.TryDeserializeFhir(
//            serializedResource,
//            "application/fhir+json",
//            out Hl7.Fhir.Model.Resource? r,
//            out _);

//        sc.ShouldBe(HttpStatusCode.OK);
//        r.ShouldNotBeNull();
//        r!.TypeName.ShouldBe("Bundle");
//     }
// }

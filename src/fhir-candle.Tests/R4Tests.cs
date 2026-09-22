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
    // Phase 2: date prefix boundary tests against effectiveDateTime (2012-09-17 x3, 2016-03-28, 2016-05-18T22:33:22Z, 2017-05-03T15:54:26-04:00)
    [InlineData(null, "date=ge2016", 3)]
    [InlineData(null, "date=lt2016", 3)]
    [InlineData(null, "date=ge2017", 1)]
    // Phase 3: precision-aware ap window against effectiveDateTime
    [InlineData(null, "date=ap2016", 3)]
    [InlineData(null, "date=ap2012-09-17", 3)]
    [InlineData(null, "date=ap2030", 0)]
    // Phase 6 (M4): offset-aware date normalization. effectiveDateTime
    // 2017-05-03T15:54:26-04:00 equals UTC 2017-05-03T19:54:26Z; reverting
    // the ToUniversalTime() normalization would shift these counts.
    [InlineData(null, "date=ge2017-05-03T19:54:26Z", 1)]
    [InlineData(null, "date=lt2017-05-03T19:54:26Z", 5)]
    // Phase 6 (M6): instant-boundary gt/ge semantics at an observation's instant
    // (2016-05-18T22:33:22Z). gt excludes the boundary observation, ge includes it.
    [InlineData(null, "date=gt2016-05-18T22:33:22Z", 1)]
    [InlineData(null, "date=ge2016-05-18T22:33:22Z", 2)]
    [InlineData("PatientExampleFull", "subject=Patient/example", R4Tests._observationsWithSubjectExample)]
    [InlineData("PatientDoesNotExistFull", "subject=Patient/example", 0)]
    [InlineData("PatientExamplePatientOnly", "subject=Patient/example", 0)]
    public void ObservationSearch(string? authKey, string search, int matchCount, int? entryCount = null)
    {
        AuthorizationInfo? authInfo = null;

        if ((authKey is not null) &&
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
        if (entryCount is not null)
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
        if (entryCount is not null)
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

    [Theory]
    [InlineData("_sort=date")]
    [InlineData("_sort=-date")]
    [InlineData("_sort=status")]
    [InlineData("_sort=-status")]
    [InlineData("_sort=_lastUpdated")]
    [InlineData("_sort=-_lastUpdated")]
    public void ObservationSortOrder(string search)
    {
        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "GET",
            Url = _fixture._store.Config.BaseUrl + "/Observation?" + search,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        bool success = _fixture._store.TypeSearch(ctx, out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(
            response.SerializedResource);
        results.ShouldNotBeNull();
        results.Entries.ShouldNotBeNull();

        // Verify self link includes _sort
        string selfLink = results.Links!.First(l => l.Relation == "self").Url;
        selfLink.ShouldContain("_sort=");

        bool descending = search.StartsWith("_sort=-", StringComparison.OrdinalIgnoreCase);

        string? lastSortValue = null;
        string? currentSortValue = null;
        int nullCount = 0;
        int valuedCount = 0;

        // traverse the bundle and check behavior based on sort key and direction
        foreach (MinimalBundle.MinimalEntry entry in results!.Entries!)
        {
            if ((entry.Search?.Mode != "match") ||
                (entry.Resource is null))
            {
                continue;
            }

            switch (search)
            {
                case "_sort=date":
                case "_sort=-date":
                    currentSortValue = entry.Resource.EffectiveDateTime;
                    break;
                case "_sort=status":
                case "_sort=-status":
                    currentSortValue = entry.Resource.Status?.ToString();
                    break;
                case "_sort=_lastUpdated":
                case "_sort=-_lastUpdated":
                    currentSortValue = entry.Resource.Meta?.LastUpdated;
                    break;
            }

            // check sort consistency
            if (descending)
            {
                // should not have a non-null value after a null value
                if ((currentSortValue is null) && (nullCount > 0))
                {
                    lastSortValue.ShouldBeNull("NULL values should be last");
                }

                // if we have a current and a previous value, the current should be equal or lower
                if ((currentSortValue is not null) &&
                    (lastSortValue is not null))
                {
                    currentSortValue.CompareTo(lastSortValue).ShouldBeLessThanOrEqualTo(0);
                }
            }
            else
            {
                // should not have a non-null value after a null value
                if ((currentSortValue is null) && (nullCount > 0))
                {
                    lastSortValue.ShouldBeNull("NULL values should be last");
                }

                // if we have a current and a previous value, the current should be higher
                if ((currentSortValue is not null) &&
                    (lastSortValue is not null))
                {
                    currentSortValue.CompareTo(lastSortValue).ShouldBeGreaterThanOrEqualTo(0);
                }
            }

            if (currentSortValue is null)
            {
                nullCount++;
            }
            else
            {
                valuedCount++;
            }

            lastSortValue = currentSortValue;
        }
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
    [InlineData(null, "_id=example&_id=example", 1)]
    [InlineData(null, "_id=example&_id=AnIdThatDoesNotExist", 0)]
    [InlineData(null, "_id=example&_revinclude=Observation:patient", 1, (R4Tests._observationsWithSubjectExample + 1))]
    [InlineData(null, "name=peter", 1)]
    [InlineData(null, "name=not-present,another-not-present", 0)]
    [InlineData(null, "name=peter,not-present", 1)]
    [InlineData(null, "name=not-present,peter", 1)]
    [InlineData(null, "name:contains=eter", 1)]
    [InlineData(null, "name:contains=zzrot", 0)]
    [InlineData(null, "name:contains=eter&name:contains=zzrot", 0)]
    [InlineData(null, "name:contains=ete&name:contains=ter", 1)]
    [InlineData(null, "name:contains=zzrot,ete&name:contains=ter", 1)]
    [InlineData(null, "name:exact=Peter", 1)]
    [InlineData(null, "name:exact=peter", 0)]
    [InlineData(null, "name:exact=Peterish", 0)]
    // Phase 8 (M9): a bare combining mark folds to the empty string; with FoldedValues
    // null-sentinel, evaluators skip it and the search must NOT match every patient
    // (which is what a literal StartsWith("") / Contains("") would do).
    [InlineData(null, "name=\u0301", 0)]
    [InlineData(null, "name:contains=\u0301", 0)]
    // Phase 4: accent-insensitive string search (pat1 has a second name with family=Muñoz)
    [InlineData(null, "family=munoz", 1)]
    [InlineData(null, "family=MUÑOZ", 1)]
    [InlineData(null, "family:contains=unoz", 1)]
    [InlineData(null, "family:exact=Muñoz", 1)]
    [InlineData(null, "family:exact=Munoz", 0)]
    [InlineData(null, "family:exact=muñoz", 0)]
    [InlineData(null, "_profile:missing=true", R4Tests._patientCount - 1)]
    [InlineData(null, "_profile:missing=false", 1)]
    [InlineData(null, "multiplebirth=3", 1)]
    [InlineData(null, "multiplebirth=le3", 1)]
    [InlineData(null, "multiplebirth=lt3", 0)]
    [InlineData(null, "birthdate=1982-01-23", 1)]
    [InlineData(null, "birthdate=1982-01", 1)]
    [InlineData(null, "birthdate=1982", 2)]
    [InlineData(null, "birthdate:missing=true", 2)]
    [InlineData(null, "birthdate:missing=false", R4Tests._patientCount - 2)]
    // Phase 2: date prefix boundary inclusivity (R4 birthdates: 1974-12-25, 1982-01-23, 1982-08-02, 1987-02-20)
    [InlineData(null, "birthdate=ge1982", 3)]
    [InlineData(null, "birthdate=gt1982", 1)]
    [InlineData(null, "birthdate=lt1982", 1)]
    [InlineData(null, "birthdate=le1982", 3)]
    [InlineData(null, "birthdate=ne1982", 2)]
    [InlineData(null, "birthdate=ge1982-01-23", 3)]
    [InlineData(null, "birthdate=le1982-08-02", 3)]
    [InlineData(null, "birthdate=lt1982-01-23", 1)]
    [InlineData(null, "birthdate=gt1982-08-02", 1)]
    // Phase 3: precision-aware ap window
    [InlineData(null, "birthdate=ap1982", 2)]
    [InlineData(null, "birthdate=ap1987", 1)]
    [InlineData(null, "birthdate=ap1983", 2)]
    [InlineData(null, "birthdate=ap1990", 0)]
    [InlineData(null, "birthdate=ap1982-01", 1)]
    [InlineData(null, "birthdate=ap1982-01-23", 1)]
    [InlineData(null, "gender=InvalidValue", 0)]
    [InlineData(null, "gender=male", R4Tests._patientsMale)]
    [InlineData(null, "gender=female", R4Tests._patientsFemale)]
    [InlineData(null, "gender=male,female", (R4Tests._patientsMale + R4Tests._patientsFemale))]
    // Phase 5: :not modifier matches resources where the element is absent
    [InlineData(null, "gender:not=male", R4Tests._patientCount - R4Tests._patientsMale)]
    [InlineData(null, "gender:not=female", R4Tests._patientCount - R4Tests._patientsFemale)]
    // Phase 6: repeated parameter AND semantics (regression)
    [InlineData(null, "birthdate=ge1980&birthdate=le1990", 3)]
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

        if ((authKey is not null) &&
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
        if (entryCount is not null)
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
        if (entryCount is not null)
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

    /// <summary>
    /// Phase 9 (L5) — leap-Feb safety-margin pin. Inline-creates a Patient with
    /// birthDate 2016-01-28 and queries birthdate=ap2016-04. The 65-day window
    /// includes that birthDate (2016-04-01 minus 65 days = 2016-01-27); a 62-day
    /// window would exclude it (2016-04-01 minus 62 days = 2016-01-30). Pins the
    /// L5 widen-by-3-days change against an inadvertent revert.
    /// </summary>
    [Fact]
    public void PatientSearchApYYYYMMWindowIncludesSafetyMargin()
    {
        string newId = $"l5-margin-r4-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        string newPatientJson = "{\"resourceType\":\"Patient\",\"id\":\"" + newId + "\"," +
            "\"name\":[{\"family\":\"L5Margin\"}],\"birthDate\":\"2016-01-28\"}";

        FhirRequestContext createCtx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = _fixture._store.Config.BaseUrl + "/Patient",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = newPatientJson,
            DestinationFormat = "application/fhir+json",
        };

        try
        {
            _fixture._store.InstanceCreate(createCtx, out FhirResponseContext _, forceAllowExistingId: true)
                .ShouldBeTrue();

            FhirRequestContext searchCtx = new()
            {
                TenantName = _fixture._store.Config.ControllerName,
                Store = _fixture._store,
                HttpMethod = "GET",
                Url = _fixture._store.Config.BaseUrl + "/Patient?birthdate=ap2016-04",
                Forwarded = null,
                Authorization = null,
                SourceFormat = "application/fhir+json",
                DestinationFormat = "application/fhir+json",
            };

            _fixture._store.TypeSearch(searchCtx, out FhirResponseContext response).ShouldBeTrue();
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.SerializedResource.ShouldContain(newId, Case.Sensitive,
                "expected birthdate=ap2016-04 to include patient born 2016-01-28 under the 65-day window");
        }
        finally
        {
            FhirRequestContext deleteCtx = new()
            {
                TenantName = _fixture._store.Config.ControllerName,
                Store = _fixture._store,
                HttpMethod = "DELETE",
                Url = _fixture._store.Config.BaseUrl + $"/Patient/{newId}",
                Forwarded = null,
                Authorization = null,
                ResourceType = "Patient",
                Id = newId,
                SourceFormat = "application/fhir+json",
                DestinationFormat = "application/fhir+json",
            };
            _fixture._store.InstanceDelete(deleteCtx, out FhirResponseContext _);
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

        if ((authKey is not null) &&
            !_fixture._authorizations.TryGetValue(authKey, out authInfo))
        {
            throw new InvalidOperationException($"Authorization key '{authKey}' not found");
        }

        FhirResponseContext response;
        string getUrl;
        string postUrl;

        bool hasSearchParams = search is not null;

        if (resourceType is null)
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

        bool success = resourceType is null
            ? _fixture._store.CompartmentSearch(ctx, out response)
            : _fixture._store.CompartmentTypeSearch(ctx, out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(response.SerializedResource);

        results.ShouldNotBeNull();
        results!.Total.ShouldBe(matchCount);
        if (entryCount is not null)
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

        success = resourceType is null
            ? _fixture._store.CompartmentSearch(ctx, out response)
            : _fixture._store.CompartmentTypeSearch(ctx, out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();

        results = JsonSerializer.Deserialize<MinimalBundle>(response.SerializedResource);

        results.ShouldNotBeNull();
        results!.Total.ShouldBe(matchCount);
        if (entryCount is not null)
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

    [Theory]
    [InlineData("_sort=birthdate")]
    [InlineData("_sort=-birthdate")]
    [InlineData("_sort=_lastUpdated")]
    [InlineData("_sort=-_lastUpdated")]
    public void PatientSortOrder(string search)
    {
        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "GET",
            Url = _fixture._store.Config.BaseUrl + "/Patient?" + search,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        bool success = _fixture._store.TypeSearch(ctx, out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(
            response.SerializedResource);
        results.ShouldNotBeNull();
        results.Entries.ShouldNotBeNull();

        // Verify self link includes _sort
        string selfLink = results.Links!.First(l => l.Relation == "self").Url;
        selfLink.ShouldContain("_sort=");

        bool descending = search.StartsWith("_sort=-", StringComparison.OrdinalIgnoreCase);

        string? lastSortValue = null;
        string? currentSortValue = null;
        int nullCount = 0;
        int valuedCount = 0;

        // traverse the bundle and check behavior based on sort key and direction
        foreach (MinimalBundle.MinimalEntry entry in results!.Entries!)
        {
            if ((entry.Search?.Mode != "match") ||
                (entry.Resource is null))
            {
                continue;
            }

            switch (search)
            {
                case "_sort=birthdate":
                case "_sort=-birthdate":
                    currentSortValue = entry.Resource.BirthDate;
                    break;
                case "_sort=_lastUpdated":
                case "_sort=-_lastUpdated":
                    currentSortValue = entry.Resource.Meta?.LastUpdated;
                    break;
            }

            // check sort consistency
            if (descending)
            {
                // should not have a non-null value after a null value
                if ((currentSortValue is null) && (nullCount > 0))
                {
                    lastSortValue.ShouldBeNull("NULL values should be last");
                }

                // if we have a current and a previous value, the current should be equal or lower
                if ((currentSortValue is not null) &&
                    (lastSortValue is not null))
                {
                    currentSortValue.CompareTo(lastSortValue).ShouldBeLessThanOrEqualTo(0, $"{lastSortValue} should be >= {currentSortValue}");
                }
            }
            else
            {
                // should not have a non-null value after a null value
                if ((currentSortValue is null) && (nullCount > 0))
                {
                    lastSortValue.ShouldBeNull("NULL values should be last");
                }

                // if we have a current and a previous value, the current should be higher
                if ((currentSortValue is not null) &&
                    (lastSortValue is not null))
                {
                    currentSortValue.CompareTo(lastSortValue).ShouldBeGreaterThanOrEqualTo(0, $"{lastSortValue} should be <= {currentSortValue}");
                }
            }

            if (currentSortValue is null)
            {
                nullCount++;
            }
            else
            {
                valuedCount++;
            }

            lastSortValue = currentSortValue;
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

        if (r is null)
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
            out FhirResponseContext response,
            forceAllowExistingId: true);

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
            out FhirResponseContext response,
            forceAllowExistingId: true);

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
            out response,
            forceAllowExistingId: true);

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
            out FhirResponseContext response,
            forceAllowExistingId: true);

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
            out response,
            forceAllowExistingId: true);

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
            out response,
            forceAllowExistingId: true);

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

/// <summary>Tests for conditional update operations.</summary>
public class R4TestConditionalUpdates : IClassFixture<R4Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Information describing the conditional.</summary>
    public static IEnumerable<object[]> ConditionalData;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4Tests _fixture;

    /// <summary>
    /// Initializes a new instance of the R4TestConditionalUpdates class.
    /// </summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">(Immutable) The test output helper.</param>
    public R4TestConditionalUpdates(R4Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>
    /// Initializes static members of the R4TestConditionalUpdates class.
    /// </summary>
    static R4TestConditionalUpdates()
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

        if (r is null)
        {
            throw new ArgumentException($"Could not deserialize json: {json}");
        }

        r.Id = id;

        return candleR4.FhirCandle.Serialization.SerializationUtils.SerializeFhir(r, "application/fhir+json", false);
    }

    /// <summary>Conditional update with no match creates the resource.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="json">        The JSON.</param>
    [Theory]
    [MemberData(nameof(ConditionalData))]
    public void ConditionalUpdateNoMatch(string resourceType, string json)
    {
        string id = Guid.NewGuid().ToString();

        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "PUT",
            Url = $"{_fixture._store.Config.BaseUrl}/{resourceType}?_id={id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = ChangeId(json, id),
            DestinationFormat = "application/fhir+json",
        };

        bool success = _fixture._store.InstanceUpdate(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"1\"");
        response.LastModified.ShouldNotBeNullOrEmpty();
        response.Location.ShouldContain($"{resourceType}/{id}");
    }

    /// <summary>Conditional update with one match updates the resource.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="json">        The JSON.</param>
    [Theory]
    [MemberData(nameof(ConditionalData))]
    public void ConditionalUpdateOneMatch(string resourceType, string json)
    {
        string id = Guid.NewGuid().ToString();

        // first, create the resource
        FhirRequestContext createCtx = new()
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

        bool success = _fixture._store.InstanceCreate(
            createCtx,
            out FhirResponseContext createResp,
            forceAllowExistingId: true);

        success.ShouldBeTrue();
        createResp.StatusCode.ShouldBe(HttpStatusCode.Created);

        // now, conditional update with search matching that resource
        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "PUT",
            Url = $"{_fixture._store.Config.BaseUrl}/{resourceType}?_id={id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = ChangeId(json, id),
            DestinationFormat = "application/fhir+json",
        };

        success = _fixture._store.InstanceUpdate(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"2\"");
        response.LastModified.ShouldNotBeNullOrEmpty();
        response.Location.ShouldContain($"{resourceType}/{id}");
    }

    /// <summary>Conditional update with multiple matches returns 412.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="json">        The JSON.</param>
    [Theory]
    [MemberData(nameof(ConditionalData))]
    public void ConditionalUpdateMultipleMatches(string resourceType, string json)
    {
        string id1 = Guid.NewGuid().ToString();
        string id2 = Guid.NewGuid().ToString();

        // create two resources
        FhirRequestContext createCtx1 = new()
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

        bool success = _fixture._store.InstanceCreate(
            createCtx1,
            out FhirResponseContext _,
            forceAllowExistingId: true);

        success.ShouldBeTrue();

        FhirRequestContext createCtx2 = createCtx1 with
        {
            SourceContent = ChangeId(json, id2),
        };

        success = _fixture._store.InstanceCreate(
            createCtx2,
            out FhirResponseContext _,
            forceAllowExistingId: true);

        success.ShouldBeTrue();

        // conditional update matching both
        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "PUT",
            Url = $"{_fixture._store.Config.BaseUrl}/{resourceType}?_id={id1},{id2}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = ChangeId(json, id1),
            DestinationFormat = "application/fhir+json",
        };

        success = _fixture._store.InstanceUpdate(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
    }

    /// <summary>Conditional update with one match and matching body id succeeds.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="json">        The JSON.</param>
    [Theory]
    [MemberData(nameof(ConditionalData))]
    public void ConditionalUpdateOneMatchIdInBody(string resourceType, string json)
    {
        string id = Guid.NewGuid().ToString();

        // create the resource
        FhirRequestContext createCtx = new()
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

        bool success = _fixture._store.InstanceCreate(
            createCtx,
            out FhirResponseContext _,
            forceAllowExistingId: true);

        success.ShouldBeTrue();

        // conditional update with body id matching the matched resource
        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "PUT",
            Url = $"{_fixture._store.Config.BaseUrl}/{resourceType}?_id={id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = ChangeId(json, id),
            DestinationFormat = "application/fhir+json",
        };

        success = _fixture._store.InstanceUpdate(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"2\"");
        response.Location.ShouldContain($"{resourceType}/{id}");
    }

    /// <summary>Conditional update with one match and mismatched body id returns 412.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="json">        The JSON.</param>
    [Theory]
    [MemberData(nameof(ConditionalData))]
    public void ConditionalUpdateOneMatchIdMismatch(string resourceType, string json)
    {
        string id = Guid.NewGuid().ToString();
        string wrongId = Guid.NewGuid().ToString();

        // create the resource
        FhirRequestContext createCtx = new()
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

        bool success = _fixture._store.InstanceCreate(
            createCtx,
            out FhirResponseContext _,
            forceAllowExistingId: true);

        success.ShouldBeTrue();

        // conditional update with id in URL path that mismatches the search result
        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "PUT",
            Url = $"{_fixture._store.Config.BaseUrl}/{resourceType}/{wrongId}?_id={id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = ChangeId(json, wrongId),
            DestinationFormat = "application/fhir+json",
        };

        success = _fixture._store.InstanceUpdate(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
    }
}

/// <summary>Tests for conditional create via URL query operations.</summary>
public class R4TestConditionalCreateViaUrlQuery : IClassFixture<R4Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Information describing the conditional.</summary>
    public static IEnumerable<object[]> ConditionalData;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4Tests _fixture;

    /// <summary>
    /// Initializes a new instance of the R4TestConditionalCreateViaUrlQuery class.
    /// </summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">(Immutable) The test output helper.</param>
    public R4TestConditionalCreateViaUrlQuery(R4Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>
    /// Initializes static members of the R4TestConditionalCreateViaUrlQuery class.
    /// </summary>
    static R4TestConditionalCreateViaUrlQuery()
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

        if (r is null)
        {
            throw new ArgumentException($"Could not deserialize json: {json}");
        }

        r.Id = id;

        return candleR4.FhirCandle.Serialization.SerializationUtils.SerializeFhir(r, "application/fhir+json", false);
    }

    /// <summary>Conditional create via URL query with no match creates the resource.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="json">        The JSON.</param>
    [Theory]
    [MemberData(nameof(ConditionalData))]
    public void ConditionalCreateViaUrlQueryNoMatch(string resourceType, string json)
    {
        string id = Guid.NewGuid().ToString();

        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = $"{_fixture._store.Config.BaseUrl}/{resourceType}?_id={id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = ChangeId(json, id),
            DestinationFormat = "application/fhir+json",
        };

        bool success = _fixture._store.InstanceCreate(
            ctx,
            out FhirResponseContext response,
            forceAllowExistingId: true);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"1\"");
        response.LastModified.ShouldNotBeNullOrEmpty();
        response.Location.ShouldContain($"{resourceType}/{id}");
    }

    /// <summary>Conditional create via URL query with one match returns existing.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="json">        The JSON.</param>
    [Theory]
    [MemberData(nameof(ConditionalData))]
    public void ConditionalCreateViaUrlQueryOneMatch(string resourceType, string json)
    {
        string id = Guid.NewGuid().ToString();

        // first, create the resource
        FhirRequestContext createCtx = new()
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

        bool success = _fixture._store.InstanceCreate(
            createCtx,
            out FhirResponseContext createResp,
            forceAllowExistingId: true);

        success.ShouldBeTrue();
        createResp.StatusCode.ShouldBe(HttpStatusCode.Created);

        // now attempt conditional create via URL query (no IfNoneExist)
        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = $"{_fixture._store.Config.BaseUrl}/{resourceType}?_id={id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = ChangeId(json, id),
            DestinationFormat = "application/fhir+json",
        };

        success = _fixture._store.InstanceCreate(
            ctx,
            out FhirResponseContext response,
            forceAllowExistingId: true);

        // should return the existing resource
        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"1\"");
        response.LastModified.ShouldNotBeNullOrEmpty();
        response.Location.ShouldContain($"{resourceType}/{id}");
    }

    /// <summary>Conditional create via URL query with multiple matches returns 412.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="json">        The JSON.</param>
    [Theory]
    [MemberData(nameof(ConditionalData))]
    public void ConditionalCreateViaUrlQueryMultipleMatches(string resourceType, string json)
    {
        string id1 = Guid.NewGuid().ToString();
        string id2 = Guid.NewGuid().ToString();
        string id3 = Guid.NewGuid().ToString();

        // create two resources
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

        bool success = _fixture._store.InstanceCreate(
            ctx,
            out FhirResponseContext _,
            forceAllowExistingId: true);

        success.ShouldBeTrue();

        ctx = ctx with
        {
            SourceContent = ChangeId(json, id2),
        };

        success = _fixture._store.InstanceCreate(
            ctx,
            out FhirResponseContext _,
            forceAllowExistingId: true);

        success.ShouldBeTrue();

        // conditional create via URL query matching both
        ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = $"{_fixture._store.Config.BaseUrl}/{resourceType}?_id={id1},{id2}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = ChangeId(json, id3),
            DestinationFormat = "application/fhir+json",
        };

        success = _fixture._store.InstanceCreate(
            ctx,
            out FhirResponseContext response,
            forceAllowExistingId: true);

        success.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
    }

    /// <summary>IfNoneExist takes precedence over URL query.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="json">        The JSON.</param>
    [Theory]
    [MemberData(nameof(ConditionalData))]
    public void ConditionalCreateViaUrlQueryWithIfNoneExist(string resourceType, string json)
    {
        string existingId = Guid.NewGuid().ToString();
        string newId = Guid.NewGuid().ToString();

        // create a resource
        FhirRequestContext createCtx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = $"{_fixture._store.Config.BaseUrl}/{resourceType}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = ChangeId(json, existingId),
            DestinationFormat = "application/fhir+json",
        };

        bool success = _fixture._store.InstanceCreate(
            createCtx,
            out FhirResponseContext _,
            forceAllowExistingId: true);

        success.ShouldBeTrue();

        // conditional create with both IfNoneExist (matching existing) and URL query (no match)
        // IfNoneExist should take precedence, returning existing resource
        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = $"{_fixture._store.Config.BaseUrl}/{resourceType}?_id={newId}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = ChangeId(json, newId),
            DestinationFormat = "application/fhir+json",
            IfNoneExist = $"_id={existingId}",
        };

        success = _fixture._store.InstanceCreate(
            ctx,
            out FhirResponseContext response,
            forceAllowExistingId: true);

        // IfNoneExist found a match, so should return the existing resource
        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Location.ShouldContain($"{resourceType}/{existingId}");
    }
}

/// <summary>Tests for bundle conditional operations.</summary>
public class R4TestBundleConditionals : IClassFixture<R4Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4Tests _fixture;

    /// <summary>
    /// Initializes a new instance of the R4TestBundleConditionals class.
    /// </summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">(Immutable) The test output helper.</param>
    public R4TestBundleConditionals(R4Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>Bundle conditional create via URL query - first creates, second returns existing.</summary>
    [Fact]
    public void BundleConditionalCreateViaUrlQuery()
    {
        string id = Guid.NewGuid().ToString();

        string bundleJson = @"{
            ""resourceType"": ""Bundle"",
            ""type"": ""transaction"",
            ""entry"": [
                {
                    ""fullUrl"": ""urn:uuid:" + id + @""",
                    ""resource"": {
                        ""resourceType"": ""Patient"",
                        ""id"": """ + id + @""",
                        ""identifier"": [{""system"": ""http://test"", ""value"": """ + id + @"""}]
                    },
                    ""request"": {
                        ""method"": ""POST"",
                        ""url"": ""Patient?identifier=http://test|" + id + @"""
                    }
                }
            ]
        }";

        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = $"{_fixture._store.Config.BaseUrl}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = bundleJson,
            DestinationFormat = "application/fhir+json",
        };

        // first submission — should create
        bool success = _fixture._store.ProcessBundle(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // second submission — should not duplicate
        ctx = ctx with
        {
            SourceContent = bundleJson,
        };

        success = _fixture._store.ProcessBundle(
            ctx,
            out FhirResponseContext response2);

        success.ShouldBeTrue();
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Bundle conditional update with no prior match creates a resource.</summary>
    [Fact]
    public void BundleConditionalUpdateNoMatch()
    {
        string id = Guid.NewGuid().ToString();

        string bundleJson = @"{
            ""resourceType"": ""Bundle"",
            ""type"": ""transaction"",
            ""entry"": [
                {
                    ""fullUrl"": ""urn:uuid:" + id + @""",
                    ""resource"": {
                        ""resourceType"": ""Patient"",
                        ""id"": """ + id + @""",
                        ""identifier"": [{""system"": ""http://test"", ""value"": """ + id + @"""}]
                    },
                    ""request"": {
                        ""method"": ""PUT"",
                        ""url"": ""Patient?identifier=http://test|" + id + @"""
                    }
                }
            ]
        }";

        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = $"{_fixture._store.Config.BaseUrl}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = bundleJson,
            DestinationFormat = "application/fhir+json",
        };

        bool success = _fixture._store.ProcessBundle(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Bundle conditional update with one prior match updates the resource.</summary>
    [Fact]
    public void BundleConditionalUpdateOneMatch()
    {
        string id = Guid.NewGuid().ToString();

        // first, create a patient directly
        FhirRequestContext createCtx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = $"{_fixture._store.Config.BaseUrl}/Patient",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = @"{
                ""resourceType"": ""Patient"",
                ""id"": """ + id + @""",
                ""identifier"": [{""system"": ""http://test"", ""value"": """ + id + @"""}]
            }",
            DestinationFormat = "application/fhir+json",
        };

        bool success = _fixture._store.InstanceCreate(
            createCtx,
            out FhirResponseContext _,
            forceAllowExistingId: true);

        success.ShouldBeTrue();

        // now submit a bundle with conditional update matching that patient
        string bundleJson = @"{
            ""resourceType"": ""Bundle"",
            ""type"": ""transaction"",
            ""entry"": [
                {
                    ""resource"": {
                        ""resourceType"": ""Patient"",
                        ""id"": """ + id + @""",
                        ""identifier"": [{""system"": ""http://test"", ""value"": """ + id + @"""}],
                        ""name"": [{""family"": ""Updated""}]
                    },
                    ""request"": {
                        ""method"": ""PUT"",
                        ""url"": ""Patient?identifier=http://test|" + id + @"""
                    }
                }
            ]
        }";

        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = $"{_fixture._store.Config.BaseUrl}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = bundleJson,
            DestinationFormat = "application/fhir+json",
        };

        success = _fixture._store.ProcessBundle(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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

    /// <summary>
    /// Regression: posting the same notification Bundle twice through
    /// $subscription-hook must surface the store's conflict response on the
    /// second call and must not register a second received-notification entry
    /// for the subscription.
    /// </summary>
    /// <param name="json">The handshake bundle JSON.</param>
    [Theory]
    [FileData("data/r4/Bundle-notification-handshake.json")]
    public void SubscriptionHookDuplicateBundleCreateFailureReturnsFailure(string json)
    {
        HttpStatusCode parseSc = candleR4.FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
            json,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? parsed,
            out _);

        parseSc.ShouldBe(HttpStatusCode.OK);
        parsed.ShouldNotBeNull();
        parsed!.ShouldBeOfType<Hl7.Fhir.Model.Bundle>();

        Hl7.Fhir.Model.Bundle bundle = (Hl7.Fhir.Model.Bundle)parsed;
        bundle.Id = "hook-dup-handshake";

        VersionedFhirStore versionedStore = (VersionedFhirStore)_fixture._store;

        ParsedSubscriptionStatus? parsedStatus = versionedStore.ParseNotificationBundle(bundle);
        parsedStatus.ShouldNotBeNull();
        string subscriptionReference = parsedStatus!.SubscriptionReference;

        int baselineCount = versionedStore.ReceivedNotifications.TryGetValue(
            subscriptionReference,
            out List<ParsedSubscriptionStatus>? baselineList)
            ? baselineList.Count
            : 0;

        FhirRequestContext ctx1 = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = $"{_fixture._config.BaseUrl}/$subscription-hook",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceObject = bundle,
            DestinationFormat = "application/fhir+json",
        };

        bool firstSuccess = _fixture._store.SystemOperation(ctx1, out FhirResponseContext response1);

        firstSuccess.ShouldBeTrue();
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);

        int countAfterFirst = versionedStore.ReceivedNotifications.TryGetValue(
            subscriptionReference,
            out List<ParsedSubscriptionStatus>? listAfterFirst)
            ? listAfterFirst.Count
            : 0;
        countAfterFirst.ShouldBe(baselineCount + 1);

        FhirRequestContext ctx2 = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "POST",
            Url = $"{_fixture._config.BaseUrl}/$subscription-hook",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceObject = bundle,
            DestinationFormat = "application/fhir+json",
        };

        bool secondSuccess = _fixture._store.SystemOperation(ctx2, out FhirResponseContext response2);

        secondSuccess.ShouldBeFalse();
        response2.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response2.Outcome.ShouldNotBeNull();
        response2.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response2.SerializedOutcome.ShouldContain("Bundle/hook-dup-handshake");
        response2.SerializedOutcome.ShouldContain("POST-base create interaction cannot overwrite existing resources");

        int countAfterSecond = versionedStore.ReceivedNotifications.TryGetValue(
            subscriptionReference,
            out List<ParsedSubscriptionStatus>? listAfterSecond)
            ? listAfterSecond.Count
            : 0;
        countAfterSecond.ShouldBe(countAfterFirst);
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
        HttpStatusCode? Status,
        int? Total,
        int MatchCount,
        int EntryCount,
        string SelfLink,
        JsonElement[] Resources);

    private sealed record SummaryResourceExpectation(JsonElement Input, GeneratedResourceBinding Binding);

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
        SearchResultInfo initial = DoSummarySearch("?_type=Patient,Observation", string.Empty);
        AssertSummarySearchCounts(initial, R4Tests._patientCount + R4Tests._observationCount);
        Dictionary<string, SummaryResourceExpectation> expectedResources = BindSummaryInputs(initial.Resources);
        string generatedPatient = Assert.Single(expectedResources, pair => !pair.Value.Input.TryGetProperty("id", out _)).Key;
        string[] expectedIdentities = ExpectedSummaryIdentities(search, generatedPatient);
        Assert.Equal(matchCount, expectedIdentities.Length);

        Dictionary<string, SearchResultInfo> summaryResults = _summaryFlags
            .Select(summaryFlag => DoSummarySearch(search, summaryFlag)).ToDictionary(result => result.SummaryFlag);

        // Membership is unordered, but each input resource's arrays, values, and metadata are not.
        foreach ((string summaryFlag, SearchResultInfo sri) in summaryResults)
        {
            sri.Success.ShouldBeTrue($"Search with _summary={summaryFlag} failed");
            AssertSummarySearchResult(sri, matchCount, expectedIdentities, expectedResources);
        }

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

        // Neither repeated summaries nor their marker codings may change a subsequent ordinary search/read.
        AssertSummarySearchResult(DoSummarySearch(search, string.Empty), matchCount, expectedIdentities, expectedResources);
        foreach (string identity in expectedIdentities)
        {
            SummaryResourceExpectation expected = expectedResources[identity];
            FhirRequestContext context = new()
            {
                TenantName = _fixture._store.Config.ControllerName,
                Store = _fixture._store,
                HttpMethod = "GET",
                Url = expected.Binding.Location,
                Authorization = null,
                DestinationFormat = "application/fhir+json",
            };
            _fixture._store.InstanceRead(context, out FhirResponseContext response).ShouldBeTrue();
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.MimeType.ShouldBe("application/fhir+json");
            response.ResourceType.ShouldBe(expected.Binding.ResourceType);
            response.Id.ShouldBe(expected.Binding.Id);
            using JsonDocument read = JsonDocument.Parse(response.SerializedResource);
            AssertSummaryProjection(expected.Input, read.RootElement, "false", expected.Binding);
            WireContractAssertions.ResourceHeaders(expected.Binding, response.ETag, response.Location, response.LastModified);
            AssertSummaryOutcome(response.SerializedOutcome);
        }
    }

    private SearchResultInfo DoSummarySearch(string search, string summaryFlag)
    {
        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "GET",
            Url = _fixture._store.Config.BaseUrl + search +
                (string.IsNullOrEmpty(summaryFlag) ? string.Empty : "&_summary=" + summaryFlag),
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

        success.ShouldBeTrue($"Search with _summary={summaryFlag} failed");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.MimeType.ShouldBe("application/fhir+json");
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        AssertSummaryOutcome(response.SerializedOutcome);

        using JsonDocument document = JsonDocument.Parse(response.SerializedResource);
        JsonElement root = document.RootElement;
        Assert.Equal("Bundle", root.GetProperty("resourceType").GetString());
        Assert.Equal("searchset", root.GetProperty("type").GetString());
        bool hasEntries = root.TryGetProperty("entry", out JsonElement entryArray);
        AssertSummaryJsonMembers(root, hasEntries
            ? ["resourceType", "type", "total", "link", "entry"]
            : ["resourceType", "type", "total", "link"]);
        if (summaryFlag == "count")
        {
            Assert.False(hasEntries, "_summary=count must not serialize entries or resource bodies.");
        }

        JsonElement link = Assert.Single(root.GetProperty("link").EnumerateArray());
        AssertSummaryJsonMembers(link, ["relation", "url"]);
        Assert.Equal("self", link.GetProperty("relation").GetString());
        JsonElement[] entries = hasEntries ? entryArray.EnumerateArray().ToArray() : [];
        foreach (JsonElement entry in entries)
        {
            AssertSummaryJsonMembers(entry, ["fullUrl", "resource", "search"]);
            AssertSummaryJsonMembers(entry.GetProperty("search"), ["mode"]);
            string identity = SummaryIdentity(entry.GetProperty("resource"));
            Assert.Equal(_fixture._config.BaseUrl + "/" + identity, entry.GetProperty("fullUrl").GetString());
        }

        JsonElement[] resources = entries.Select(entry => entry.GetProperty("resource").Clone()).ToArray();
        Assert.Equal(resources.Length, resources.Select(SummaryIdentity).Distinct(StringComparer.Ordinal).Count());
        return new(summaryFlag, success, response.StatusCode, root.GetProperty("total").GetInt32(),
            entries.Count(entry => entry.GetProperty("search").GetProperty("mode").GetString() == "match"),
            entries.Length, link.GetProperty("url").GetString()!, resources);
    }

    private static void AssertSummarySearchCounts(SearchResultInfo result, int total)
    {
        result.Success.ShouldBeTrue();
        result.Status.ShouldBe(HttpStatusCode.OK);
        result.Total.ShouldBe(total);
        int entries = result.SummaryFlag == "count" ? 0 : total;
        result.EntryCount.ShouldBe(entries);
        result.MatchCount.ShouldBe(entries);
        Assert.Equal(entries, result.Resources.Length);
        if (string.IsNullOrEmpty(result.SummaryFlag))
        {
            Assert.DoesNotContain("_summary=", result.SelfLink, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("_summary=" + result.SummaryFlag, result.SelfLink, StringComparison.Ordinal);
        }
    }

    private static void AssertSummarySearchResult(
        SearchResultInfo result,
        int total,
        string[] identities,
        IReadOnlyDictionary<string, SummaryResourceExpectation> expectations)
    {
        AssertSummarySearchCounts(result, total);
        if (result.SummaryFlag == "count")
        {
            Assert.Empty(result.Resources);
            return;
        }

        Assert.Equal(identities.Order(StringComparer.Ordinal), result.Resources.Select(SummaryIdentity).Order(StringComparer.Ordinal));
        foreach (JsonElement resource in result.Resources)
        {
            SummaryResourceExpectation expected = expectations[SummaryIdentity(resource)];
            AssertSummaryProjection(expected.Input, resource,
                string.IsNullOrEmpty(result.SummaryFlag) ? "false" : result.SummaryFlag, expected.Binding);
        }
    }

    private static string[] ExpectedSummaryIdentities(string search, string generatedPatient)
    {
        // Reviewed data/r4 inputs, not identities learned from the query being tested.
        string[] officialPatients = ["Patient/example", "Patient/pat1", "Patient/pat2", "Patient/pat3", "Patient/pat4"];
        string[] observations =
        [
            "Observation/1minute-apgar-score", "Observation/656", "Observation/blood-pressure",
            "Observation/blood-pressure-cancel", "Observation/blood-pressure-dar", "Observation/example",
        ];
        return search switch
        {
            "/Patient?_id:not=example" => ["Patient/pat1", "Patient/pat2", "Patient/pat3", "Patient/pat4", generatedPatient],
            "/Patient?_profile:missing=false" or "/Patient?active:missing=true" => [generatedPatient],
            "/Patient?_profile:missing=true" or "/Patient?name-use=official" or "/Patient?active=true" => officialPatients,
            "/Patient?multiplebirth=3" or "/Patient?multiplebirth=le3" or "/Patient?gender=female" => ["Patient/pat4"],
            "/Patient?birthdate=1982-01-23" or "/Patient?birthdate=1982-01" => ["Patient/pat3"],
            "/Patient?birthdate=1982" => ["Patient/pat3", "Patient/pat4"],
            "/Patient?gender=male" => ["Patient/example", "Patient/pat1", "Patient/pat3"],
            "/Patient?gender=male,female" => ["Patient/example", "Patient/pat1", "Patient/pat3", "Patient/pat4"],
            "?_type=Patient" => [.. officialPatients, generatedPatient],
            "?_type=Patient,Observation" => [.. officialPatients, generatedPatient, .. observations],
            "/Patient/example/Observation?patient=example" =>
                ["Observation/example", "Observation/blood-pressure", "Observation/blood-pressure-cancel", "Observation/blood-pressure-dar"],
            "/Patient?_id=example" or
            "/Patient?name=peter" or
            "/Patient?name=peter,not-present" or
            "/Patient?name=not-present,peter" or
            "/Patient?name:contains=eter" or
            "/Patient?name:exact=Peter" or
            "/Patient?identifier=urn:oid:1.2.36.146.595.217.0.1|12345" or
            "/Patient?identifier=|12345" or
            "/Patient?identifier:of-type=http://terminology.hl7.org/CodeSystem/v2-0203|MR|12345" or
            "/Patient?telecom=phone|(03) 5555 6473" or
            "/Patient?telecom=|(03) 5555 6473" or
            "/Patient?telecom=phone|" or
            "/Patient?_id=example&name=peter" or
            "/Patient?_has:Observation:patient:_id=blood-pressure" or
            "/Patient?_has:Observation:subject:_id=blood-pressure" => ["Patient/example"],
            "/Patient?_id=AnIdThatDoesNotExist" or
            "/Patient?name=not-present,another-not-present" or
            "/Patient?name:contains=zzrot" or
            "/Patient?name:exact=peter" or
            "/Patient?name:exact=Peterish" or
            "/Patient?multiplebirth=lt3" or
            "/Patient?gender=InvalidValue" or
            "/Patient?name-use=invalid-name-use" or
            "/Patient?identifier=urn:oid:1.2.36.146.595.217.0.1|ValueThatDoesNotExist" or
            "/Patient?identifier:of-type=http://terminology.hl7.org/CodeSystem/v2-0203|EXT|12345" or
            "/Patient?identifier:of-type=http://terminology.hl7.org/CodeSystem/v2-0203|MR|ABC" or
            "/Patient?active=false" or
            "/Patient?active=garbage" or
            "/Patient?_id=example&name=not-present" or
            "/Patient?_id=example&_profile:missing=false" => [],
            _ => throw new ArgumentException("No independently authored summary membership for " + search, nameof(search)),
        };
    }

    private Dictionary<string, SummaryResourceExpectation> BindSummaryInputs(JsonElement[] resources)
    {
        string directory = _fixture._config.LoadDirectory?.FullName
            ?? throw new InvalidOperationException("The R4 summary fixture requires its checked-in data/r4 inputs.");
        Dictionary<string, SummaryResourceExpectation> expectations = new(StringComparer.Ordinal);
        foreach (JsonElement input in SummaryInputs(directory))
        {
            string resourceType = input.GetProperty("resourceType").GetString()!;
            JsonElement actual;
            if (input.TryGetProperty("id", out JsonElement inputId))
            {
                string identity = resourceType + "/" + inputId.GetString();
                actual = Assert.Single(resources, resource => SummaryIdentity(resource) == identity);
            }
            else
            {
                // The transaction's sole id-less Patient is bound by its authored identifier, not by search order.
                Assert.Equal("Patient", resourceType);
                JsonElement identifier = Assert.Single(input.GetProperty("identifier").EnumerateArray());
                string system = identifier.GetProperty("system").GetString()!;
                string value = identifier.GetProperty("value").GetString()!;
                actual = Assert.Single(resources, resource =>
                    resource.GetProperty("resourceType").GetString() == "Patient" &&
                    resource.GetProperty("identifier").EnumerateArray().Any(candidate =>
                        candidate.GetProperty("system").GetString() == system &&
                        candidate.GetProperty("value").GetString() == value));
            }

            string id = actual.GetProperty("id").GetString()!;
            Assert.Matches(@"\A[A-Za-z0-9\-.]{1,64}\z", id);
            JsonElement meta = actual.GetProperty("meta");
            string version = meta.GetProperty("versionId").GetString()!;
            Assert.Equal("1", version);
            string lastUpdated = meta.GetProperty("lastUpdated").GetString()!;
            Assert.Matches(@"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})\z", lastUpdated);
            Assert.True(DateTimeOffset.TryParse(lastUpdated, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTimeOffset instant));
            Assert.True(instant <= DateTimeOffset.UtcNow.AddSeconds(1));
            string baseUrl = _fixture._config.BaseUrl;
            GeneratedResourceBinding binding = new(resourceType, id, version, lastUpdated, instant, baseUrl, $"{baseUrl}/{resourceType}/{id}");

            // Only generated identity/version/time are captured. All content still comes from the raw input.
            AssertSummaryProjection(input, actual, "false", binding);
            Assert.True(expectations.TryAdd(SummaryIdentity(actual), new(input, binding)));
        }

        Assert.Equal(resources.Length, expectations.Count);
        Assert.Equal(expectations.Keys.Order(StringComparer.Ordinal), resources.Select(SummaryIdentity).Order(StringComparer.Ordinal));
        return expectations;
    }

    private static IEnumerable<JsonElement> SummaryInputs(string directory)
    {
        string[] files =
        [
            "patient-example.json", "patient-example-a.json", "patient-example-b.json",
            "patient-example-c.json", "patient-example-d.json",
            "Observation-1minute-apgar-score.json", "Observation-656.json", "Observation-blood-pressure.json",
            "Observation-blood-pressure-cancel.json", "Observation-blood-pressure-dar.json", "Observation-example.json",
        ];
        foreach (string fileName in files)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, fileName)));
            yield return document.RootElement.Clone();
        }

        using JsonDocument transaction = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "Bundle-transaction-cdex-load-payer.json")));
        JsonElement patient = transaction.RootElement.GetProperty("entry")[0].GetProperty("resource");
        Assert.Equal("Patient", patient.GetProperty("resourceType").GetString());
        Assert.False(patient.TryGetProperty("id", out _));
        yield return patient.Clone();
    }

    private static void AssertSummaryProjection(
        JsonElement input,
        JsonElement actual,
        string summaryFlag,
        GeneratedResourceBinding? binding)
    {
        string resourceType = input.GetProperty("resourceType").GetString()!;
        Assert.Contains(resourceType, new[] { "Patient", "Observation" });
        Assert.Equal(resourceType, actual.GetProperty("resourceType").GetString());
        Assert.Equal(binding?.Id ?? input.GetProperty("id").GetString(), actual.GetProperty("id").GetString());
        if (binding is not null)
        {
            Assert.Equal(binding.Version, actual.GetProperty("meta").GetProperty("versionId").GetString());
            Assert.Equal(binding.LastUpdated, actual.GetProperty("meta").GetProperty("lastUpdated").GetString());
        }

        string[] selected = summaryFlag switch
        {
            "false" => input.EnumerateObject().Select(property => property.Name).ToArray(),
            "data" => input.EnumerateObject().Where(property => property.Name != "text").Select(property => property.Name).ToArray(),
            "text" => resourceType == "Patient"
                ? ["resourceType", "id", "meta", "text"]
                : ["resourceType", "id", "meta", "text", "status", "code"],
            "true" => SummaryMembers(resourceType),
            _ => throw new ArgumentOutOfRangeException(nameof(summaryFlag)),
        };
        string[] present = input.EnumerateObject().Where(property => selected.Contains(property.Name, StringComparer.Ordinal))
            .Select(property => property.Name).ToArray();
        string[] generatedMembers = binding is not null || summaryFlag != "false" ? ["id", "meta"] : ["id"];
        AssertSummaryJsonMembers(actual, present.Concat(generatedMembers).Distinct(StringComparer.Ordinal));

        if (summaryFlag == "false")
        {
            JsonProperty[] originalMeta = input.TryGetProperty("meta", out JsonElement inputMeta)
                ? inputMeta.EnumerateObject().ToArray()
                : [];
            JsonElement actualMeta = actual.GetProperty("meta");
            AssertSummaryJsonMembers(actualMeta,
                originalMeta.Select(property => property.Name).Concat(["versionId", "lastUpdated"]).Distinct(StringComparer.Ordinal));
            foreach (JsonProperty property in originalMeta)
            {
                WireContractAssertions.Content(property.Value.GetRawText(), actualMeta.GetProperty(property.Name).GetRawText(), CompatibilityFormat.Json);
            }
        }
        else
        {
            // Included contained resources receive the same exact pair, but not stored-resource version/time.
            Sdk6ContractTests.AssertSummaryExchangeMetadata(input.GetRawText(), actual.GetRawText(), CompatibilityFormat.Json,
                storedResource: binding is not null);
        }

        foreach (string name in present.Where(name => name is not ("id" or "meta")))
        {
            JsonElement expectedValue = input.GetProperty(name);
            JsonElement actualValue = actual.GetProperty(name);
            if (summaryFlag == "data" && name == "contained")
            {
                Assert.Equal(expectedValue.GetArrayLength(), actualValue.GetArrayLength());
                for (int index = 0; index < expectedValue.GetArrayLength(); index++)
                {
                    AssertSummaryProjection(expectedValue[index], actualValue[index], "data", null);
                }
            }
            else if (summaryFlag == "true")
            {
                AssertSummaryValue(expectedValue, actualValue, SummaryChildShape(resourceType, name, expectedValue));
            }
            else
            {
                WireContractAssertions.Content(expectedValue.GetRawText(), actualValue.GetRawText(), CompatibilityFormat.Json);
            }
        }
    }

    private static void AssertSummaryValue(JsonElement input, JsonElement actual, string shape)
    {
        Assert.Equal(input.ValueKind, actual.ValueKind);
        if (input.ValueKind == JsonValueKind.Array)
        {
            Assert.Equal(input.GetArrayLength(), actual.GetArrayLength());
            for (int index = 0; index < input.GetArrayLength(); index++)
            {
                AssertSummaryValue(input[index], actual[index], shape);
            }
            return;
        }
        if (input.ValueKind != JsonValueKind.Object)
        {
            WireContractAssertions.Content(input.GetRawText(), actual.GetRawText(), CompatibilityFormat.Json);
            return;
        }

        string[] allowed = SummaryMembers(shape);
        JsonProperty[] properties = input.EnumerateObject().Where(property => allowed.Contains(property.Name, StringComparer.Ordinal)).ToArray();
        AssertSummaryJsonMembers(actual, properties.Select(property => property.Name));
        foreach (JsonProperty property in properties)
        {
            AssertSummaryValue(property.Value, actual.GetProperty(property.Name), SummaryChildShape(shape, property.Name, property.Value));
        }
    }

    private static string[] SummaryMembers(string shape) => shape switch
    {
        // FHIR R4 element definitions' isSummary flags, restricted to the shapes in the reviewed inputs.
        // No serializer/model inspector supplies this list. Element.id/extension and primitive extensions are not summary fields.
        "Patient" =>
        [
            "resourceType", "id", "meta", "implicitRules", "identifier", "active", "name", "telecom", "gender",
            "birthDate", "deceasedBoolean", "deceasedDateTime", "address", "managingOrganization", "link",
        ],
        "Observation" =>
        [
            "resourceType", "id", "meta", "implicitRules", "identifier", "basedOn", "partOf", "status", "code",
            "subject", "focus", "encounter", "effectiveDateTime", "issued", "performer", "valueQuantity",
            "hasMember", "derivedFrom", "component",
        ],
        "Observation.component" => ["modifierExtension", "code", "valueQuantity", "valueCodeableConcept"],
        "Patient.link" => ["modifierExtension", "other", "type"],
        "Identifier" => ["use", "type", "system", "value", "period", "assigner"],
        "HumanName" => ["use", "text", "family", "given", "prefix", "suffix", "period"],
        "ContactPoint" => ["system", "value", "use", "rank", "period"],
        "Address" => ["use", "type", "text", "line", "city", "district", "state", "postalCode", "country", "period"],
        "Reference" => ["reference", "type", "identifier", "display"],
        "Coding" => ["system", "version", "code", "display", "userSelected"],
        "CodeableConcept" => ["coding", "text"],
        "Quantity" => ["value", "comparator", "unit", "system", "code"],
        "Period" => ["start", "end"],
        _ => throw new ArgumentException("No independently authored summary projection for " + shape, nameof(shape)),
    };

    private static string SummaryChildShape(string shape, string name, JsonElement value)
    {
        if (value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            return "primitive";
        }
        return (shape, name) switch
        {
            ("Patient", "identifier") or ("Observation", "identifier") or ("Reference", "identifier") => "Identifier",
            ("Patient", "name") => "HumanName",
            ("Patient", "telecom") => "ContactPoint",
            ("Patient", "address") => "Address",
            ("Patient", "link") => "Patient.link",
            ("Patient", "managingOrganization") or ("Patient.link", "other") or ("Identifier", "assigner") or
            ("Observation", "basedOn") or ("Observation", "partOf") or ("Observation", "subject") or
            ("Observation", "focus") or ("Observation", "encounter") or ("Observation", "performer") or
            ("Observation", "hasMember") or ("Observation", "derivedFrom") => "Reference",
            ("Observation", "code") or ("Observation.component", "code") or
            ("Observation.component", "valueCodeableConcept") or ("Identifier", "type") => "CodeableConcept",
            ("Observation", "valueQuantity") or ("Observation.component", "valueQuantity") => "Quantity",
            ("Observation", "component") => "Observation.component",
            ("CodeableConcept", "coding") => "Coding",
            ("Identifier", "period") or ("HumanName", "period") or ("ContactPoint", "period") or ("Address", "period") => "Period",
            ("HumanName", "given") or ("HumanName", "prefix") or ("HumanName", "suffix") or ("Address", "line") => "primitive",
            _ => throw new ArgumentException($"No independently authored summary child shape for {shape}.{name}.", nameof(name)),
        };
    }

    private void AssertSummaryOutcome(string body) =>
        WireContractAssertions.Body(new OutcomeBodyExpectation("summary-outcome", [new("information", "success")]),
            body, CompatibilityFormat.Json, _fixture._config.BaseUrl, new CompatibilityBindings());

    private static string SummaryIdentity(JsonElement resource) =>
        resource.GetProperty("resourceType").GetString() + "/" + resource.GetProperty("id").GetString();

    private static void AssertSummaryJsonMembers(JsonElement actual, IEnumerable<string> expected)
    {
        Assert.Equal(JsonValueKind.Object, actual.ValueKind);
        string[] names = actual.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(expected.Order(StringComparer.Ordinal), names.Order(StringComparer.Ordinal));
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

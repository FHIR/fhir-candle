// <copyright file="FhirStoreTestsR5Resource.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR5;
extern alias coreR5;

using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using fhir.candle.Tests.Models;
using System.Text.Json;
using Xunit.Abstractions;
using candleR5::FhirCandle.Storage;
using fhir.candle.Tests.Extensions;
using Shouldly;
using System.Net;

namespace fhir.candle.Tests;

/// <summary>Unit tests for FHIR R5.</summary>
public class R5Tests
{
    /// <summary>The FHIR store.</summary>
    internal IFhirStore _store;

    /// <summary>(Immutable) The configuration.</summary>
    internal readonly TenantConfiguration _config;

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

    /// <summary>(Immutable) Number of encounters.</summary>
    internal const int _encounterCount = 3;

    /// <summary>(Immutable) Identifier for the encounters with subject.</summary>
    internal const int _encountersWithSubjectIdentifier = 1;

    /// <summary>(Immutable) The number of encounters with the subject 'Patient/example'.</summary>
    internal const int _encountersWithSubjectExample = 3;

    /// <summary>(Immutable) Number of subscription topics.</summary>
    internal const int _subscriptionTopicCount = 1;

    /// <summary>Initializes a new instance of the <see cref="R5Tests"/> class.</summary>
    public R5Tests()
    {
        string path = Path.GetRelativePath(Directory.GetCurrentDirectory(), "data/r5");
        DirectoryInfo? loadDirectory = null;

        if (Directory.Exists(path))
        {
            loadDirectory = new DirectoryInfo(path);
        }

        _config = new()
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R5,
            ControllerName = "r5",
            BaseUrl = "http://localhost/fhir/r5",
            LoadDirectory = loadDirectory,
        };

        _store = new VersionedFhirStore();
        _store.Init(_config);
    }
}

/// <summary>Test R5 patient looped.</summary>
public class R5TestsPatientLooped : IClassFixture<R5Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R5Tests _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="R5TestsPatientLooped"/> class.
    /// </summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">(Immutable) The test output helper.</param>
    public R5TestsPatientLooped(R5Tests fixture, ITestOutputHelper testOutputHelper)
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

/// <summary>Test R5 Encounter searches.</summary>
public class R5TestsEncounter : IClassFixture<R5Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R5Tests _fixture;

    /// <summary>Initializes a new instance of the <see cref="R5TestsEncounter"/> class.</summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">The test output helper.</param>
    public R5TestsEncounter(R5Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("subject:identifier=urn:oid:1.2.36.146.595.217.0.1|12345", R5Tests._encountersWithSubjectIdentifier)]
    [InlineData("subject:identifier=urn:oid:1.2.36.146.595.217.0.1|", R5Tests._encountersWithSubjectIdentifier)]
    [InlineData("subject:identifier=|12345", R5Tests._encountersWithSubjectIdentifier)]
    [InlineData("subject=Patient/example", R5Tests._encountersWithSubjectExample)]
    [InlineData("subject._id=example", R5Tests._encountersWithSubjectExample)]
    [InlineData("subject:Patient._id=example", R5Tests._encountersWithSubjectExample)]
    [InlineData("subject._id=example&_include=Encounter:patient", R5Tests._encountersWithSubjectExample, R5Tests._encountersWithSubjectExample + 1)]
    public void EncounterSearch(string search, int matchCount, int? entryCount = null)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "GET",
            Url = _fixture._store.Config.BaseUrl + "/Encounter?" + search,
            Forwarded = null,
            Authorization = null,
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
        selfLink.ShouldStartWith(_fixture._config.BaseUrl + "/Encounter?");
        foreach (string searchPart in search.Split('&'))
        {
            selfLink.ShouldContain(searchPart);
        }

        //_testOutputHelper.WriteLine(bundle);
    }
}


/// <summary>Test R5 Observation searches.</summary>
public class R5TestsObservation : IClassFixture<R5Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R5Tests _fixture;

    /// <summary>Initializes a new instance of the <see cref="R5TestsObservation"/> class.</summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">The test output helper.</param>
    public R5TestsObservation(R5Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("_id:not=example", (R5Tests._observationCount - 1))]
    [InlineData("_id=AnIdThatDoesNotExist", 0)]
    [InlineData("_id=example", 1)]
    [InlineData("_id=example&_include=Observation:patient", 1, 2)]
    [InlineData("code-value-quantity=http://loinc.org|29463-7$185|http://unitsofmeasure.org|[lb_av]", 1)]
    [InlineData("code-value-quantity=http://loinc.org|29463-7,http://example.org|testing$185|http://unitsofmeasure.org|[lb_av]", 1)]
    [InlineData("code-value-quantity=http://loinc.org|29463-7,urn:iso:std:iso:11073:10101|152584$185|http://unitsofmeasure.org|[lb_av],820|urn:iso:std:iso:11073:10101|265201", 2)]
    [InlineData("value-quantity=185|http://unitsofmeasure.org|[lb_av]", 1)]
    [InlineData("value-quantity=185|http://unitsofmeasure.org|lbs", 1)]
    [InlineData("value-quantity=185||[lb_av]", 1)]
    [InlineData("value-quantity=185||lbs", 1)]
    [InlineData("value-quantity=185", 1)]
    [InlineData("value-quantity=ge185|http://unitsofmeasure.org|[lb_av]", 1)]
    [InlineData("value-quantity=ge185||[lb_av]", 1)]
    [InlineData("value-quantity=ge185||lbs", 1)]
    [InlineData("value-quantity=ge185", 2)]
    [InlineData("value-quantity=gt185|http://unitsofmeasure.org|[lb_av]", 0)]
    [InlineData("value-quantity=gt185||[lb_av]", 0)]
    [InlineData("value-quantity=gt185||lbs", 0)]
    [InlineData("value-quantity=84.1|http://unitsofmeasure.org|[kg]", 0)]       // TODO: test unit conversion
    [InlineData("value-quantity=820|urn:iso:std:iso:11073:10101|265201", 1)]
    [InlineData("value-quantity=820|urn:iso:std:iso:11073:10101|cL/s", 1)]
    [InlineData("value-quantity=820|urn:iso:std:iso:11073:10101|cl/s", 1)]
    [InlineData("value-quantity=820||265201", 1)]
    [InlineData("value-quantity=820||cL/s", 1)]
    [InlineData("subject=Patient/example", R5Tests._observationsWithSubjectExample)]
    [InlineData("subject:Patient=Patient/example", R5Tests._observationsWithSubjectExample)]
    [InlineData("subject:Device=Patient/example", 0)]
    [InlineData("subject=Patient/UnknownPatientId", 0)]
    [InlineData("subject=example", R5Tests._observationsWithSubjectExample)]
    [InlineData("code=http://loinc.org|9272-6", 1)]
    [InlineData("code=http://snomed.info/sct|169895004", 1)]
    [InlineData("code=http://snomed.info/sct|9272-6", 0)]
    [InlineData("_profile=http://hl7.org/fhir/StructureDefinition/vitalsigns", R5Tests._observationsVitalSigns)]
    [InlineData("_profile:missing=true", (R5Tests._observationCount - R5Tests._observationsVitalSigns))]
    [InlineData("_profile:missing=false", R5Tests._observationsVitalSigns)]
    [InlineData("subject.name=peter", R5Tests._observationsWithSubjectExample)]
    [InlineData("subject:Patient.name=peter", R5Tests._observationsWithSubjectExample)]
    [InlineData("subject._id=example", R5Tests._observationsWithSubjectExample)]
    [InlineData("subject:Patient._id=example", R5Tests._observationsWithSubjectExample)]
    [InlineData("subject._id=example&_include=Observation:patient", R5Tests._observationsWithSubjectExample, R5Tests._observationsWithSubjectExample + 1)]
    public void ObservationSearch(string search, int matchCount, int? entryCount = null)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        FhirRequestContext ctx = new()
        {
            TenantName = _fixture._store.Config.ControllerName,
            Store = _fixture._store,
            HttpMethod = "GET",
            Url = _fixture._store.Config.BaseUrl + "/Observation?" + search,
            Forwarded = null,
            Authorization = null,
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
            Authorization = null,
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

/// <summary>Test R5 Patient searches.</summary>
public class R5TestsPatient : IClassFixture<R5Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R5Tests _fixture;

    /// <summary>Initializes a new instance of the <see cref="R5TestsPatient"/> class.</summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">The test output helper.</param>
    public R5TestsPatient(R5Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("_id:not=example", (R5Tests._patientCount - 1))]
    [InlineData("_id=AnIdThatDoesNotExist", 0)]
    [InlineData("_id=example", 1)]
    [InlineData("_id=example&_revinclude=Observation:patient", 1, (R5Tests._observationsWithSubjectExample + 1))]
    [InlineData("name=peter", 1)]
    [InlineData("name=not-present,another-not-present", 0)]
    [InlineData("name=peter,not-present", 1)]
    [InlineData("name=not-present,peter", 1)]
    [InlineData("name:contains=eter", 1)]
    [InlineData("name:contains=zzrot", 0)]
    [InlineData("name:exact=Peter", 1)]
    [InlineData("name:exact=peter", 0)]
    [InlineData("name:exact=Peterish", 0)]
    [InlineData("_profile:missing=true", R5Tests._patientCount)]
    [InlineData("_profile:missing=false", 0)]
    [InlineData("multiplebirth=3", 1)]
    [InlineData("multiplebirth=le3", 1)]
    [InlineData("multiplebirth=lt3", 0)]
    [InlineData("birthdate=1982-01-23", 1)]
    [InlineData("birthdate=1982-01", 1)]
    [InlineData("birthdate=1982", 2)]
    [InlineData("gender=InvalidValue", 0)]
    [InlineData("gender=male", R5Tests._patientsMale)]
    [InlineData("gender=female", R5Tests._patientsFemale)]
    [InlineData("gender=male,female", (R5Tests._patientsMale + R5Tests._patientsFemale))]
    [InlineData("name-use=official", R5Tests._patientCount)]
    [InlineData("name-use=invalid-name-use", 0)]
    [InlineData("identifier=urn:oid:1.2.36.146.595.217.0.1|12345", 1)]
    [InlineData("identifier=|12345", 1)]
    [InlineData("identifier=urn:oid:1.2.36.146.595.217.0.1|ValueThatDoesNotExist", 0)]
    [InlineData("active=true", R5Tests._patientCount)]
    [InlineData("active=false", 0)]
    [InlineData("active=garbage", 0)]
    [InlineData("telecom=phone|(03) 5555 6473", 1)]
    [InlineData("telecom=|(03) 5555 6473", 1)]
    [InlineData("telecom=phone|", 1)]
    [InlineData("_id=example&name=peter", 1)]
    [InlineData("_id=example&name=not-present", 0)]
    [InlineData("_id=example&_profile:missing=false", 0)]
    [InlineData("_has:Observation:patient:_id=blood-pressure", 1)]
    [InlineData("_has:Observation:subject:_id=blood-pressure", 1)]
    public void PatientSearch(string search, int matchCount, int? entryCount = null)
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
            Authorization = null,
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
}

/// <summary>A test subscription internals.</summary>
public class R5TestSubscriptions : IClassFixture<R5Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R5Tests _fixture;

    /// <summary>
    /// Initializes a new instance of the fhir.candle.Tests.TestSubscriptionInternals class.
    /// </summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">(Immutable) The test output helper.</param>
    public R5TestSubscriptions(R5Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>Parse topic.</summary>
    /// <param name="json">The JSON.</param>
    [Theory]
    [FileData("data/r5/SubscriptionTopic-encounter-complete.json")]
    public void ParseTopic(string json)
    {
        HttpStatusCode sc = candleR5.FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
            json,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? r,
            out _);

        sc.ShouldBe(HttpStatusCode.OK);
        r.ShouldNotBeNull();
        r!.TypeName.ShouldBe("SubscriptionTopic");
        candleR5.FhirCandle.Subscriptions.TopicConverter converter = new candleR5.FhirCandle.Subscriptions.TopicConverter();

        bool success = converter.TryParse(r, out ParsedSubscriptionTopic s);

        success.ShouldBeTrue();
        s.ShouldNotBeNull();
        s.Id.ShouldBe("encounter-complete");
        s.Url.ShouldBe("http://example.org/FHIR/SubscriptionTopic/encounter-complete");
        s.ResourceTriggers.ShouldHaveCount(1);
        s.EventTriggers.ShouldBeEmpty();
        s.AllowedFilters.ShouldNotBeEmpty();
        s.NotificationShapes.ShouldNotBeEmpty();
    }

    [Theory]
    [FileData("data/r5/Subscription-encounter-complete.json")]
    public void ParseSubscription(string json)
    {
        HttpStatusCode sc = candleR5.FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
            json,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? r,
            out _);

        sc.ShouldBe(HttpStatusCode.OK);
        r.ShouldNotBeNull();
        r!.TypeName.ShouldBe("Subscription");
        candleR5.FhirCandle.Subscriptions.SubscriptionConverter converter = new candleR5.FhirCandle.Subscriptions.SubscriptionConverter(10);

        bool success = converter.TryParse(r, out ParsedSubscription s);

        success.ShouldBeTrue();
        s.ShouldNotBeNull();
        s.Id.ShouldBe("e1f461cb-f41c-470e-aa75-d5223b2c943a");
        s.TopicUrl.ShouldBe("http://example.org/FHIR/SubscriptionTopic/encounter-complete");
        s.Filters.ShouldHaveCount(1);
        s.ChannelCode.ShouldBe("rest-hook");
        s.Endpoint.ShouldBe("https://subscriptions.argo.run/fhir/r5/$subscription-hook");
        s.HeartbeatSeconds.ShouldBe(120);
        s.TimeoutSeconds.ShouldBe(0);
        s.ContentType.ShouldBe("application/fhir+json");
        s.ContentLevel.ShouldBe("id-only");
        s.CurrentStatus.ShouldBe("active");
    }

    [Theory]
    [FileData("data/r5/Bundle-notification-handshake.json")]
    public void ParseHandshake(string json)
    {
        HttpStatusCode sc = candleR5.FhirCandle.Serialization.SerializationUtils.TryDeserializeFhir(
            json,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? r,
            out _);

        sc.ShouldBe(HttpStatusCode.OK);
        r.ShouldNotBeNull();
        r!.TypeName.ShouldBe("Bundle");

        ParsedSubscriptionStatus? s = ((VersionedFhirStore)_fixture._store).ParseNotificationBundle((Hl7.Fhir.Model.Bundle)r);

        s.ShouldNotBeNull();
        s!.BundleId.ShouldBe("1d2910b6-ccd4-402d-bde3-912d2b4e439f");
        s.SubscriptionReference.ShouldBe("https://subscriptions.argo.run/fhir/r5/Subscription/e1f461cb-f41c-470e-aa75-d5223b2c943a");
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
    [InlineData("(%previous.empty() or (%previous.status != 'completed')) and (%current.status = 'completed')", true, true, true, true, false, false)]
    [InlineData("(%previous.empty() | (%previous.status != 'completed')) and (%current.status = 'completed')", true, true, true, true, false, false)]
    [InlineData("(%previous.id.empty() or (%previous.status != 'completed')) and (%current.status = 'completed')", true, true, true, true, false, false)]
    [InlineData("(%previous.id.empty() | (%previous.status != 'completed')) and (%current.status = 'completed')", true, true, true, true, false, false)]
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
        ResourceStore<coreR5.Hl7.Fhir.Model.Encounter> rs = (ResourceStore<coreR5.Hl7.Fhir.Model.Encounter>)_fixture._store["Encounter"];

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

        coreR5.Hl7.Fhir.Model.Encounter previous = new()
        {
            Id = "object-under-test",
            Status = coreR5.Hl7.Fhir.Model.EncounterStatus.Planned,
        };
        coreR5.Hl7.Fhir.Model.Encounter current = new()
        {
            Id = "object-under-test",
            Status = coreR5.Hl7.Fhir.Model.EncounterStatus.Completed,
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

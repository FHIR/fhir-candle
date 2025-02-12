// <copyright file="FhirStoreTestsR5.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR5;

using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using Shouldly;
using System.Net;
using System.Text.Json;
using Xunit.Abstractions;
using candleR5::FhirCandle.Storage;

namespace fhir.candle.Tests;

/// <summary>Unit tests core FhirStore R5 functionality.</summary>
public class FhirStoreTestsR5: IDisposable
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The configuration.</summary>
    private static readonly TenantConfiguration _config = new()
    {
        FhirVersion = FhirReleases.FhirSequenceCodes.R5,
        ControllerName = "r5",
        BaseUrl = "http://localhost/fhir/r5",
        AllowExistingId = true,
        AllowCreateAsUpdate = true,
    };

    private const int _expectedRestResources = 157;

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirStoreTestsR5"/> class.
    /// </summary>
    /// <param name="testOutputHelper">The test output helper.</param>
    public FhirStoreTestsR5(ITestOutputHelper testOutputHelper)
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
    [FileData("data/r5/searchparameter-patient-multiplebirth.json")]
    public void SearchParameterCreate(string json)
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

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created, response.SerializedOutcome);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"1\"");
        response.Location.ShouldContain("SearchParameter/");

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
            IfMatch = response.ETag,
            IfModifiedSince = response.LastModified,
        };

        success = fhirStore.InstanceRead(
            ctx,
            out response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, response.SerializedOutcome);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"1\"");
        response.Location.ShouldEndWith("SearchParameter/Patient-multiplebirth");
        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData("data/r5/searchparameter-patient-multiplebirth.json")]
    public void SearchParameterCreateCapabilityCount(string json)
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

        success.ShouldBe(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();

        MinimalCapabilities? capabilities = JsonSerializer.Deserialize<MinimalCapabilities>(response.SerializedResource);

        capabilities.ShouldNotBeNull();
        capabilities!.Rest.ShouldNotBeNullOrEmpty();

        MinimalCapabilities.MinimalRest rest = capabilities!.Rest!.First();
        rest.Mode.ShouldBe("server");
        rest.Resources.ShouldNotBeNullOrEmpty();

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

        success.ShouldBe(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

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

        success.ShouldBe(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();

        capabilities = JsonSerializer.Deserialize<MinimalCapabilities>(response.SerializedResource);

        capabilities.ShouldNotBeNull();
        capabilities!.Rest.ShouldNotBeNullOrEmpty();

        rest = capabilities!.Rest!.First();
        rest.Mode.ShouldBe("server");
        rest.Resources.ShouldNotBeNullOrEmpty();

        foreach (MinimalCapabilities.MinimalResource r in rest.Resources!)
        {
            if (r.ResourceType != "Patient")
            {
                continue;
            }

            (r.SearchParams?.Count() ?? 0).ShouldBe(spCount + 1);
            break;
        }
    }

    [Theory]
    [FileData("data/r5/subscriptiontopic-encounter-create-interaction.json")]
    public void SubscriptionTopicCreateRead(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        string serializedResource, serializedOutcome, eTag, lastModified, location;

        DoCreate(
            "SubscriptionTopic",
            json,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        serializedResource.ShouldNotBeNullOrEmpty();
        serializedOutcome.ShouldNotBeNullOrEmpty();
        eTag.ShouldBe("W/\"1\"");
        lastModified.ShouldNotBeNullOrEmpty();
        location.ShouldEndWith("SubscriptionTopic/encounter-create-interaction");

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = fhirStore.Config.BaseUrl + "/SubscriptionTopic/encounter-create-interaction",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
            IfMatch = eTag,
            IfModifiedSince = lastModified,
        };

        bool success = fhirStore.InstanceRead(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, serializedOutcome);
        response.SerializedResource.ShouldNotBeNullOrEmpty();
        response.SerializedOutcome.ShouldNotBeNullOrEmpty();
        response.ETag.ShouldBe("W/\"1\"");
        response.Location.ShouldEndWith("SubscriptionTopic/encounter-create-interaction");
        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData(
        "data/r5/subscriptiontopic-encounter-complete-fhirpath.json",
        "data/r5/subscription-encounter-complete-fhirpath.json",
        "data/r5/patient-example.json",
        "data/r5/encounter-virtual-planned.json")]
    public void SubscriptionNotTriggered(string topicJson, string subscriptionJson, string patientJson, string encounterJson)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        string serializedResource, serializedOutcome, eTag, lastModified, location;

        DoCreate(
            "SubscriptionTopic",
            topicJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        DoCreate(
            "Subscription",
            subscriptionJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        DoCreate(
            "Patient",
            patientJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        DoCreate(
            "Encounter",
            encounterJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        string notification = fhirStore.SerializeSubscriptionEvents(
            "encounter-complete-fhirpath",
            new long[1] { 1 },
            "notification-event",
            false);

        notification.ShouldNotBeEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(notification);

        results.ShouldNotBeNull();
        results!.Entries.ShouldHaveCount(1);

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData(
        "data/r5/subscriptiontopic-encounter-create-interaction.json",
        "data/r5/subscription-encounter-create-interaction.json",
        "data/r5/patient-example.json",
        "data/r5/encounter-virtual-planned.json")]
    public void SubscriptionTriggeredCreate(string topicJson, string subscriptionJson, string patientJson, string encounterJson)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        string serializedResource, serializedOutcome, eTag, lastModified, location;

        DoCreate(
            "SubscriptionTopic",
            topicJson, 
            fhirStore, 
            out serializedResource, 
            out serializedOutcome, 
            out eTag, 
            out lastModified, 
            out location);

        DoCreate(
            "Subscription",
            subscriptionJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        DoCreate(
            "Patient",
            patientJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        DoCreate(
            "Encounter",
            encounterJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);


        string notification = fhirStore.SerializeSubscriptionEvents(
            "encounter-create-interaction",
            Array.Empty<long>(),
            "notification-event",
            false);

        notification.ShouldNotBeEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(notification);
        results.ShouldNotBeNull();
        results.Entries.ShouldNotBeNullOrEmpty();

        MinimalBundle.MinimalEntry.MinimalResource? resource = results.Entries.First().Resource;
        resource.ShouldNotBeNull();
        resource.EventsSinceSubscriptionStart.ShouldNotBeNull();
        resource.EventsSinceSubscriptionStart.ToString().ShouldBeEquivalentTo("1");

        //_testOutputHelper.WriteLine(bundle);
    }


    [Theory]
    [FileData(
        "data/r5/subscriptiontopic-encounter-complete-fhirpath.json",
        "data/r5/subscription-encounter-complete-fhirpath.json",
        "data/r5/patient-example.json",
        "data/r5/encounter-virtual-in-progress.json")]
    public void SubscriptionTriggeredUpdateFhirpath(string topicJson, string subscriptionJson, string patientJson, string encounterJson)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        string serializedResource, serializedOutcome, eTag, lastModified, location;

        DoCreate(
            "SubscriptionTopic",
            topicJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        DoCreate(
            "Subscription",
            subscriptionJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        DoCreate(
            "Patient",
            patientJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        DoCreate(
            "Encounter",
            encounterJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        string notification = fhirStore.SerializeSubscriptionEvents(
            "encounter-complete-fhirpath",
            Array.Empty<long>(),
            "notification-event",
            false);

        notification.ShouldNotBeEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(notification);

        results.ShouldNotBeNull();
        results!.Entries.ShouldHaveCount(1);

        encounterJson = encounterJson.Replace("in-progress", "completed");

        DoUpdate(
            "Encounter",
            "virtual-in-progress",
            encounterJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        notification = fhirStore.SerializeSubscriptionEvents(
            "encounter-complete-fhirpath",
            Array.Empty<long>(),
            "notification-event",
            false);

        notification.ShouldNotBeEmpty();

        results = JsonSerializer.Deserialize<MinimalBundle>(notification);
        results.ShouldNotBeNull();
        results.Entries.ShouldNotBeNullOrEmpty();

        MinimalBundle.MinimalEntry.MinimalResource? resource = results.Entries.First().Resource;
        resource.ShouldNotBeNull();
        resource.EventsSinceSubscriptionStart.ShouldNotBeNull();
        resource.EventsSinceSubscriptionStart.ToString().ShouldBeEquivalentTo("1");

        //_testOutputHelper.WriteLine(bundle);
    }


    [Theory]
    [FileData(
        "data/r5/subscriptiontopic-encounter-complete-query.json",
        "data/r5/subscription-encounter-complete-query.json",
        "data/r5/patient-example.json",
        "data/r5/encounter-virtual-in-progress.json")]
    public void SubscriptionTriggeredUpdateQuery(string topicJson, string subscriptionJson, string patientJson, string encounterJson)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        string serializedResource, serializedOutcome, eTag, lastModified, location;

        DoCreate(
            "SubscriptionTopic",
            topicJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        DoCreate(
            "Subscription",
            subscriptionJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        DoCreate(
            "Patient",
            patientJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        DoCreate(
            "Encounter",
            encounterJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        string notification = fhirStore.SerializeSubscriptionEvents(
            "encounter-complete-query",
            Array.Empty<long>(),
            "notification-event",
            false);

        notification.ShouldNotBeEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(notification);

        results.ShouldNotBeNull();
        results!.Entries.ShouldHaveCount(1);

        encounterJson = encounterJson.Replace("in-progress", "completed");

        DoUpdate(
            "Encounter",
            "virtual-in-progress",
            encounterJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        notification = fhirStore.SerializeSubscriptionEvents(
            "encounter-complete-query",
            Array.Empty<long>(),
            "notification-event",
            false);

        notification.ShouldNotBeEmpty();

        results = JsonSerializer.Deserialize<MinimalBundle>(notification);
        results.ShouldNotBeNull();
        results.Entries.ShouldNotBeNullOrEmpty();

        MinimalBundle.MinimalEntry.MinimalResource? resource = results.Entries.First().Resource;
        resource.ShouldNotBeNull();
        resource.EventsSinceSubscriptionStart.ShouldNotBeNull();
        resource.EventsSinceSubscriptionStart.ToString().ShouldBeEquivalentTo("1");

        //_testOutputHelper.WriteLine(bundle);
    }

    /// <summary>Executes the create operation.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="json">              The JSON.</param>
    /// <param name="fhirStore">         The FHIR store.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <param name="eTag">              [out] The tag.</param>
    /// <param name="lastModified">      [out] The last modified.</param>
    /// <param name="location">          [out] The location.</param>
    private static HttpStatusCode DoCreate(
        string resourceType,
        string json,
        IFhirStore fhirStore, 
        out string serializedResource, 
        out string serializedOutcome, 
        out string eTag, 
        out string lastModified, 
        out string location)
    {
        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = $"{fhirStore.Config.BaseUrl}/{resourceType}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceCreate(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Location.ShouldContain(resourceType);

        serializedResource = response.SerializedResource;
        serializedOutcome = response.SerializedOutcome;
        eTag = response.ETag;
        lastModified = response.LastModified;
        location = response.Location;

        return response.StatusCode ?? HttpStatusCode.InternalServerError;
    }

    /// <summary>Executes the update operation.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="id">                Id of the resource we are updating.</param>
    /// <param name="json">              The JSON.</param>
    /// <param name="fhirStore">         The FHIR store.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <param name="eTag">              [out] The tag.</param>
    /// <param name="lastModified">      [out] The last modified.</param>
    /// <param name="location">          [out] The location.</param>
    private static HttpStatusCode DoUpdate(
        string resourceType,
        string id,
        string json,
        IFhirStore fhirStore,
        out string serializedResource,
        out string serializedOutcome,
        out string eTag,
        out string lastModified,
        out string location)
    {
        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "PUT",
            Url = $"{fhirStore.Config.BaseUrl}/{resourceType}/{id}",
            Forwarded = null,
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
        };

        bool success = fhirStore.InstanceUpdate(
            ctx,
            out FhirResponseContext response);

        success.ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Location.ShouldContain(resourceType);

        serializedResource = response.SerializedResource;
        serializedOutcome = response.SerializedOutcome;
        eTag = response.ETag;
        lastModified = response.LastModified;
        location = response.Location;

        return response.StatusCode ?? HttpStatusCode.InternalServerError;
    }
}

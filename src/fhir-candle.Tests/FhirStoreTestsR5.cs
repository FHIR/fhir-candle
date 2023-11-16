﻿// <copyright file="FhirStoreTestsR5.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR5;

using FhirCandle.Models;
using FhirCandle.Storage;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FluentAssertions;
using System.Net;
using System.Text.Json;
using Xunit.Abstractions;
using candleR5::FhirCandle.Models;
using candleR5::FhirCandle.Storage;
using Hl7.Fhir.Model;
using fhircandle.Tests.Models;

namespace fhir.candle.Tests;

/// <summary>Unit tests core FhirStore R5 functionality.</summary>
public class FhirStoreTestsR5: IDisposable
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The configuration.</summary>
    private static readonly TenantConfiguration _config = new()
    {
        FhirVersion = TenantConfiguration.SupportedFhirVersions.R5,
        ControllerName = "r5",
        BaseUrl = "http://localhost/fhir/r5",
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
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
            AllowCreateAsUpdate = true,
            AllowExistingId = true,
        };

        HttpStatusCode scCreate = fhirStore.InstanceCreate(
            ctx,
            out string serializedResource,
            out string serializedOutcome,
            out string eTag,
            out string lastModified,
            out string location);

        scCreate.Should().Be(HttpStatusCode.Created);
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        lastModified.Should().NotBeNullOrEmpty();
        location.Should().Contain("SearchParameter/");

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = fhirStore.Config.BaseUrl + "/SearchParameter/Patient-multiplebirth",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
            IfMatch = eTag,
            IfModifiedSince = lastModified,
        };

        HttpStatusCode scRead = fhirStore.InstanceRead(
            ctx,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified);

        scRead.Should().Be(HttpStatusCode.OK);
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        location.Should().EndWith("SearchParameter/Patient-multiplebirth");
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
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        // read the metadata
        HttpStatusCode scRead = fhirStore.GetMetadata(
            ctx,
            out string serializedResource,
            out string serializedOutcome,
            out string eTag,
            out string lastModified);

        scRead.Should().Be(HttpStatusCode.OK);
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();

        MinimalCapabilities? capabilities = JsonSerializer.Deserialize<MinimalCapabilities>(serializedResource);

        capabilities.Should().NotBeNull();
        capabilities!.Rest.Should().NotBeNullOrEmpty();

        MinimalCapabilities.MinimalRest rest = capabilities!.Rest!.First();
        rest.Mode.Should().Be("server");
        rest.Resources.Should().NotBeNullOrEmpty();

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
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
            AllowExistingId = true,
            AllowCreateAsUpdate = true,
        };

        // add a search parameter for the patient resource
        HttpStatusCode scCreate = fhirStore.InstanceCreate(
            ctx,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out string location);

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = fhirStore.Config.BaseUrl + "/metadata",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        // read the metadata again
        scRead = fhirStore.GetMetadata(
            ctx,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified);

        scRead.Should().Be(HttpStatusCode.OK);
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();

        capabilities = JsonSerializer.Deserialize<MinimalCapabilities>(serializedResource);

        capabilities.Should().NotBeNull();
        capabilities!.Rest.Should().NotBeNullOrEmpty();

        rest = capabilities!.Rest!.First();
        rest.Mode.Should().Be("server");
        rest.Resources.Should().NotBeNullOrEmpty();

        foreach (MinimalCapabilities.MinimalResource r in rest.Resources!)
        {
            if (r.ResourceType != "Patient")
            {
                continue;
            }

            (r.SearchParams?.Count() ?? 0).Should().Be(spCount + 1);
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

        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        lastModified.Should().NotBeNullOrEmpty();
        location.Should().EndWith("SubscriptionTopic/encounter-create-interaction");

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = fhirStore.Config.BaseUrl + "/SubscriptionTopic/encounter-create-interaction",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
            IfMatch = eTag,
            IfModifiedSince = lastModified,
        };

        HttpStatusCode scRead = fhirStore.InstanceRead(
            ctx,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified);

        scRead.Should().Be(HttpStatusCode.OK);
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        location.Should().EndWith("SubscriptionTopic/encounter-create-interaction");
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

        notification.Should().NotBeEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(notification);

        results.Should().NotBeNull();
        results!.Entries.Should().HaveCount(1);

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

        notification.Should().NotBeEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(notification);

        results.Should().NotBeNull();

        results!.Entries.Should().NotBeEmpty();
        results!.Entries!.First().Resource.Should().NotBeNull();

        MinimalStatus? status = JsonSerializer.Deserialize<MinimalStatus>(results!.Entries!.First().Resource!.ToString() ?? string.Empty);

        status.Should().NotBeNull();
        status!.EventsSinceSubscriptionStart.Should().Be("1");

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

        notification.Should().NotBeEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(notification);

        results.Should().NotBeNull();
        results!.Entries.Should().HaveCount(1);

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

        notification.Should().NotBeEmpty();

        results = JsonSerializer.Deserialize<MinimalBundle>(notification);

        results.Should().NotBeNull();

        results!.Entries.Should().NotBeEmpty();
        results!.Entries!.First().Resource.Should().NotBeNull();

        MinimalStatus? status = JsonSerializer.Deserialize<MinimalStatus>(results!.Entries!.First().Resource!.ToString() ?? string.Empty);

        status.Should().NotBeNull();
        status!.EventsSinceSubscriptionStart.Should().Be("1");


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

        notification.Should().NotBeEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(notification);

        results.Should().NotBeNull();
        results!.Entries.Should().HaveCount(1);

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

        notification.Should().NotBeEmpty();

        results = JsonSerializer.Deserialize<MinimalBundle>(notification);

        results.Should().NotBeNull();

        results!.Entries.Should().NotBeEmpty();
        results!.Entries!.First().Resource.Should().NotBeNull();

        MinimalStatus? status = JsonSerializer.Deserialize<MinimalStatus>(results!.Entries!.First().Resource!.ToString() ?? string.Empty);

        status.Should().NotBeNull();
        status!.EventsSinceSubscriptionStart.Should().Be("1");


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
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
            AllowCreateAsUpdate = true,
            AllowExistingId = true,
        };

        HttpStatusCode sc = fhirStore.InstanceCreate(
            ctx,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);
        sc.Should().Be(HttpStatusCode.Created);
        location.Should().Contain(resourceType);
        return sc;
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
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
            AllowExistingId = true,
            AllowCreateAsUpdate = true,
        };

        HttpStatusCode sc = fhirStore.InstanceUpdate(
            ctx,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);
        sc.Should().Be(HttpStatusCode.OK);
        location.Should().Contain(resourceType);
        return sc;
    }
}
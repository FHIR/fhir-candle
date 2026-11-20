// <copyright file="SubscriptionTriggerInteractionTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Collections;
using System.Net;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using FhirCandle.Models;
using FhirCandle.Utils;
using Hl7.FhirPath;
using Xunit.Abstractions;

namespace fhir.candle.Tests;

/// <summary>Resource-trigger interaction semantics through the native converters and real FHIRPath compiler.</summary>
public sealed class SubscriptionTriggerInteractionTests
{
    private const string ResourceTriggerExtensionUrl = "http://hl7.org/fhir/5.0/StructureDefinition/extension-SubscriptionTopic.resourceTrigger";
    private readonly ITestOutputHelper _output;

    /// <summary>Creates a test with per-operation event and HTTP diagnostics.</summary>
    /// <param name="output">The current xUnit test output.</param>
    public SubscriptionTriggerInteractionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Gets the existing lifecycle fixture's supported FHIR versions.</summary>
    public static IEnumerable<object[]> Versions => LifecycleTestStore.Versions;

    /// <summary>A matching update-only FHIRPath trigger cannot admit create or delete events.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void UpdateOnlyFhirPathTriggersIgnoreCreateAndDelete(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        AssertCrudScenario(fixture, FhirPathTopic(fixture, "true", "update"), [0, 1, 0]);
    }

    /// <summary>Create-only and delete-only restrictions select their corresponding interactions.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void CreateAndDeleteFhirPathRestrictionsMapToTheirInteractions(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string interaction in new[] { "create", "delete" })
        {
            using LifecycleTestStore fixture = new(version);
            int[] expected = interaction == "create" ? [1, 0, 0] : [0, 0, 1];
            _output.WriteLine($"Restriction: {interaction} only.");
            AssertCrudScenario(fixture, FhirPathTopic(fixture, "true", interaction), expected);
        }
    }

    /// <summary>Removing native interaction restrictions retains the compiler's unrestricted default.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void OmittedInteractionsAllowAllResourceChanges(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        AssertCrudScenario(fixture, FhirPathTopic(fixture, "true"), [1, 1, 1]);
    }

    /// <summary>Interaction eligibility alone does not make a false FHIRPath criterion match.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void FalseFhirPathExpressionDoesNotGenerateEvents(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        AssertCrudScenario(fixture, FhirPathTopic(fixture, "false"), [0, 0, 0]);
    }

    /// <summary>Skipped expressions have no error side effects, while eligible failures do not roll back CRUD.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void DisallowedInteractionsSkipFhirPathAndAllowedFailuresRemainContained(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        ParsedSubscription subscription = RegisterTopicAndSubscription(fixture, FhirPathTopic(fixture, "true", "update"));
        long revision = subscription.Revision;
        using CountedThrowingProbe probe = new(fixture, subscription.TopicUrl);
        Assert.False(probe.OnCreate);
        Assert.True(probe.OnUpdate);
        Assert.False(probe.OnDelete);
        Assert.Empty(subscription.NotificationErrors);

        MeasuredInteraction created = CreatePlannedEncounter(fixture, subscription);
        string focusId = created.Response.Id;
        AssertEventDeltas(fixture, subscription, revision, created, focusId, 0);
        Assert.Equal(0, probe.Calls);
        Assert.Empty(subscription.NotificationErrors);

        MeasuredInteraction updated = UpdateEncounter(fixture, subscription, focusId);
        AssertEventDeltas(fixture, subscription, revision, updated, focusId, 0);
        Assert.Equal(1, probe.Calls);
        string error = Assert.Single(subscription.NotificationErrors);
        Assert.Contains(probe.Marker, error);
        string[] errorsAfterUpdate = subscription.NotificationErrors.ToArray();
        AssertUpdatePersisted(fixture, subscription, revision, focusId);
        Assert.Equal(1, probe.Calls);
        Assert.Equal(errorsAfterUpdate, subscription.NotificationErrors.ToArray());

        MeasuredInteraction deleted = DeleteEncounter(fixture, subscription, focusId);
        AssertEventDeltas(fixture, subscription, revision, deleted, focusId, 0);
        Assert.Equal(1, probe.Calls);
        Assert.Equal(errorsAfterUpdate, subscription.NotificationErrors.ToArray());
        AssertDeleted(fixture, subscription, revision, focusId);
        Assert.Equal(1, probe.Calls);
        Assert.Equal(errorsAfterUpdate, subscription.NotificationErrors.ToArray());
    }

    /// <summary>Skipping one trigger preserves later FHIRPath matches and separate query fallback.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void SkippedFhirPathTriggerDoesNotHideLaterFhirPathOrQueryMatches(FhirReleases.FhirSequenceCodes version)
    {
        foreach (bool queryFallback in new[] { false, true })
        {
            using LifecycleTestStore fixture = new(version);
            JsonObject topic = FhirPathTopic(fixture, "true", "create");
            JsonObject first = SingleResourceTrigger(fixture, topic);
            JsonObject eligible;
            if (queryFallback)
            {
                eligible = SingleResourceTrigger(fixture, UpdateOnlyQueryTopic(fixture));
                // Both FHIRPath candidates must be skipped on update before the separate query candidates run.
                ReplaceResourceTriggers(fixture, topic, first, first.DeepClone().AsObject(),
                    eligible, eligible.DeepClone().AsObject());
            }
            else
            {
                eligible = first.DeepClone().AsObject();
                SetInteractions(fixture, eligible, "update");
                ReplaceResourceTriggers(fixture, topic, first, eligible, eligible.DeepClone().AsObject());
            }
            _output.WriteLine(queryFallback ? "Mixed triggers: query fallback." : "Mixed triggers: later FHIRPath match.");
            // Create matches the first candidate; update matches only the later candidates, once per subscription.
            AssertCrudScenario(fixture, topic, [1, 1, 0]);
        }
    }

    /// <summary>The native criterion retains both transition matching and previous-resource evaluation on delete.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void FhirPathCriteriaPreserveCurrentAndPreviousResourceContexts(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version);
        AssertCrudScenario(fixture, fixture.Fixture("topic-fhirpath"), [0, 1, 1]);
    }

    /// <summary>Interaction-only and native query triggers retain their existing update-only behavior.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void InteractionAndQueryTriggersRetainUpdateOnlyEligibility(FhirReleases.FhirSequenceCodes version)
    {
        foreach (string name in new[] { "topic-interaction", "topic-query" })
        {
            using LifecycleTestStore fixture = new(version);
            JsonObject topic = name == "topic-query" ? UpdateOnlyQueryTopic(fixture) : fixture.Fixture(name);
            SetInteractions(fixture, SingleResourceTrigger(fixture, topic), "update");
            _output.WriteLine($"Trigger-family control: {name}.");
            AssertCrudScenario(fixture, topic, [0, 1, 0]);
        }
    }

    private static ParsedSubscription RegisterTopicAndSubscription(LifecycleTestStore fixture, JsonObject topic)
    {
        fixture.Put(fixture.Fixture("patient"));
        fixture.Put(topic);
        return RegisterFilterlessSubscription(fixture);
    }

    private void AssertCrudScenario(LifecycleTestStore fixture, JsonObject topic, int[] expected)
    {
        ParsedSubscription subscription = RegisterTopicAndSubscription(fixture, topic);
        long revision = subscription.Revision;
        string framework = typeof(SubscriptionTriggerInteractionTests).Assembly
            .GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName;
        _output.WriteLine($"{fixture.Version}; {framework}; expected POST/PUT/DELETE deltas: {string.Join("/", expected)} " +
            "for count, history, and EventNotification sends.");

        // Measure the whole CRUD sequence before asserting event expectations so both forbidden operations are visible.
        MeasuredInteraction created = CreatePlannedEncounter(fixture, subscription);
        string focusId = created.Response.Id;
        MeasuredInteraction updated = UpdateEncounter(fixture, subscription, focusId);
        AssertUpdatePersisted(fixture, subscription, revision, focusId);
        MeasuredInteraction deleted = DeleteEncounter(fixture, subscription, focusId);
        AssertDeleted(fixture, subscription, revision, focusId);

        MeasuredInteraction[] mutations = [created, updated, deleted];
        Assert.Equal(mutations.Length, expected.Length);
        for (int index = 0; index < mutations.Length; index++)
        {
            AssertEventDeltas(fixture, subscription, revision, mutations[index], focusId, expected[index]);
        }
        Assert.Empty(subscription.NotificationErrors);
    }

    private MeasuredInteraction CreatePlannedEncounter(LifecycleTestStore fixture, ParsedSubscription subscription)
    {
        JsonObject create = fixture.Fixture("planned");
        create.Remove("id");
        Assert.Equal("planned", create["status"]!.GetValue<string>());
        MeasuredInteraction created = InteractAndMeasure(fixture, subscription, "POST", "Encounter", create);
        AssertSuccessful(created, HttpStatusCode.Created);
        Assert.False(string.IsNullOrEmpty(created.Response.Id));
        return created;
    }

    private MeasuredInteraction UpdateEncounter(LifecycleTestStore fixture, ParsedSubscription subscription, string focusId)
    {
        JsonObject update = fixture.Fixture("complete");
        Assert.Equal(CompleteStatus(fixture), update["status"]!.GetValue<string>());
        update["id"] = focusId;
        update["language"] = "fr";
        MeasuredInteraction updated = InteractAndMeasure(fixture, subscription, "PUT", "Encounter/" + focusId, update);
        AssertSuccessful(updated, HttpStatusCode.OK);
        Assert.Equal(focusId, updated.Response.Id);
        return updated;
    }

    private MeasuredInteraction DeleteEncounter(LifecycleTestStore fixture, ParsedSubscription subscription, string focusId)
    {
        MeasuredInteraction deleted = InteractAndMeasure(fixture, subscription, "DELETE", "Encounter/" + focusId);
        AssertSuccessful(deleted, HttpStatusCode.OK);
        Assert.Equal(focusId, deleted.Response.Id);
        return deleted;
    }

    private void AssertUpdatePersisted(LifecycleTestStore fixture, ParsedSubscription subscription, long revision, string focusId)
    {
        MeasuredInteraction read = InteractAndMeasure(fixture, subscription, "GET", "Encounter/" + focusId);
        AssertSuccessful(read, HttpStatusCode.OK);
        JsonObject persisted = JsonNode.Parse(read.Response.SerializedResource)!.AsObject();
        Assert.Equal(focusId, persisted["id"]!.GetValue<string>());
        Assert.Equal(CompleteStatus(fixture), persisted["status"]!.GetValue<string>());
        Assert.Equal("fr", persisted["language"]!.GetValue<string>());
        AssertEventDeltas(fixture, subscription, revision, read, focusId, 0);
    }

    private void AssertDeleted(LifecycleTestStore fixture, ParsedSubscription subscription, long revision, string focusId)
    {
        MeasuredInteraction missing = InteractAndMeasure(fixture, subscription, "GET", "Encounter/" + focusId);
        Assert.False(missing.Success, missing.Response.SerializedOutcome);
        Assert.Equal(HttpStatusCode.NotFound, missing.Response.StatusCode);
        AssertEventDeltas(fixture, subscription, revision, missing, focusId, 0);
    }

    private static string CompleteStatus(LifecycleTestStore fixture) =>
        fixture.Version == FhirReleases.FhirSequenceCodes.R5 ? "completed" : "finished";

    private static void AssertEventDeltas(
        LifecycleTestStore fixture, ParsedSubscription subscription, long revision, MeasuredInteraction operation,
        string focusId, int expected)
    {
        Assert.Same(subscription, fixture.Subscription);
        Assert.Equal(LifecycleTestStore.Id, subscription.Id);
        Assert.Equal(LifecycleTestStore.Topic, subscription.TopicUrl);
        Assert.Equal(revision, subscription.Revision);
        Assert.Equal(revision, fixture.ExecutableRevision(subscription));
        Assert.Equal("active", subscription.CurrentStatus);
        Assert.True(subscription.ExpirationTicks > fixture.Clock.GetUtcNow().UtcTicks);
        Assert.All(subscription.Filters.Values, filters => Assert.Empty(filters));
        Assert.Equal((long)expected, operation.GeneratedCountDelta);
        Assert.Equal(expected, operation.HistoryDelta);
        Assert.Equal(expected, operation.Events.Length);
        Assert.Equal(expected, operation.Sends.Length);
        Assert.All(operation.Events, subscriptionEvent => AssertEventIdentity(subscriptionEvent, focusId));
        foreach (SubscriptionSendEventArgs send in operation.Sends)
        {
            Assert.Same(subscription, send.Subscription);
            Assert.Same(fixture.Store.Config, send.Tenant);
            Assert.Equal(revision, send.Revision);
            Assert.Equal(ParsedSubscription.NotificationTypeCodes.EventNotification, send.NotificationType);
            SubscriptionEvent subscriptionEvent = Assert.Single(send.NotificationEvents);
            AssertEventIdentity(subscriptionEvent, focusId);
            Assert.Same(Assert.Single(operation.Events, candidate => candidate.EventNumber == subscriptionEvent.EventNumber),
                subscriptionEvent);
        }
    }

    private static JsonObject FhirPathTopic(LifecycleTestStore fixture, string criterion, params string[] interactions)
    {
        JsonObject topic = fixture.Fixture("topic-fhirpath");
        JsonObject trigger = SingleResourceTrigger(fixture, topic);
        SetInteractions(fixture, trigger, interactions);
        if (fixture.Version == FhirReleases.FhirSequenceCodes.R4)
        {
            JsonArray children = trigger["extension"]!.AsArray();
            Assert.Single(children, extension => extension!["url"]!.GetValue<string>() == "fhirPathCriteria")!["valueString"] = criterion;
        }
        else
        {
            trigger["fhirPathCriteria"] = criterion;
        }
        return topic;
    }

    private static JsonObject SingleResourceTrigger(LifecycleTestStore fixture, JsonObject topic)
    {
        if (fixture.Version == FhirReleases.FhirSequenceCodes.R4)
        {
            JsonObject trigger = Assert.Single(topic["extension"]!.AsArray(),
                extension => extension!["url"]!.GetValue<string>() == ResourceTriggerExtensionUrl)!.AsObject();
            Assert.Equal("Encounter", Assert.Single(trigger["extension"]!.AsArray(),
                extension => extension!["url"]!.GetValue<string>() == "resource")!["valueUri"]!.GetValue<string>());
            return trigger;
        }
        JsonObject native = Assert.Single(topic["resourceTrigger"]!.AsArray())!.AsObject();
        Assert.Equal("Encounter", native["resource"]!.GetValue<string>());
        return native;
    }

    private static void SetInteractions(LifecycleTestStore fixture, JsonObject trigger, params string[] interactions)
    {
        if (fixture.Version == FhirReleases.FhirSequenceCodes.R4)
        {
            JsonArray children = trigger["extension"]!.AsArray();
            foreach (JsonNode? interaction in children
                .Where(extension => extension!["url"]!.GetValue<string>() == "supportedInteraction").ToArray())
            {
                children.Remove(interaction);
            }
            foreach (string interaction in interactions)
            {
                children.Add(new JsonObject
                {
                    ["url"] = "supportedInteraction",
                    ["valueCode"] = interaction,
                });
            }
        }
        else
        {
            trigger.Remove("supportedInteraction");
            if (interactions.Length > 0)
            {
                trigger["supportedInteraction"] = new JsonArray(interactions.Select(interaction => (JsonNode?)JsonValue.Create(interaction)).ToArray());
            }
        }
    }

    private static JsonObject UpdateOnlyQueryTopic(LifecycleTestStore fixture)
    {
        JsonObject topic = fixture.Fixture("topic-query");
        JsonArray container = topic[fixture.Version == FhirReleases.FhirSequenceCodes.R4 ? "extension" : "resourceTrigger"]!.AsArray();
        JsonNode?[] triggers = fixture.Version == FhirReleases.FhirSequenceCodes.R4
            ? container.Where(extension => extension!["url"]!.GetValue<string>() == ResourceTriggerExtensionUrl).ToArray()
            : container.ToArray();
        Assert.Equal(2, triggers.Length);
        // The native fixture also has a delete-specific query; this control must contain only the update query.
        ReplaceResourceTriggers(fixture, topic, triggers[0]!.AsObject());
        SetInteractions(fixture, SingleResourceTrigger(fixture, topic), "update");
        return topic;
    }

    private static void ReplaceResourceTriggers(LifecycleTestStore fixture, JsonObject topic, params JsonObject[] triggers)
    {
        JsonNode?[] replacements = triggers.Select(trigger => (JsonNode?)trigger.DeepClone()).ToArray();
        if (fixture.Version == FhirReleases.FhirSequenceCodes.R4)
        {
            JsonArray extensions = topic["extension"]!.AsArray();
            foreach (JsonNode? trigger in extensions
                .Where(extension => extension!["url"]!.GetValue<string>() == ResourceTriggerExtensionUrl).ToArray())
            {
                extensions.Remove(trigger);
            }
            foreach (JsonNode? replacement in replacements)
            {
                extensions.Add(replacement);
            }
        }
        else
        {
            topic["resourceTrigger"] = new JsonArray(replacements);
        }
    }

    private static ParsedSubscription RegisterFilterlessSubscription(LifecycleTestStore fixture)
    {
        JsonObject resource = fixture.SubscriptionJson(end: fixture.Clock.GetUtcNow().AddMinutes(10).ToString("O"));
        resource.Remove(fixture.Version == FhirReleases.FhirSequenceCodes.R5 ? "filterBy" : "_criteria");
        fixture.Put(resource);
        ParsedSubscription subscription = fixture.Subscription;
        Assert.Equal(LifecycleTestStore.Id, subscription.Id);
        Assert.Equal(LifecycleTestStore.Topic, subscription.TopicUrl);
        Assert.Equal("active", subscription.CurrentStatus);
        Assert.True(subscription.ExpirationTicks > fixture.Clock.GetUtcNow().UtcTicks);
        Assert.True(subscription.Revision > 0);
        Assert.All(subscription.Filters.Values, filters => Assert.Empty(filters));
        return subscription;
    }

    private MeasuredInteraction InteractAndMeasure(
        LifecycleTestStore fixture, ParsedSubscription subscription, string method, string path, JsonObject? content = null)
    {
        long generatedBefore = subscription.CurrentEventCount;
        HashSet<long> historyBefore;
        lock (subscription.GeneratedEvents)
        {
            historyBefore = new(subscription.GeneratedEvents.Keys);
        }
        int sentBefore = fixture.Sent.Count;

        (bool success, FhirResponseContext response) = fixture.Interact(method, path, content);

        long generatedDelta = subscription.CurrentEventCount - generatedBefore;
        int historyDelta;
        SubscriptionEvent[] events;
        lock (subscription.GeneratedEvents)
        {
            historyDelta = subscription.GeneratedEvents.Count - historyBefore.Count;
            events = subscription.GeneratedEvents.Values.Where(subscriptionEvent => !historyBefore.Contains(subscriptionEvent.EventNumber)).ToArray();
        }
        SubscriptionSendEventArgs[] sends = fixture.Sent.Skip(sentBefore)
            .Where(send => send.NotificationType == ParsedSubscription.NotificationTypeCodes.EventNotification).ToArray();
        _output.WriteLine($"{fixture.Version} {method} {path}: success={success}; HTTP={(int?)response.StatusCode} ({response.StatusCode}); " +
            $"generated-count delta={generatedDelta}; generated-history delta={historyDelta}; EventNotification-send delta={sends.Length}.");
        return new(success, response, generatedDelta, historyDelta, events, sends);
    }

    private static void AssertSuccessful(MeasuredInteraction operation, HttpStatusCode expected)
    {
        Assert.True(operation.Success, operation.Response.SerializedOutcome);
        Assert.Equal(expected, operation.Response.StatusCode);
    }

    private static void AssertEventIdentity(SubscriptionEvent subscriptionEvent, string focusId)
    {
        Assert.Equal(LifecycleTestStore.Id, subscriptionEvent.SubscriptionId);
        Assert.Equal(LifecycleTestStore.Topic, subscriptionEvent.TopicUrl);
        Assert.Equal("active", subscriptionEvent.StatusAtGeneration);
        Assert.Equal("Encounter", subscriptionEvent.Focus.GetType().Name);
        Assert.Equal(focusId, subscriptionEvent.Focus.GetType().GetProperty("Id")!.GetValue(subscriptionEvent.Focus));
    }

    private sealed class CountedThrowingProbe : IDisposable
    {
        private readonly object _registration;
        private readonly object _executable;
        private readonly PropertyInfo _property;
        private readonly object _originalTriggers;
        private int _calls;
        internal int Calls => Volatile.Read(ref _calls);
        internal string Marker { get; } = "subscription-interaction-probe-" + Guid.NewGuid().ToString("N");
        internal bool OnCreate { get; }
        internal bool OnUpdate { get; }
        internal bool OnDelete { get; }

        internal CountedThrowingProbe(LifecycleTestStore fixture, string topicUrl)
        {
            object resourceStore = fixture.Store["Encounter"];
            _registration = resourceStore.GetType().GetField("_subscriptionRegistrationLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resourceStore)!;
            lock (_registration)
            {
                IDictionary topics = (IDictionary)resourceStore.GetType().GetField("_executableSubscriptions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resourceStore)!;
                _executable = topics[topicUrl]!;
                _property = _executable.GetType().GetProperty("FhirPathTriggers")!;
                _originalTriggers = _property.GetValue(_executable)!;
                object original = Assert.Single(((IEnumerable)_originalTriggers).Cast<object>());
                OnCreate = (bool)original.GetType().GetProperty("OnCreate")!.GetValue(original)!;
                OnUpdate = (bool)original.GetType().GetProperty("OnUpdate")!.GetValue(original)!;
                OnDelete = (bool)original.GetType().GetProperty("OnDelete")!.GetValue(original)!;
                CompiledExpression counted = (root, context) =>
                {
                    Interlocked.Increment(ref _calls);
                    throw new InvalidOperationException(Marker);
                };
                Array triggers = Array.CreateInstance(original.GetType(), 1);
                triggers.SetValue(Activator.CreateInstance(original.GetType(), OnCreate, OnUpdate, OnDelete, counted), 0);
                _property.SetValue(_executable, triggers);
            }
        }

        public void Dispose()
        {
            lock (_registration)
            {
                _property.SetValue(_executable, _originalTriggers);
            }
        }
    }

    private sealed record MeasuredInteraction(
        bool Success,
        FhirResponseContext Response,
        long GeneratedCountDelta,
        int HistoryDelta,
        SubscriptionEvent[] Events,
        SubscriptionSendEventArgs[] Sends);
}

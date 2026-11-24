// <copyright file="LiveCapacityTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;

namespace fhir.candle.Tests;

public class LiveCapacityTests
{
    public static IEnumerable<object[]> Versions => LifecycleTestStore.Versions;

    private static JsonObject Row(string type, string id) => new() { ["resourceType"] = type, ["id"] = id };

    private static void Reset(LifecycleTestStore fixture) =>
        fixture.Store.GetType().GetMethod("ResetStore")!.Invoke(fixture.Store, [false]);

    private static void AssertState(LifecycleTestStore fixture, string[] live, params string[] protectedRows)
    {
        lock (fixture.Field("_writeGate"))
        {
            string[] actual = fixture.Store.SelectMany(type => type.Value.Keys.Select(id => type.Key + "/" + id)).Order().ToArray();
            Assert.Equal(live.Concat(protectedRows).Append("CapabilityStatement/metadata").Order(), actual);
            LinkedList<(string, string)> order = Assert.IsType<LinkedList<(string, string)>>(fixture.Field("_liveOrder"));
            Dictionary<(string, string), LinkedListNode<(string, string)>> nodes =
                Assert.IsType<Dictionary<(string, string), LinkedListNode<(string, string)>>>(fixture.Field("_liveNodes"));
            Assert.Equal(fixture.Store.Config.MaxResourceCount == 0 ? [] : live, order.Select(key => key.Item1 + "/" + key.Item2));
            Assert.Equal(order.Count, nodes.Count);
            for (LinkedListNode<(string, string)>? node = order.First; node is not null; node = node.Next)
            {
                Assert.Same(node, nodes[node.Value]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void LiveIndexMatchesStoreAfterEachMutation(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore other = new(version, capacity: 1);
        other.Store.Config.ControllerName = "other";
        other.Store.Config.BaseUrl = "http://unit.test/fhir/other";
        Reset(other);
        other.Put(Row("Patient", "same"));
        foreach (int capacity in new[] { 0, 1, 2 })
        {
            using LifecycleTestStore fixture = new(version, capacity: capacity);
            Reset(fixture);
            List<string> live = [];
            void Insert(string key)
            {
                live.Add(key);
                if (capacity != 0 && live.Count > capacity)
                {
                    live.RemoveAt(0);
                }
                AssertState(fixture, live.ToArray());
            }
            fixture.Put(Row("Patient", "same"));
            Insert("Patient/same");
            fixture.Put(Row("Patient", "same"));
            AssertState(fixture, live.ToArray());
            fixture.Put(Row("Practitioner", "same"));
            Insert("Practitioner/same");
            string oldest = live[0];
            fixture.Put(Row(oldest.Split('/')[0], "same"));
            AssertState(fixture, live.ToArray());
            FhirRequestContext rejected = new(fixture.Store, "PUT", oldest)
            {
                IfMatch = "W/\"999\"", SourceContent = Row(oldest.Split('/')[0], "same").ToJsonString(),
                SourceFormat = "application/fhir+json", DestinationFormat = "application/fhir+json",
            };
            Assert.False(fixture.Store.InstanceUpdate(rejected, out FhirResponseContext failure));
            Assert.Equal(HttpStatusCode.PreconditionFailed, failure.StatusCode);
            AssertState(fixture, live.ToArray());
            FhirRequestContext conditional = new(fixture.Store, "POST", "Practitioner")
            {
                Interaction = Common.StoreInteractionCodes.TypeCreateConditional, IfNoneExist = "_id=same",
                SourceContent = """{"resourceType":"Practitioner","active":true}""", SourceFormat = "application/fhir+json",
                DestinationFormat = "application/fhir+json",
            };
            Assert.True(fixture.Store.InstanceCreate(conditional, out FhirResponseContext unchanged), unchanged.SerializedOutcome);
            Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
            AssertState(fixture, live.ToArray());
            (bool success, FhirResponseContext created) = fixture.Interact("POST", "Patient", Row("Patient", "ignored"));
            Assert.True(success, created.SerializedOutcome);
            Insert("Patient/" + created.Id);
            Assert.True(fixture.Interact("DELETE", "Patient/" + created.Id).Success);
            live.Remove("Patient/" + created.Id);
            AssertState(fixture, live.ToArray());
            fixture.Put(Row("Patient", created.Id));
            Insert("Patient/" + created.Id);
            Assert.Equal("1", (string?)fixture.Get("Patient/" + created.Id)["meta"]!["versionId"]);
            JsonObject batch = JsonNode.Parse("""
                {"resourceType":"Bundle","type":"batch","entry":[
                 {"request":{"method":"PUT","url":"Patient/batch"},"resource":{"resourceType":"Patient","id":"batch"}}]}
                """)!.AsObject();
            Assert.True(fixture.Interact("POST", "", batch).Success);
            Insert("Patient/batch");
            Assert.True(fixture.Interact("DELETE", "Patient?_id=batch").Success);
            live.Remove("Patient/batch");
            AssertState(fixture, live.ToArray());
            AssertState(other, ["Patient/same"]);
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void DirectTypedWritesShareTheTenantBudget(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version, capacity: 1);
        Reset(fixture);
        fixture.Put(Row("Patient", "typed"));
        object row = fixture.Interact("GET", "Patient/typed").Response.Resource!;
        IResourceStore typed = fixture.Store["Patient"];
        Type type = typed.GetType();
        HashSet<string> none = [];
        Assert.NotNull(type.GetMethod("InstanceDelete")!.Invoke(typed, ["typed", none]));
        AssertState(fixture, []);
        fixture.Put(Row("Practitioner", "typed"));
        object?[] update = [row, false, "", "", none, null, null];
        Assert.Null(type.GetMethod("InstanceUpdate")!.Invoke(typed, update));
        AssertState(fixture, ["Practitioner/typed"]);
        update[1] = true;
        Assert.NotNull(type.GetMethod("InstanceUpdate")!.Invoke(typed, update));
        Assert.Equal(HttpStatusCode.Created, update[5]);
        AssertState(fixture, ["Patient/typed"]);
        object?[] create = [new FhirRequestContext(fixture.Store, "POST", "Patient"), row, true, null, null];
        Assert.Null(type.GetMethod("InstanceCreate")!.Invoke(typed, create));
        Assert.Equal(HttpStatusCode.Conflict, create[3]);
        AssertState(fixture, ["Patient/typed"]);
        fixture.Put(Row("Practitioner", "other"));
        Assert.NotNull(type.GetMethod("InstanceCreate")!.Invoke(typed, create));
        Assert.Equal(HttpStatusCode.Created, create[3]);
        AssertState(fixture, ["Patient/typed"]);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void ProtectedLoadAndMetadataSurviveEvictionAndReset(FhirReleases.FhirSequenceCodes version)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("candle-capacity-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "patient.json"), Row("Patient", "same").ToJsonString());
            using LifecycleTestStore fixture = new(version, capacity: 1, loadDirectory: directory.FullName);
            Reset(fixture);
            const string Protected = "Patient/same";
            AssertState(fixture, [], Protected);
            Assert.Equal("metadata", (string?)fixture.Get("metadata")["id"]);
            fixture.Put(Row("Practitioner", "same"));
            AssertState(fixture, ["Practitioner/same"], Protected);
            fixture.Put(Row("Patient", "next"));
            AssertState(fixture, ["Patient/next"], Protected);
            Assert.Equal("metadata", (string?)fixture.Get("metadata")["id"]);
            AssertState(fixture, ["Patient/next"], Protected);
            Assert.False(fixture.Interact("PUT", Protected, Row("Patient", "same")).Success);
            IResourceStore patient = fixture.Store["Patient"];
            Assert.Null(patient.GetType().GetMethod("InstanceDelete")!.Invoke(patient, ["same", new HashSet<string>()]));
            AssertState(fixture, ["Patient/next"], Protected);
            Reset(fixture);
            AssertState(fixture, [], Protected);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public Task HandedOffCallbackFailureReportsOriginWithoutFailingPublisher(FhirReleases.FhirSequenceCodes version) =>
        ExerciseHandOff(version, throwingSink: false, failingPublisher: false);

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task ThrowingSinkDoesNotChangeWriterOutcomeOrSuppressLaterReport(FhirReleases.FhirSequenceCodes version)
    {
        await ExerciseHandOff(version, throwingSink: true, failingPublisher: false);
        await ExerciseHandOff(version, throwingSink: true, failingPublisher: true);
    }

    private static async Task ExerciseHandOff(FhirReleases.FhirSequenceCodes version, bool throwingSink, bool failingPublisher)
    {
        using LifecycleTestStore fixture = new(version);
        TimeSpan timeout = TimeSpan.FromSeconds(10);
        using ManualResetEventSlim publishing = new();
        using ManualResetEventSlim release = new();
        InvalidOperationException publisherFailure = new("synchronous callback");
        List<string> events = [];
        List<(string Report, bool Locked)> reports = [];
        fixture.Store.GetType().GetField("_subscriptionDiagnosticWriter", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(fixture.Store, new Action<string>(report =>
            {
                reports.Add((report, Monitor.IsEntered(fixture.Field("_writeGate"))));
                if (throwingSink && reports.Count == 1)
                {
                    throw new InvalidOperationException("synthetic sink failure");
                }
            }));

        bool released = false;
        fixture.Store.OnInstanceUpdated += (_, args) =>
        {
            events.Add("put " + args.ResourceId);
            if (args.ResourceId == "publisher")
            {
                publishing.Set();
                released = release.Wait(timeout);
                if (failingPublisher)
                {
                    throw publisherFailure;
                }
            }
        };
        fixture.Store.OnInstanceCreated += (_, args) =>
        {
            events.Add("post " + args.ResourceId);
            throw new InvalidOperationException("synthetic private callback details");
        };
        Task<Exception?> publisher = Task.Run<Exception?>(() => Record.Exception(() => fixture.Put(Row("Patient", "publisher"))));
        List<string> createdIds = [];
        List<(string Report, bool Locked)> expectedReports = [];
        try
        {
            Assert.True(publishing.Wait(timeout));
            long previousBatch = (long)fixture.Field("_mutationSequence");
            for (int i = 0; i < (throwingSink ? 2 : 1); i++)
            {
                string id = "input-" + i;
                (bool success, FhirResponseContext response) = await Task.Run(() =>
                    fixture.Interact("POST", "Practitioner", Row("Practitioner", id))).WaitAsync(timeout);
                Assert.True(success, response.SerializedOutcome);
                createdIds.Add(response.Id);
                long batch = (long)fixture.Field("_mutationSequence");
                Assert.True(batch > previousBatch);
                expectedReports.Add(($"publication_failed batch={batch} event=OnInstanceCreated", false));
                previousBatch = batch;
            }
            await Task.Run(() => fixture.Put(Row("Patient", "later"))).WaitAsync(timeout);
            Assert.False(publisher.IsCompleted);
        }
        finally
        {
            release.Set();
            await publisher.WaitAsync(timeout);
        }

        Assert.True(released);
        Assert.Same(failingPublisher ? publisherFailure : null, await publisher);
        Assert.Equal(expectedReports, reports);
        Assert.Equal(new[] { "put publisher" }.Concat(createdIds.Select(id => "post " + id)).Append("put later"), events);
        Assert.Equal("publisher", (string?)fixture.Get("Patient/publisher")["id"]);
        foreach (string id in createdIds)
        {
            Assert.Equal(id, (string?)fixture.Get("Practitioner/" + id)["id"]);
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void ReentrantPublicationKeepsDistinctFailureOwners(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version, capacity: 1);
        Reset(fixture);
        fixture.Put(Row("Patient", "victim"));
        InvalidOperationException outerFailure = new("outer eviction");
        InvalidOperationException innerFailure = new("inner eviction");
        Exception? innerResult = null;
        List<string> events = [];
        fixture.Store.OnInstanceUpdated += (_, args) =>
        {
            events.Add("put " + args.ResourceId);
            if (args.ResourceId == "outer")
            {
                // The nested publisher must drain the outer eviction before its own events.
                innerResult = Record.Exception(() => fixture.Put(Row("Patient", "inner")));
                events.Add("inner returned");
            }
        };
        fixture.Store.OnInstanceDeleted += (_, args) =>
        {
            events.Add("delete " + args.ResourceId);
            throw args.ResourceId == "victim" ? outerFailure : innerFailure;
        };
        Exception? outerResult = Record.Exception(() => fixture.Put(Row("Patient", "outer")));
        Assert.Equal(["put outer", "delete victim", "put inner", "delete outer", "inner returned"], events);
        Assert.Same(outerFailure, outerResult);
        Assert.Same(innerFailure, innerResult);
        AssertState(fixture, ["Patient/inner"]);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task SubscriptionEvictionPublishesAfterUnlock(FhirReleases.FhirSequenceCodes version)
    {
        using LifecycleTestStore fixture = new(version, capacity: 2);
        Reset(fixture);
        fixture.PutSubscription("requested");
        JsonObject topic = fixture.Fixture("topic-fhirpath");
        fixture.Put(topic);
        string topicKey = topic["resourceType"] + "/" + topic["id"];
        string key = "Subscription/" + LifecycleTestStore.Id;
        long prior = fixture.Subscription.Revision;
        TimeSpan timeout = TimeSpan.FromSeconds(10);
        using ManualResetEventSlim publishing = new();
        using ManualResetEventSlim release = new();
        List<(long Revision, bool Removed)> changes = [];
        List<string> instances = [];
        List<Exception?> observations = [];
        bool released = false;
        fixture.Store.OnInstanceUpdated += (_, args) => instances.Add("put " + args.ResourceType);
        fixture.Store.OnInstanceDeleted += (_, args) => instances.Add("delete " + args.ResourceType);
        fixture.Store.OnSubscriptionsChanged += (_, args) =>
        {
            changes.Add((args.Revision, args.RemovedSubscriptionId is not null));
            observations.Add(Record.Exception(() =>
            {
                fixture.AssertOutsideLocks();
                Assert.False(Monitor.IsEntered(fixture.Field("_writeGate")));
                if (changes.Count == 1)
                {
                    Assert.Equal("active", (string?)fixture.Get(key)["status"]);
                    Assert.Equal(args.Revision, fixture.ExecutableRevision(fixture.Subscription));
                    AssertState(fixture, [key, topicKey]);
                }
            }));
            if (changes.Count == 1)
            {
                publishing.Set();
                released = release.Wait(timeout);
            }
        };
        Task update = Task.Run(() => fixture.PutSubscription("active"));
        try
        {
            Assert.True(publishing.Wait(timeout));
            ParsedSubscription updated = fixture.Subscription;
            await Task.Run(() => fixture.Put(Row("Patient", "victim"))).WaitAsync(timeout);
            Assert.Empty(fixture.Store.CurrentSubscriptions);
            Assert.Null(fixture.ExecutableRevision(updated));
            AssertState(fixture, [topicKey, "Patient/victim"]);
            Assert.True((await Task.Run(() => fixture.Interact("DELETE", "Patient/victim")).WaitAsync(timeout)).Success);
            AssertState(fixture, [topicKey]);
            await Task.Run(() => fixture.PutSubscription()).WaitAsync(timeout);
            Assert.Equal("1", (string?)fixture.Get(key)["meta"]!["versionId"]);
            Assert.Equal(fixture.Subscription.Revision, fixture.ExecutableRevision(fixture.Subscription));
            AssertState(fixture, [topicKey, key]);
        }
        finally
        {
            release.Set();
            await update.WaitAsync(timeout);
        }
        Assert.True(released);
        Assert.All(observations, Assert.Null);
        Assert.Equal([(prior + 1, false), (prior + 2, true), (prior + 3, false)], changes);
        Assert.Equal(["put Subscription", "put Patient", "delete Subscription", "delete Patient", "put Subscription"], instances);
    }
}

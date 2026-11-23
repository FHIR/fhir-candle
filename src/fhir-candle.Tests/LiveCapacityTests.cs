// <copyright file="LiveCapacityTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
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
        fixture.Store.OnInstanceUpdated += (_, args) => instances.Add("put " + args.ResourceType);
        fixture.Store.OnInstanceDeleted += (_, args) => instances.Add("delete " + args.ResourceType);
        fixture.Store.OnSubscriptionsChanged += (_, args) =>
        {
            fixture.AssertOutsideLocks();
            Assert.False(Monitor.IsEntered(fixture.Field("_writeGate")));
            changes.Add((args.Revision, args.RemovedSubscriptionId is not null));
            if (changes.Count == 1)
            {
                Assert.Equal("active", (string?)fixture.Get(key)["status"]);
                Assert.Equal(args.Revision, fixture.ExecutableRevision(fixture.Subscription));
                AssertState(fixture, [key, topicKey]);
                publishing.Set();
                Assert.True(release.Wait(timeout));
            }
        };
        Task update = Task.Run(() => fixture.PutSubscription("active"));
        try
        {
            Assert.True(publishing.Wait(timeout));
            await Task.Run(() =>
            {
                ParsedSubscription updated = fixture.Subscription;
                fixture.Put(Row("Patient", "victim"));
                Assert.Empty(fixture.Store.CurrentSubscriptions);
                Assert.Null(fixture.ExecutableRevision(updated));
                AssertState(fixture, [topicKey, "Patient/victim"]);
                Assert.True(fixture.Interact("DELETE", "Patient/victim").Success);
                AssertState(fixture, [topicKey]);
                fixture.PutSubscription();
                Assert.Equal("1", (string?)fixture.Get(key)["meta"]!["versionId"]);
                Assert.Equal(fixture.Subscription.Revision, fixture.ExecutableRevision(fixture.Subscription));
                AssertState(fixture, [topicKey, key]);
            }).WaitAsync(timeout);
        }
        finally
        {
            release.Set();
            await update.WaitAsync(timeout);
        }
        Assert.Equal([(prior + 1, false), (prior + 2, true), (prior + 3, false)], changes);
        Assert.Equal(["put Subscription", "put Patient", "delete Subscription", "delete Patient", "put Subscription"], instances);
    }
}

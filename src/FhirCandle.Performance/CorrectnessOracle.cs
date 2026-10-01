// <copyright file="CorrectnessOracle.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FhirCandle.Performance.Probe;

namespace FhirCandle.Performance;

/// <summary>A completely consumed response, including actual headers and unmodified body bytes.</summary>
public sealed record ResponseEvidence(
    int StatusCode,
    Dictionary<string, string[]> Headers,
    byte[] Body,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc);

/// <summary>The narrow identity/metadata binding permitted for a newly persisted resource.</summary>
public sealed record ResourceBinding(string ResourceType, string Id, string VersionId, DateTimeOffset LastUpdated);

/// <summary>An independently known input and its previously verified server metadata.</summary>
public sealed record ExpectedResource(byte[] Content, ResourceBinding Binding);

/// <summary>One fully consumed HTTP exchange, including its actual requested address.</summary>
public sealed record HttpExchangeEvidence(string Uri, ResponseEvidence Response);

/// <summary>The individual receiving interval for one expected registered notification.</summary>
public sealed record ReceiptExpectation(NotificationInput Notification, DateTimeOffset Earliest, DateTimeOffset Latest);

/// <summary>Exact successful codes from the two pinned sources; live work must confirm every exercised code.</summary>
public sealed record SuccessfulStatusProfile(
    string Variant, string SourceRevision, int PutAsCreate, int Create, int Update, int Read, int Search, int Delete, int Hook);

/// <summary>Independent JSON and public-state assertions; none of these checks are timed workloads.</summary>
public static class CorrectnessOracle
{
    private const string Source = "https://example.org/fhir/source/";
    private const string Topic = "https://example.org/candle-perf/SubscriptionTopic/observation";
    private const string Timestamp = "2025-01-02T03:04:05+00:00";
    private const string Backport = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/";

    /// <summary>No arbitrary variant or broad 2xx success profile is accepted.</summary>
    public static SuccessfulStatusProfile StatusProfile(string variant) => variant switch
    {
        "baseline" => new(variant, ExperimentRunner.BaselineRevision, 200, 201, 200, 200, 200, 200, 200),
        "candidate" => new(variant, ExperimentRunner.CandidateRevision, 201, 201, 200, 200, 200, 200, 200),
        _ => throw new InvalidDataException("Unknown successful-status artifact profile."),
    };

    /// <summary>Checks every clinical input against separately specified fields, groups and references.</summary>
    public static void ValidateCorpus(WorkloadCorpus corpus)
    {
        Require(corpus.FhirVersion is "R4" or "R4B" or "R5", "Unsupported corpus FHIR version.");
        Require((corpus.PatientCount is 100 or 1000) && corpus.ObservationCount == corpus.PatientCount * 10 &&
            corpus.Resources.Length == corpus.PatientCount + corpus.ObservationCount, "Wrong complete clinical population.");
        ValidatePermutation(corpus.PatientOrder, corpus.PatientCount);
        ValidatePermutation(corpus.ObservationOrder, corpus.ObservationCount);
        for (int index = 0; index < corpus.Resources.Length; index++)
        {
            bool patient = index < corpus.PatientCount;
            int logicalIndex = patient ? index : index - corpus.PatientCount;
            string id = (patient ? "patient-" : "observation-") + logicalIndex.ToString("D8", CultureInfo.InvariantCulture);
            CorpusResource resource = corpus.Resources[index];
            Equal(patient ? "Patient" : "Observation", resource.ResourceType, "corpus type");
            Equal(id, resource.Id, "corpus logical ID");
            string expected = patient ? PatientContract(id, logicalIndex) : ObservationContract(id,
                "Patient/patient-" + (logicalIndex / 10).ToString("D8", CultureInfo.InvariantCulture), logicalIndex);
            CompareJson(Parse(expected), Parse(resource.Body), "complete corpus input " + resource.Identity);
        }
    }

    /// <summary>Checks the frozen request meanings and expected sets independently of returned work.</summary>
    public static void ValidateCaseRequests(
        WorkloadCase workload, WorkloadCorpus corpus, IReadOnlyList<WorkloadRequest> requests, int count, int offset = 0)
    {
        Require(workload.FhirVersion == corpus.FhirVersion && workload.PatientCount == corpus.PatientCount &&
            workload.ObservationCount == corpus.ObservationCount && requests.Count == count && count > 0 && offset >= 0,
            "Case/corpus/operation-count mismatch.");
        for (int index = 0; index < count; index++)
        {
            WorkloadRequest request = requests[index];
            int number = checked(offset + index + 1);
            Require(request.Number == number, "Operation order changed.");
            string suffix = number.ToString("D8", CultureInfo.InvariantCulture);
            int patientIndex = corpus.PatientOrder[(number - 1) % corpus.PatientCount];
            int observationIndex = corpus.ObservationOrder[(number - 1) % corpus.ObservationCount];
            string patient = "patient-" + patientIndex.ToString("D8", CultureInfo.InvariantCulture);
            string observation = "observation-" + observationIndex.ToString("D8", CultureInfo.InvariantCulture);
            string method = "GET";
            string? prefer = null;
            string? content = null;
            string url;
            string[] identities;
            switch (workload.CaseId)
            {
                case "patient-create":
                    method = "POST";
                    url = "Patient";
                    prefer = "return=representation";
                    content = PatientContract(null, number, "created-patient-" + suffix);
                    identities = [];
                    break;
                case "observation-create":
                    method = "POST";
                    url = "Observation";
                    prefer = "return=representation";
                    content = ObservationContract(null, "Patient/" + patient, number, "created-observation-" + suffix);
                    identities = [];
                    break;
                case "patient-update":
                    patientIndex = corpus.PatientOrder[0];
                    patient = "patient-" + patientIndex.ToString("D8", CultureInfo.InvariantCulture);
                    method = "PUT";
                    url = "Patient/" + patient;
                    prefer = "return=representation";
                    content = PatientContract(patient, patientIndex, active: number % 2 == 0);
                    identities = [url];
                    break;
                case "observation-update":
                    observationIndex = corpus.ObservationOrder[0];
                    observation = "observation-" + observationIndex.ToString("D8", CultureInfo.InvariantCulture);
                    method = "PUT";
                    url = "Observation/" + observation;
                    prefer = "return=representation";
                    content = ObservationContract(observation,
                        "Patient/patient-" + (observationIndex / 10).ToString("D8", CultureInfo.InvariantCulture),
                        observationIndex, quantity: number % 2 == 1 ? 38.5m : 39.5m,
                        effective: number % 2 == 1 ? "2025-01-02T03:04:06Z" : "2025-01-02T03:04:07Z");
                    identities = [url];
                    break;
                case "patient-token":
                    url = "Patient?identifier=" + Uri.EscapeDataString("https://example.org/candle-perf/patient|" + patient) + "&_count=100";
                    identities = ["Patient/" + patient];
                    break;
                case "patient-string":
                    url = "Patient?family=Family017&_count=100";
                    identities = Enumerable.Range(0, corpus.PatientCount).Where(value => value % 100 == 17)
                        .Select(value => "Patient/patient-" + value.ToString("D8", CultureInfo.InvariantCulture)).ToArray();
                    break;
                case "observation-token":
                    url = "Observation?code=" + Uri.EscapeDataString("https://example.org/candle-perf/code|Code017") + "&_count=100";
                    identities = Enumerable.Range(0, corpus.ObservationCount).Where(value => value % 1000 == 17)
                        .Select(value => "Observation/observation-" + value.ToString("D8", CultureInfo.InvariantCulture)).ToArray();
                    break;
                case "observation-reference":
                    patientIndex = corpus.PatientOrder[0];
                    url = "Observation?subject=" + Uri.EscapeDataString(
                        "Patient/patient-" + patientIndex.ToString("D8", CultureInfo.InvariantCulture)) + "&_count=100";
                    identities = Enumerable.Range(patientIndex * 10, 10)
                        .Select(value => "Observation/observation-" + value.ToString("D8", CultureInfo.InvariantCulture)).ToArray();
                    break;
                case "empty-search":
                    url = "Observation?code=" + Uri.EscapeDataString("https://example.org/candle-perf/code|OutsideDomain") + "&_count=100";
                    identities = [];
                    break;
                case "patient-read":
                    url = "Patient/" + patient;
                    identities = [url];
                    break;
                case "observation-read":
                    url = "Observation/" + observation;
                    identities = [url];
                    break;
                case "subscription-receive":
                    method = "POST";
                    url = "$subscription-hook";
                    prefer = "return=OperationOutcome";
                    NotificationInput notification = request.Notification ?? throw new InvalidDataException("Missing event input.");
                    Require(notification.FhirVersion == workload.FhirVersion && notification.EventNumber == number,
                        "Wrong notification version/order.");
                    ValidateNotification(notification);
                    Require(request.Body is not null && request.Body.SequenceEqual(notification.Body), "Hook bytes differ from its event fixture.");
                    content = Encoding.UTF8.GetString(notification.Body);
                    identities = ["Bundle/notification-" + suffix];
                    break;
                default:
                    throw new InvalidDataException("Unknown independently specified case.");
            }

            Equal(method, request.Method, "case method");
            Equal(url, request.RelativeUrl, "case URL (including percent-encoded values)");
            Require(request.Prefer == prefer && request.ExpectedIdentities.SequenceEqual(identities, StringComparer.Ordinal),
                "Wrong Prefer or independently expected identities.");
            Require((content is null) == (request.Body is null), "Unexpected or missing input body.");
            Require(workload.CaseId == "subscription-receive" || request.Notification is null, "Unexpected notification binding.");
            if (content is not null)
            {
                CompareJson(Parse(content), Parse(request.Body!), "case input");
            }
        }
    }

    /// <summary>Checks full deterministic inputs independently of the generator's object construction.</summary>
    public static void ValidateChallenge(ProbeChallenge challenge)
    {
        Require(challenge.FhirVersion is "R4" or "R4B" or "R5", "Unsupported FHIR version.");
        Require((challenge.EventNumber > 0) && (challenge.EventNumber <= 99999999), "Invalid event number.");
        string suffix = challenge.EventNumber.ToString("D8", CultureInfo.InvariantCulture);
        Equal("patient-00000000", challenge.PatientId, "challenge Patient ID");
        Equal("notification-" + suffix, challenge.BundleId, "challenge Bundle ID");
        Equal("00000000-0000-4000-8000-" + challenge.EventNumber.ToString("D12", CultureInfo.InvariantCulture),
            challenge.StatusId, "challenge status ID");
        Equal("notification-observation-" + suffix, challenge.FocusId, "challenge focus ID");
        Equal("notification-patient-" + suffix, challenge.ContextId, "challenge context ID");
        CompareJson(Parse(PatientContract(challenge.PatientId)), Parse(challenge.Patient), "Patient input");
        CompareJson(Parse(ObservationContract(null, "Patient/" + challenge.PatientId)), Parse(challenge.Observation), "Observation input");
        ValidateNotification(challenge);
    }

    /// <summary>Requires status first, one complete event, and full Observation plus Patient content.</summary>
    public static void ValidateNotification(ProbeChallenge challenge) =>
        ValidateNotification(new NotificationInput(challenge.FhirVersion, challenge.EventNumber, challenge.BundleId,
            challenge.StatusId, challenge.FocusId, challenge.ContextId, challenge.Notification));

    /// <summary>Checks version-specific primitive/entry requirements and all full-content relationships.</summary>
    public static void ValidateNotification(NotificationInput challenge)
    {
        Require(challenge.FhirVersion is "R4" or "R4B" or "R5", "Unsupported notification FHIR version.");
        Require(challenge.EventNumber is >= 1 and <= 99999999, "Invalid notification event number.");
        string suffix = challenge.EventNumber.ToString("D8", CultureInfo.InvariantCulture);
        Equal("notification-" + suffix, challenge.BundleId, "notification logical ID");
        Equal("00000000-0000-4000-8000-" + challenge.EventNumber.ToString("D12", CultureInfo.InvariantCulture),
            challenge.StatusId, "notification status logical ID");
        Equal("notification-observation-" + suffix, challenge.FocusId, "notification focus logical ID");
        Equal("notification-patient-" + suffix, challenge.ContextId, "notification context logical ID");
        JsonElement bundle = Parse(challenge.Body);
        Fields(bundle, challenge.FhirVersion == "R5"
            ? ["resourceType", "id", "type", "timestamp", "entry"]
            : ["resourceType", "id", "type", "timestamp", "entry", "meta"]);
        Equal("Bundle", Text(bundle, "resourceType"), "notification resource type");
        Equal(challenge.BundleId, Text(bundle, "id"), "notification ID");
        Equal(challenge.FhirVersion == "R5" ? "subscription-notification" : "history", Text(bundle, "type"), "Bundle type");
        Equal(Timestamp, Text(bundle, "timestamp"), "Bundle timestamp");
        if (challenge.FhirVersion != "R5")
        {
            Profile(Property(bundle, "meta"), Backport + (challenge.FhirVersion == "R4"
                ? "backport-subscription-notification-r4" : "backport-subscription-notification"));
        }

        JsonElement entries = Property(bundle, "entry");
        Require((entries.ValueKind == JsonValueKind.Array) && (entries.GetArrayLength() == 3), "Expected status, focus and context entries.");
        string focus = Source + "Observation/" + challenge.FocusId;
        string context = Source + "Patient/" + challenge.ContextId;
        string[] urls = ["urn:uuid:" + challenge.StatusId, focus, context];
        string[] methods = ["GET", "PUT", "PUT"];
        string[] requestUrls = ["Subscription/perf-subscription/$status", "Observation/" + challenge.FocusId, "Patient/" + challenge.ContextId];
        for (int index = 0; index < 3; index++)
        {
            JsonElement entry = entries[index];
            Fields(entry, challenge.FhirVersion == "R5"
                ? ["fullUrl", "resource"] : ["fullUrl", "resource", "request", "response"]);
            Equal(urls[index], Text(entry, "fullUrl"), "entry fullUrl");
            if (challenge.FhirVersion != "R5")
            {
                Fields(Property(entry, "request"), ["method", "url"]);
                Equal(methods[index], Text(Property(entry, "request"), "method"), "history request method");
                Equal(requestUrls[index], Text(Property(entry, "request"), "url"), "history request URL");
                Fields(Property(entry, "response"), ["status"]);
                Equal("200", Text(Property(entry, "response"), "status"), "history response");
            }
        }

        JsonElement status = Property(entries[0], "resource");
        Equal(challenge.StatusId, Text(status, "id"), "status ID");
        string number = challenge.EventNumber.ToString(CultureInfo.InvariantCulture);
        if (challenge.FhirVersion == "R4")
        {
            Fields(status, ["resourceType", "id", "meta", "parameter"]);
            Equal("Parameters", Text(status, "resourceType"), "R4 status");
            Profile(Property(status, "meta"), Backport + "backport-subscription-status-r4");
            JsonElement parameters = Property(status, "parameter");
            Require(parameters.GetArrayLength() == 6, "R4 status parameter count.");
            Parameter(parameters[0], "subscription", "valueReference", Source + "Subscription/perf-subscription", reference: true);
            Parameter(parameters[1], "topic", "valueCanonical", Topic);
            Parameter(parameters[2], "status", "valueCode", "active");
            Parameter(parameters[3], "type", "valueCode", "event-notification");
            Parameter(parameters[4], "events-since-subscription-start", "valueString", number);
            Fields(parameters[5], ["name", "part"]);
            Equal("notification-event", Text(parameters[5], "name"), "R4 event parameter");
            JsonElement parts = Property(parameters[5], "part");
            Require(parts.GetArrayLength() == 4, "R4 event part count.");
            Parameter(parts[0], "event-number", "valueString", number);
            Parameter(parts[1], "timestamp", "valueInstant", Timestamp);
            Parameter(parts[2], "focus", "valueReference", focus, reference: true);
            Parameter(parts[3], "additional-context", "valueReference", context, reference: true);
        }
        else
        {
            Fields(status, ["resourceType", "id", "status", "type", "eventsSinceSubscriptionStart", "notificationEvent", "subscription", "topic"]);
            Equal("SubscriptionStatus", Text(status, "resourceType"), "native status");
            Equal("active", Text(status, "status"), "status code");
            Equal("event-notification", Text(status, "type"), "notification type");
            Equal(number, Text(status, "eventsSinceSubscriptionStart"), "string count primitive");
            Reference(Property(status, "subscription"), Source + "Subscription/perf-subscription");
            Equal(Topic, Text(status, "topic"), "topic");
            JsonElement events = Property(status, "notificationEvent");
            Require(events.GetArrayLength() == 1, "Exactly one notification event is required.");
            Fields(events[0], ["eventNumber", "timestamp", "focus", "additionalContext"]);
            Equal(number, Text(events[0], "eventNumber"), "string event primitive");
            Equal(Timestamp, Text(events[0], "timestamp"), "event timestamp");
            Reference(Property(events[0], "focus"), focus);
            JsonElement contexts = Property(events[0], "additionalContext");
            Require(contexts.GetArrayLength() == 1, "Exactly one additional context is required.");
            Reference(contexts[0], context);
        }

        CompareJson(Parse(ObservationContract(challenge.FocusId, context)), Property(entries[1], "resource"), "full Observation focus");
        CompareJson(Parse(PatientContract(challenge.ContextId)), Property(entries[2], "resource"), "full Patient context");
    }

    /// <summary>Binds ID/metadata to body and headers, then checks the entire persisted representation.</summary>
    public static ResourceBinding ValidateResponse(
        ResponseEvidence response,
        int expectedStatus,
        byte[] expectedContent,
        string baseUrl,
        DateTimeOffset createdSinceUtc,
        ResourceBinding? binding = null)
    {
        ValidateStatusAndMediaType(response, expectedStatus);
        ResourceBinding result = ValidateResourceContent(Parse(response.Body), expectedContent,
            binding?.VersionId ?? "1", createdSinceUtc, response.CompletedUtc, binding, unchanged: true);
        ValidateIdentityHeaders(response, result, baseUrl);
        return result;
    }

    /// <summary>Requires a real clinical change, identity continuity and exactly one version increment.</summary>
    public static ResourceBinding ValidateUpdateResponse(
        ResponseEvidence response, int expectedStatus, byte[] previousContent, byte[] expectedContent,
        ResourceBinding previous, string baseUrl)
    {
        JsonElement before = Parse(previousContent);
        JsonElement after = Parse(expectedContent);
        Equal(previous.ResourceType, Text(before, "resourceType"), "pre-existing update type");
        Equal(previous.Id, Text(before, "id"), "pre-existing update ID");
        Equal(previous.ResourceType, Text(after, "resourceType"), "update type continuity");
        Equal(previous.Id, Text(after, "id"), "update identity continuity");
        if (previous.ResourceType == "Patient")
        {
            Require(Property(before, "active").GetBoolean() != Property(after, "active").GetBoolean(),
                "No-op Patient update.");
        }
        else if (previous.ResourceType == "Observation")
        {
            Require(Property(Property(before, "valueQuantity"), "value").GetDecimal() !=
                Property(Property(after, "valueQuantity"), "value").GetDecimal() &&
                Text(before, "effectiveDateTime") != Text(after, "effectiveDateTime"), "No-op Observation update.");
            CompareJson(Property(before, "subject"), Property(after, "subject"), "preserved update subject");
        }
        else
        {
            throw new InvalidDataException("Unsupported clinical update type.");
        }

        Require(long.TryParse(previous.VersionId, NumberStyles.None, CultureInfo.InvariantCulture, out long version) &&
            version > 0 && version < long.MaxValue, "Invalid pre-existing version.");
        string expectedVersion = (version + 1).ToString(CultureInfo.InvariantCulture);
        ValidateStatusAndMediaType(response, expectedStatus);
        ResourceBinding result = ValidateResourceContent(Parse(response.Body), expectedContent, expectedVersion,
            previous.LastUpdated, response.CompletedUtc, previous, unchanged: false);
        ValidateIdentityHeaders(response, result, baseUrl);
        return result;
    }

    /// <summary>Requires the complete deleted representation; absence is independently checked in stopped state.</summary>
    public static void ValidateDeleteResponse(ResponseEvidence response, int expectedStatus, ExpectedResource expected)
    {
        ValidateStatusAndMediaType(response, expectedStatus);
        _ = ValidateResourceContent(Parse(response.Body), expected.Content, expected.Binding.VersionId,
            expected.Binding.LastUpdated, response.CompletedUtc, expected.Binding, unchanged: true);
        Require(!response.Headers.Keys.Any(name => name.Equals("ETag", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Location", StringComparison.OrdinalIgnoreCase) || name.Equals("Last-Modified", StringComparison.OrdinalIgnoreCase)),
            "Unexpected delete identity headers; the pinned delete contract supplies none.");
    }

    /// <summary>New identities and exact batch growth are both necessary, not interchangeable proofs.</summary>
    public static void ValidateCreateGrowth(
        string resourceType, IReadOnlyCollection<string> initialIds, int initialCount, int finalCount,
        int expectedOperations, IReadOnlyList<ResourceBinding> created)
    {
        Require(initialIds.Count == initialCount && initialIds.Distinct(StringComparer.Ordinal).Count() == initialCount &&
            expectedOperations > 0 && created.Count == expectedOperations && finalCount == initialCount + expectedOperations,
            "Wrong create batch size or resource-count growth.");
        HashSet<string> identities = new(initialIds, StringComparer.Ordinal);
        foreach (ResourceBinding binding in created)
        {
            Require(binding.ResourceType == resourceType && binding.VersionId == "1" && IsFhirId(binding.Id) && identities.Add(binding.Id),
                "Create reused an existing/duplicate identity or returned an update.");
        }
    }

    private static ResourceBinding ValidateResourceContent(
        JsonElement actual, byte[] expectedContent, string expectedVersion,
        DateTimeOffset earliest, DateTimeOffset latest, ResourceBinding? binding, bool unchanged)
    {
        JsonObject expected = JsonNode.Parse(expectedContent)?.AsObject() ?? throw new InvalidDataException("Missing expected resource.");
        string type = expected["resourceType"]!.GetValue<string>();
        Equal(type, Text(actual, "resourceType"), "response resource type");
        string id = Text(actual, "id");
        Require(IsFhirId(id), "Invalid generated FHIR ID.");
        string? fixedId = expected["id"]?.GetValue<string>();
        if (fixedId is not null)
        {
            Equal(fixedId, id, "fixed logical identity");
        }

        if (binding is not null)
        {
            Equal(binding.Id, id, "persisted bound ID");
            Equal(binding.ResourceType, type, "persisted bound type");
        }

        JsonElement meta = Property(actual, "meta");
        Equal(expectedVersion, Text(meta, "versionId"), "stored version");
        string lastUpdatedText = Text(meta, "lastUpdated");
        Require(DateTimeOffset.TryParse(lastUpdatedText, CultureInfo.InvariantCulture, DateTimeStyles.None,
            out DateTimeOffset lastUpdated) && lastUpdated >= earliest && lastUpdated <= latest.AddSeconds(1),
            "Unbound server timestamp.");
        if (binding is not null && unchanged)
        {
            Require(lastUpdated == binding.LastUpdated, "Persisted metadata changed without an update.");
            Equal(binding.VersionId, Text(meta, "versionId"), "persisted version");
        }

        expected["id"] = id;
        JsonObject expectedMeta = expected["meta"]?.AsObject() ?? new JsonObject();
        if (expected["meta"] is null)
        {
            expected["meta"] = expectedMeta;
        }

        expectedMeta["versionId"] = expectedVersion;
        expectedMeta["lastUpdated"] = lastUpdatedText;
        CompareJson(Parse(expected.ToJsonString()), actual, "complete returned content");
        return new ResourceBinding(type, id, expectedVersion, lastUpdated);
    }

    private static void ValidateIdentityHeaders(ResponseEvidence response, ResourceBinding binding, string baseUrl)
    {
        Equal(baseUrl + "/" + binding.ResourceType + "/" + binding.Id, Header(response, "Location"), "Location/body identity");
        Equal("W/\"" + binding.VersionId + "\"", Header(response, "ETag"), "ETag/body version");
        Require(DateTimeOffset.TryParse(Header(response, "Last-Modified"), CultureInfo.InvariantCulture, DateTimeStyles.None,
            out DateTimeOffset modified) && modified.ToUnixTimeSeconds() == binding.LastUpdated.ToUnixTimeSeconds(),
            "Last-Modified/body timestamp mismatch.");
    }

    /// <summary>Requires the materialized successful receiving outcome, not merely any 2xx code.</summary>
    public static void ValidateHookResponse(ResponseEvidence response)
    {
        ValidateStatusAndMediaType(response, 200);
        JsonElement outcome = Parse(response.Body);
        Fields(outcome, ["resourceType", "id", "issue"]);
        Equal("OperationOutcome", Text(outcome, "resourceType"), "hook outcome");
        Require(Guid.TryParseExact(Text(outcome, "id"), "D", out _), "Outcome must have its generated GUID identity.");
        JsonElement issues = Property(outcome, "issue");
        Require(issues.GetArrayLength() == 1, "Hook outcome issue count.");
        Fields(issues[0], ["severity", "code", "diagnostics"]);
        Equal("information", Text(issues[0], "severity"), "outcome severity");
        Equal("success", Text(issues[0], "code"), "outcome code");
        Equal("Subscription Notification Received", Text(issues[0], "diagnostics"), "outcome diagnostics");
    }

    /// <summary>Only continuation discovery belongs inside a future logical-operation window.</summary>
    public static string? NextPageUri(ResponseEvidence response, string currentUri, string baseUrl)
    {
        using JsonDocument document = JsonDocument.Parse(response.Body, new JsonDocumentOptions { MaxDepth = 64 });
        JsonElement root = document.RootElement;
        Require(root.ValueKind == JsonValueKind.Object, "Invalid search continuation document.");
        if (!root.TryGetProperty("link", out JsonElement links))
        {
            return null;
        }

        Require(links.ValueKind == JsonValueKind.Array, "Invalid search links.");
        string? next = null;
        foreach (JsonElement link in links.EnumerateArray())
        {
            if (Text(link, "relation") == "next")
            {
                Require(next is null, "Duplicate search continuation.");
                next = SearchAddress(Text(link, "url"), currentUri, baseUrl).AbsoluteUri;
            }
        }

        return next;
    }

    /// <summary>Audits all pages as one logical search, including complete sets, contents and navigation.</summary>
    public static void ValidateSearch(
        IReadOnlyList<HttpExchangeEvidence> pages, IReadOnlyDictionary<string, ExpectedResource> expected,
        string requestUri, string baseUrl, int expectedStatus = 200)
    {
        Require(pages.Count > 0, "A search has no completed response.");
        HashSet<string> seen = new(StringComparer.Ordinal);
        HashSet<string> addresses = new(StringComparer.Ordinal);
        string? next = requestUri;
        foreach (HttpExchangeEvidence page in pages)
        {
            Require(next is not null && SameSearchUri(page.Uri, next) && addresses.Add(page.Uri),
                "Missing, duplicate or out-of-order search page.");
            ValidateStatusAndMediaType(page.Response, expectedStatus);
            JsonElement bundle = Parse(page.Response.Body);
            bool hasEntries = bundle.TryGetProperty("entry", out JsonElement entries);
            Fields(bundle, hasEntries ? ["resourceType", "type", "total", "link", "entry"] : ["resourceType", "type", "total", "link"]);
            Equal("Bundle", Text(bundle, "resourceType"), "search resource type");
            Equal("searchset", Text(bundle, "type"), "search Bundle type");
            Require(Property(bundle, "total").TryGetInt32(out int total) && total == expected.Count, "Wrong searchset total.");
            JsonElement links = Property(bundle, "link");
            Require(links.ValueKind == JsonValueKind.Array, "Missing search links.");
            HashSet<string> relations = new(StringComparer.Ordinal);
            foreach (JsonElement link in links.EnumerateArray())
            {
                Fields(link, ["relation", "url"]);
                string relation = Text(link, "relation");
                Require((relation is "self" or "next" or "previous" or "first" or "last") && relations.Add(relation),
                    "Unexpected or duplicate search link relation.");
                Uri address = SearchAddress(Text(link, "url"), page.Uri, baseUrl);
                if (relation == "self")
                {
                    Require(SameSearchUri(address.AbsoluteUri, page.Uri), "Search self link changed the request meaning.");
                }
            }

            Require(relations.Contains("self"), "Missing search self link.");
            if (hasEntries)
            {
                Require(entries.ValueKind == JsonValueKind.Array, "Invalid search entries.");
                foreach (JsonElement entry in entries.EnumerateArray())
                {
                    Fields(entry, ["fullUrl", "resource", "search"]);
                    Fields(Property(entry, "search"), ["mode"]);
                    Equal("match", Text(Property(entry, "search"), "mode"), "search entry mode");
                    JsonElement resource = Property(entry, "resource");
                    string identity = Text(resource, "resourceType") + "/" + Text(resource, "id");
                    Require(expected.TryGetValue(identity, out ExpectedResource? known) && seen.Add(identity),
                        "Unexpected, duplicate or error search entry: " + identity);
                    Equal(identity, known!.Binding.ResourceType + "/" + known.Binding.Id, "expected search binding");
                    Equal(baseUrl + "/" + identity, Text(entry, "fullUrl"), "search fullUrl identity");
                    _ = ValidateResourceContent(resource, known.Content, known.Binding.VersionId, known.Binding.LastUpdated,
                        page.Response.CompletedUtc, known.Binding, unchanged: true);
                }
            }

            next = NextPageUri(page.Response, page.Uri, baseUrl);
        }

        Require(next is null, "Truncated search: the final next page was not consumed.");
        Require(seen.SetEquals(expected.Keys), "Missing or truncated search result set.");
    }

    /// <summary>Checks complete clinical counts while preserving every initial conformance-store count.</summary>
    public static void ValidateState(
        TenantSnapshot initial, TenantSnapshot current, int patientCount, int observationCount, int bundleCount,
        IReadOnlyDictionary<string, bool> expectedIdentities, string expectedLoadDirectory)
    {
        Require(patientCount >= 0 && observationCount >= 0 && bundleCount >= 0, "Negative expected population.");
        Require(initial.TenantNames.SequenceEqual(new[] { "perf" }) &&
            current.TenantNames.SequenceEqual(initial.TenantNames), "Wrong tenant population.");
        Require(initial.ResourceCounts.Keys.SequenceEqual(current.ResourceCounts.Keys), "Resource-store inventory changed.");
        Require(initial.ResourceCounts["Patient"] == 0 && initial.ResourceCounts["Observation"] == 0 &&
            initial.ResourceCounts["Bundle"] == 0 && initial.ReceiptKeyCount == 0 && initial.ReceiptCount == 0 &&
            initial.EventCount == 0 && initial.ReceivedNotifications.Count == 0, "Case process did not start clinically empty.");
        foreach (KeyValuePair<string, int> pair in initial.ResourceCounts)
        {
            int expected = pair.Key switch
            {
                "Patient" => patientCount,
                "Observation" => observationCount,
                "Bundle" => bundleCount,
                _ => pair.Value,
            };
            Require(pair.Value >= 0 && current.ResourceCounts[pair.Key] == expected, "Unexpected resource population: " + pair.Key);
        }

        Require(initial.SubscriptionCount == 0 && initial.TopicCount == 0 &&
            current.SubscriptionCount == 0 && current.TopicCount == 0, "Unexpected outbound subscription/topic state.");
        ValidateSourceRegistration(initial, expectedLoadDirectory);
        ValidateSourceRegistration(current, expectedLoadDirectory);
        Require(initial.LoadedPackageDirectives.Length == 0 && initial.LoadedPackageIds.Length == 0 &&
            current.LoadedPackageDirectives.SequenceEqual(initial.LoadedPackageDirectives, StringComparer.Ordinal) &&
            current.LoadedPackageIds.SequenceEqual(initial.LoadedPackageIds, StringComparer.Ordinal) &&
            current.LoadedSupplements.SequenceEqual(initial.LoadedSupplements, StringComparer.Ordinal),
            "Unexpected packages or changed source-registration history.");
        Require(current.ReceiptCount == bundleCount && current.EventCount == bundleCount &&
            current.ReceiptKeyCount == (bundleCount == 0 ? 0 : 1) &&
            current.ReceivedNotifications.Count == current.ReceiptKeyCount &&
            current.ReceivedNotifications.Values.Sum(receipts => receipts.Length) == bundleCount &&
            current.ReceivedNotifications.Values.SelectMany(receipts => receipts)
                .Sum(receipt => receipt.NotificationEvents.Length) == bundleCount, "Unexpected receipt population.");
        Require(current.RequestedResources.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedIdentities.Keys),
            "Inspected identity set differs from the requested audit set.");
        foreach (KeyValuePair<string, bool> expected in expectedIdentities)
        {
            string[] identity = expected.Key.Split('/');
            Require(identity.Length == 2, "Invalid expected state identity.");
            Stored(current, identity[0], identity[1], expected.Value);
        }
    }

    /// <summary>Requires the exact configured source registration and a still-empty owned directory.</summary>
    public static void ValidateEmptySource(TenantSnapshot snapshot, string expectedLoadDirectory)
    {
        ValidateSourceRegistration(snapshot, expectedLoadDirectory);
        Require(Directory.Exists(expectedLoadDirectory), "The owned empty-source directory is missing.");
        Require(!Directory.EnumerateFileSystemEntries(expectedLoadDirectory).Any(), "Unexpected data-source activity.");
    }

    /// <summary>Checks every resource-type delta, fixed source history and HTTP-bound identities.</summary>
    public static void ValidateState(
        TenantSnapshot initial, TenantSnapshot current, ProbeChallenge challenge, ResourceBinding observation,
        string expectedLoadDirectory, bool received)
    {
        Require(initial.TenantNames.SequenceEqual(new[] { "perf" }) && current.TenantNames.SequenceEqual(initial.TenantNames),
            "Wrong tenant population.");
        Require(initial.ResourceCounts.Keys.SequenceEqual(current.ResourceCounts.Keys), "Resource-store inventory changed.");
        foreach (KeyValuePair<string, int> pair in initial.ResourceCounts)
        {
            int growth = pair.Key is "Patient" or "Observation" ? 1 : (received && (pair.Key == "Bundle") ? 1 : 0);
            Require(current.ResourceCounts[pair.Key] == pair.Value + growth, "Unexpected resource growth: " + pair.Key);
        }

        Require(initial.ResourceCounts["Patient"] == 0 && initial.ResourceCounts["Observation"] == 0 &&
            initial.ResourceCounts["Bundle"] == 0 && initial.ReceiptKeyCount == 0 && initial.ReceiptCount == 0 &&
            initial.EventCount == 0 && initial.ReceivedNotifications.Count == 0, "The disposable challenge did not start empty.");
        Require(current.SubscriptionCount == 0 && current.TopicCount == 0, "Unexpected outbound subscription/topic state.");
        ValidateSourceRegistration(initial, expectedLoadDirectory);
        ValidateSourceRegistration(current, expectedLoadDirectory);
        Require(initial.LoadedPackageDirectives.Length == 0 && initial.LoadedPackageIds.Length == 0 &&
            current.LoadedPackageDirectives.SequenceEqual(initial.LoadedPackageDirectives, StringComparer.Ordinal) &&
            current.LoadedPackageIds.SequenceEqual(initial.LoadedPackageIds, StringComparer.Ordinal) &&
            current.LoadedSupplements.SequenceEqual(initial.LoadedSupplements, StringComparer.Ordinal),
            "Unexpected packages or changed source-registration history.");
        Stored(current, "Patient", challenge.PatientId, present: true);
        Stored(current, "Observation", observation.Id, present: true);
        Stored(current, "Bundle", challenge.BundleId, present: received);
        Stored(current, "Patient", challenge.ContextId, present: false);
        Stored(current, "Observation", challenge.FocusId, present: false);
        if (!received)
        {
            Require(current.ReceiptKeyCount == 0 && current.ReceiptCount == 0 &&
                current.EventCount == 0 && current.ReceivedNotifications.Count == 0, "Receipt appeared before the notification.");
        }
    }

    /// <summary>Validates every list entry and event; a correct dictionary key alone is insufficient.</summary>
    public static void ValidateReceipt(TenantSnapshot snapshot, ProbeChallenge challenge, DateTimeOffset earliest, DateTimeOffset latest) =>
        ValidateReceipt(snapshot, [new ReceiptExpectation(new NotificationInput(challenge.FhirVersion, challenge.EventNumber,
            challenge.BundleId, challenge.StatusId, challenge.FocusId, challenge.ContextId, challenge.Notification), earliest, latest)]);

    /// <summary>Requires precisely the ordered receipt list, with one individually bound event per Bundle.</summary>
    public static void ValidateReceipt(TenantSnapshot snapshot, IReadOnlyList<ReceiptExpectation> expected)
    {
        string subscription = Source + "Subscription/perf-subscription";
        int keys = expected.Count == 0 ? 0 : 1;
        Require(snapshot.ReceiptKeyCount == keys && snapshot.ReceiptCount == expected.Count &&
            snapshot.EventCount == expected.Count && snapshot.ReceivedNotifications.Count == keys, "Incorrect receipt/key/event totals.");
        if (expected.Count == 0)
        {
            return;
        }

        Require(snapshot.ReceivedNotifications.TryGetValue(subscription, out ReceiptSnapshot[]? receipts) &&
            receipts.Length == expected.Count, "Missing or duplicated registered receipt.");
        HashSet<string> bundles = new(StringComparer.Ordinal);
        HashSet<string> statuses = new(StringComparer.Ordinal);
        long previous = 0;
        for (int index = 0; index < expected.Count; index++)
        {
            ReceiptExpectation item = expected[index];
            ValidateNotification(item.Notification);
            Require(bundles.Add(item.Notification.BundleId) && statuses.Add(item.Notification.StatusId) &&
                item.Notification.EventNumber > previous, "Expected notification identities/events are not unique and increasing.");
            ValidateReceiptEntry(receipts![index], item.Notification, item.Earliest, item.Latest);
            previous = item.Notification.EventNumber;
        }
    }

    private static void ValidateReceiptEntry(
        ReceiptSnapshot receipt, NotificationInput challenge, DateTimeOffset earliest, DateTimeOffset latest)
    {
        string subscription = Source + "Subscription/perf-subscription";
        Equal(challenge.BundleId, receipt.BundleId, "receipt Bundle ID");
        Equal(subscription, receipt.SubscriptionReference, "receipt subscription");
        Equal(Topic, receipt.SubscriptionTopicCanonical, "receipt topic");
        Equal("active", receipt.Status, "receipt status");
        Equal("EventNotification", receipt.NotificationType, "parsed notification type");
        Require(receipt.EventsSinceSubscriptionStart == challenge.EventNumber, "Wrong receipt event total.");
        Require(receipt.ProcessedDateTime >= earliest && receipt.ProcessedDateTime <= latest.AddSeconds(1), "Wrong receipt processing time.");
        Require(receipt.Errors.Length == 0 && receipt.NotificationEvents.Length == 1, "Missing event or receipt errors.");
        NotificationEventSnapshot notificationEvent = receipt.NotificationEvents[0];
        Equal(string.Empty, notificationEvent.Id, "unassigned event backbone ID");
        Require(notificationEvent.EventNumber == challenge.EventNumber, "Wrong parsed event number.");
        Require(notificationEvent.Timestamp == DateTimeOffset.Parse(Timestamp, CultureInfo.InvariantCulture), "Wrong event timestamp.");
        Equal(Source + "Observation/" + challenge.FocusId, notificationEvent.FocusReference, "parsed focus");
        Require(notificationEvent.AdditionalContextReferences.SequenceEqual(new[] { Source + "Patient/" + challenge.ContextId }),
            "Missing, duplicated or mismatched additional context.");
    }

    private static void ValidateSourceRegistration(TenantSnapshot snapshot, string expectedLoadDirectory)
    {
        Require(!string.IsNullOrEmpty(expectedLoadDirectory) && Path.IsPathFullyQualified(expectedLoadDirectory),
            "Missing absolute expected empty-source path.");
        string expected = Path.GetFullPath(expectedLoadDirectory);
        Require(snapshot.Configuration.TryGetValue("LoadDirectory", out string? configured) &&
            string.Equals(configured, expected, StringComparison.Ordinal),
            "Effective data source is not the expected owned empty directory.");
        // Init registers LoadDirectory.FullName before enumerating files, including an empty source.
        Require(snapshot.LoadedSupplements.Length == 1 &&
            string.Equals(snapshot.LoadedSupplements[0], expected, StringComparison.Ordinal),
            "Expected exactly one owned empty-source registration.");
    }

    private static void Stored(TenantSnapshot snapshot, string type, string id, bool present)
    {
        Require(snapshot.RequestedResources.TryGetValue(type + "/" + id, out ResourceIdentity? identity), "Identity was not inspected.");
        if (present)
        {
            Require(identity is not null, "Missing probe-observed stored resource.");
            Equal(type, identity!.ResourceType, "stored type");
            Equal(id, identity.Id, "stored ID");
        }
        else
        {
            Require(identity is null, "Embedded content was unexpectedly stored separately.");
        }
    }

    private static void ValidateStatusAndMediaType(ResponseEvidence response, int expectedStatus)
    {
        Require(response.StatusCode == expectedStatus, $"Expected HTTP {expectedStatus}, got {response.StatusCode}.");
        Equal("application/fhir+json", Header(response, "Content-Type").Split(';')[0], "FHIR media type");
        Require(response.Body.Length > 0, "Missing materialized response.");
        Require(response.CompletedUtc >= response.StartedUtc, "Invalid response interval.");
    }

    private static string Header(ResponseEvidence response, string name)
    {
        KeyValuePair<string, string[]>[] matches = response.Headers.Where(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        Require(matches.Length == 1 && matches[0].Value.Length == 1, "Missing or ambiguous response header: " + name);
        return matches[0].Value[0];
    }

    private static void Parameter(JsonElement parameter, string name, string primitive, string expected, bool reference = false)
    {
        Fields(parameter, ["name", primitive]);
        Equal(name, Text(parameter, "name"), "parameter name");
        if (reference)
        {
            Reference(Property(parameter, primitive), expected);
        }
        else
        {
            Equal(expected, Text(parameter, primitive), "parameter primitive");
        }
    }

    private static void Reference(JsonElement reference, string expected)
    {
        Fields(reference, ["reference"]);
        Equal(expected, Text(reference, "reference"), "reference relationship");
    }

    private static void Profile(JsonElement meta, string expected)
    {
        Fields(meta, ["profile"]);
        JsonElement profiles = Property(meta, "profile");
        Require(profiles.GetArrayLength() == 1 && profiles[0].GetString() == expected, "Wrong backport profile.");
    }

    private static string PatientContract(string? id, int index = 0, string? identifier = null, bool active = true)
    {
        string idProperty = id is null ? string.Empty : "\"id\":\"" + id + "\",";
        return $$$"""
            {"resourceType":"Patient",{{{idProperty}}}
             "identifier":[{"system":"https://example.org/candle-perf/patient","value":"{{{identifier ?? id}}}"}],
             "active":{{{(active ? "true" : "false")}}},
             "name":[{"use":"official","family":"Family{{{(index % 100).ToString("D3", CultureInfo.InvariantCulture)}}}",
             "given":["Given{{{index.ToString("D8", CultureInfo.InvariantCulture)}}}"]}],
             "gender":"unknown","birthDate":"1980-01-02"}
            """;
    }

    private static string ObservationContract(
        string? id, string subject, int index = 0, string? identifier = null,
        decimal quantity = 37.5m, string effective = "2025-01-02T03:04:05Z")
    {
        string idProperty = id is null ? string.Empty : "\"id\":\"" + id + "\",";
        return $$$"""
            {"resourceType":"Observation",{{{idProperty}}}"status":"final",
             "identifier":[{"system":"https://example.org/candle-perf/observation","value":"{{{identifier ?? id ?? "observation-00000000"}}}"}],
             "code":{"coding":[{"system":"https://example.org/candle-perf/code",
             "code":"Code{{{(index % 1000).ToString("D3", CultureInfo.InvariantCulture)}}}","display":"Synthetic measurement"}]},
             "subject":{"reference":"{{{subject}}}"},"effectiveDateTime":"{{{effective}}}",
             "valueQuantity":{"value":{{{quantity.ToString(CultureInfo.InvariantCulture)}}},
             "unit":"synthetic-unit","system":"https://example.org/candle-perf/unit","code":"unit"}}
            """;
    }

    private static void ValidatePermutation(int[] order, int count) =>
        Require(order.Length == count && order.Order().SequenceEqual(Enumerable.Range(0, count)), "Invalid corpus request permutation.");

    private static Uri SearchAddress(string value, string currentUri, string baseUrl)
    {
        Uri current = new(currentUri, UriKind.Absolute);
        Uri root = new(baseUrl, UriKind.Absolute);
        Require(Uri.TryCreate(current, value, out Uri? address) && address.Scheme == root.Scheme &&
            address.Authority == root.Authority && address.AbsolutePath == current.AbsolutePath &&
            address.AbsolutePath.StartsWith(root.AbsolutePath + "/", StringComparison.Ordinal) &&
            (address.AbsolutePath[(root.AbsolutePath.Length + 1)..] is "Patient" or "Observation") &&
            string.IsNullOrEmpty(address.Fragment) && string.IsNullOrEmpty(address.UserInfo),
            "Search continuation escaped the addressed loopback tenant/resource.");
        return address!;
    }

    private static bool SameSearchUri(string left, string right)
    {
        Uri first = new(left, UriKind.Absolute);
        Uri second = new(right, UriKind.Absolute);
        return first.GetLeftPart(UriPartial.Path) == second.GetLeftPart(UriPartial.Path) &&
            string.IsNullOrEmpty(first.Fragment) && string.IsNullOrEmpty(second.Fragment) &&
            QueryParts(first).SequenceEqual(QueryParts(second), StringComparer.Ordinal);
    }

    private static string[] QueryParts(Uri uri) => uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => Uri.UnescapeDataString(part.Replace("+", " ", StringComparison.Ordinal)))
        .Order(StringComparer.Ordinal).ToArray();

    private static JsonElement Parse(string text) => Parse(Encoding.UTF8.GetBytes(text));

    private static JsonElement Parse(byte[] bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
        return document.RootElement.Clone();
    }

    private static JsonElement Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value)
            ? value : throw new InvalidDataException("Missing JSON property: " + name);

    private static string Text(JsonElement element, string name)
    {
        JsonElement value = Property(element, name);
        return value.ValueKind == JsonValueKind.String ? value.GetString()!
            : throw new InvalidDataException("Expected string primitive: " + name);
    }

    private static void Fields(JsonElement element, string[] expected)
    {
        Require(element.ValueKind == JsonValueKind.Object, "Expected JSON object.");
        string[] names = element.EnumerateObject().Select(property => property.Name).ToArray();
        Require(names.Length == expected.Length && names.Distinct(StringComparer.Ordinal).Count() == names.Length &&
            names.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)), "Unexpected or duplicated JSON fields.");
    }

    private static void CompareJson(JsonElement expected, JsonElement actual, string path)
    {
        Require(expected.ValueKind == actual.ValueKind, "JSON kind mismatch: " + path);
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                JsonProperty[] properties = expected.EnumerateObject().ToArray();
                Fields(actual, properties.Select(property => property.Name).ToArray());
                foreach (JsonProperty property in properties)
                {
                    CompareJson(property.Value, Property(actual, property.Name), path + "." + property.Name);
                }

                break;
            case JsonValueKind.Array:
                Require(expected.GetArrayLength() == actual.GetArrayLength(), "Array length mismatch: " + path);
                for (int index = 0; index < expected.GetArrayLength(); index++)
                {
                    CompareJson(expected[index], actual[index], path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]");
                }

                break;
            case JsonValueKind.Number:
                Require(expected.GetDecimal() == actual.GetDecimal(), "Numeric content mismatch: " + path);
                break;
            case JsonValueKind.String:
                Equal(expected.GetString()!, actual.GetString()!, path);
                break;
            default:
                Equal(expected.GetRawText(), actual.GetRawText(), path);
                break;
        }
    }

    private static bool IsFhirId(string id) => (id.Length is >= 1 and <= 64) &&
        id.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.');

    private static void Equal(string expected, string actual, string label) =>
        Require(string.Equals(expected, actual, StringComparison.Ordinal), "Mismatched " + label + ".");

    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}

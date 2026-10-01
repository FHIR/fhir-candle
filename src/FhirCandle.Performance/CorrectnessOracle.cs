// <copyright file="CorrectnessOracle.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

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

/// <summary>Independent JSON and public-state assertions; none of these checks are timed workloads.</summary>
public static class CorrectnessOracle
{
    private const string Source = "https://example.org/fhir/source/";
    private const string Topic = "https://example.org/candle-perf/SubscriptionTopic/observation";
    private const string Timestamp = "2025-01-02T03:04:05+00:00";
    private const string Backport = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/";

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
    public static void ValidateNotification(ProbeChallenge challenge)
    {
        Require(challenge.FhirVersion is "R4" or "R4B" or "R5", "Unsupported notification FHIR version.");
        JsonElement bundle = Parse(challenge.Notification);
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
        JsonElement actual = Parse(response.Body);
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
        Equal("1", Text(meta, "versionId"), "first stored version");
        string lastUpdatedText = Text(meta, "lastUpdated");
        DateTimeOffset lastUpdated = DateTimeOffset.Parse(lastUpdatedText, CultureInfo.InvariantCulture);
        Require((lastUpdated >= createdSinceUtc) && (lastUpdated <= response.CompletedUtc.AddSeconds(1)), "Unbound server timestamp.");
        if (binding is not null)
        {
            Require(lastUpdated == binding.LastUpdated, "Persisted metadata changed without an update.");
            Equal(binding.VersionId, Text(meta, "versionId"), "persisted version");
        }

        Equal(baseUrl + "/" + type + "/" + id, Header(response, "Location"), "Location/body identity");
        Equal("W/\"1\"", Header(response, "ETag"), "ETag/body version");
        DateTimeOffset modified = DateTimeOffset.Parse(Header(response, "Last-Modified"), CultureInfo.InvariantCulture);
        Require(modified.ToUnixTimeSeconds() == lastUpdated.ToUnixTimeSeconds(), "Last-Modified/body timestamp mismatch.");

        expected["id"] = id;
        JsonObject expectedMeta = expected["meta"]?.AsObject() ?? new JsonObject();
        if (expected["meta"] is null)
        {
            expected["meta"] = expectedMeta;
        }

        expectedMeta["versionId"] = "1";
        expectedMeta["lastUpdated"] = lastUpdatedText;
        CompareJson(Parse(expected.ToJsonString()), actual, "complete returned content");
        return new ResourceBinding(type, id, "1", lastUpdated);
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
    public static void ValidateReceipt(TenantSnapshot snapshot, ProbeChallenge challenge, DateTimeOffset earliest, DateTimeOffset latest)
    {
        string subscription = Source + "Subscription/perf-subscription";
        Require(snapshot.ReceiptKeyCount == 1 && snapshot.ReceiptCount == 1 &&
            snapshot.EventCount == 1 && snapshot.ReceivedNotifications.Count == 1, "Incorrect receipt/key/event totals.");
        Require(snapshot.ReceivedNotifications.TryGetValue(subscription, out ReceiptSnapshot[]? receipts) &&
            receipts.Length == 1, "Missing or duplicated registered receipt.");
        ReceiptSnapshot receipt = receipts![0];
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

    private static string PatientContract(string id) => $$$"""
        {"resourceType":"Patient","id":"{{{id}}}",
         "identifier":[{"system":"https://example.org/candle-perf/patient","value":"{{{id}}}"}],
         "active":true,"name":[{"use":"official","family":"Family000","given":["Given00000000"]}],
         "gender":"unknown","birthDate":"1980-01-02"}
        """;

    private static string ObservationContract(string? id, string subject)
    {
        string idProperty = id is null ? string.Empty : "\"id\":\"" + id + "\",";
        return $$$"""
            {"resourceType":"Observation",{{{idProperty}}}"status":"final",
             "identifier":[{"system":"https://example.org/candle-perf/observation","value":"{{{id ?? "observation-00000000"}}}"}],
             "code":{"coding":[{"system":"https://example.org/candle-perf/code","code":"Code000","display":"Synthetic measurement"}]},
             "subject":{"reference":"{{{subject}}}"},"effectiveDateTime":"2025-01-02T03:04:05Z",
             "valueQuantity":{"value":37.5,"unit":"synthetic-unit","system":"https://example.org/candle-perf/unit","code":"unit"}}
            """;
    }

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

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}

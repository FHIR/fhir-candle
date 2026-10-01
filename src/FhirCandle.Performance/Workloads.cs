// <copyright file="Workloads.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FhirCandle.Performance;

/// <summary>Deterministic input bytes for an untimed, disposable tenant-identity challenge.</summary>
public sealed record ProbeChallenge(
    string FhirVersion,
    long EventNumber,
    string PatientId,
    string BundleId,
    string StatusId,
    string FocusId,
    string ContextId,
    byte[] Patient,
    byte[] Observation,
    byte[] Notification);

/// <summary>BCL-only input generation; no product or Firely serializers are involved.</summary>
public static class Workloads
{
    /// <summary>The algorithm identity reserved for the full workload ledger.</summary>
    public const string Algorithm = "candle-perf-v1";

    /// <summary>The request-order seed reserved for the later full corpus.</summary>
    public const int Seed = 100101;

    /// <summary>Produces a PUT Patient, ID-less POST Observation, and a separate full-content event.</summary>
    public static ProbeChallenge CreateProbeChallenge(string fhirVersion, long eventNumber = 1)
    {
        if ((fhirVersion is not ("R4" or "R4B" or "R5")) || (eventNumber < 1) || (eventNumber > 99999999))
        {
            throw new ArgumentOutOfRangeException(nameof(fhirVersion), "Expected R4/R4B/R5 and an eight-digit positive event number.");
        }

        string suffix = eventNumber.ToString("D8", CultureInfo.InvariantCulture);
        string patientId = "patient-00000000";
        string bundleId = "notification-" + suffix;
        string statusId = "00000000-0000-4000-8000-" + eventNumber.ToString("D12", CultureInfo.InvariantCulture);
        string focusId = "notification-observation-" + suffix;
        string contextId = "notification-patient-" + suffix;
        byte[] patient = JsonSerializer.SerializeToUtf8Bytes(Patient(patientId));
        byte[] observation = JsonSerializer.SerializeToUtf8Bytes(Observation(null, "Patient/" + patientId));
        byte[] notification = CreateNotification(fhirVersion, eventNumber, bundleId, statusId, focusId, contextId);
        return new ProbeChallenge(fhirVersion, eventNumber, patientId, bundleId, statusId, focusId, contextId,
            patient, observation, notification);
    }

    private static JsonObject Patient(string id) => new()
    {
        ["resourceType"] = "Patient",
        ["id"] = id,
        ["identifier"] = new JsonArray(new JsonObject
        {
            ["system"] = "https://example.org/candle-perf/patient",
            ["value"] = id,
        }),
        ["active"] = true,
        ["name"] = new JsonArray(new JsonObject
        {
            ["use"] = "official",
            ["family"] = "Family000",
            ["given"] = new JsonArray("Given00000000"),
        }),
        ["gender"] = "unknown",
        ["birthDate"] = "1980-01-02",
    };

    private static JsonObject Observation(string? id, string subject)
    {
        JsonObject observation = new()
        {
            ["resourceType"] = "Observation",
            ["status"] = "final",
            ["identifier"] = new JsonArray(new JsonObject
            {
                ["system"] = "https://example.org/candle-perf/observation",
                ["value"] = id ?? "observation-00000000",
            }),
            ["code"] = new JsonObject
            {
                ["coding"] = new JsonArray(new JsonObject
                {
                    ["system"] = "https://example.org/candle-perf/code",
                    ["code"] = "Code000",
                    ["display"] = "Synthetic measurement",
                }),
            },
            ["subject"] = new JsonObject { ["reference"] = subject },
            ["effectiveDateTime"] = "2025-01-02T03:04:05Z",
            ["valueQuantity"] = new JsonObject
            {
                ["value"] = 37.5m,
                ["unit"] = "synthetic-unit",
                ["system"] = "https://example.org/candle-perf/unit",
                ["code"] = "unit",
            },
        };
        if (id is not null)
        {
            observation["id"] = id;
        }

        return observation;
    }

    private static byte[] CreateNotification(
        string version, long number, string bundleId, string statusId, string focusId, string contextId)
    {
        const string Source = "https://example.org/fhir/source/";
        const string Topic = "https://example.org/candle-perf/SubscriptionTopic/observation";
        const string Backport = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/";
        string subscription = Source + "Subscription/perf-subscription";
        string focus = Source + "Observation/" + focusId;
        string context = Source + "Patient/" + contextId;
        string count = number.ToString(CultureInfo.InvariantCulture);
        JsonObject status;
        if (version == "R4")
        {
            status = new JsonObject
            {
                ["resourceType"] = "Parameters",
                ["id"] = statusId,
                ["meta"] = new JsonObject { ["profile"] = new JsonArray(Backport + "backport-subscription-status-r4") },
                ["parameter"] = new JsonArray(
                    ReferenceParameter("subscription", subscription),
                    new JsonObject { ["name"] = "topic", ["valueCanonical"] = Topic },
                    new JsonObject { ["name"] = "status", ["valueCode"] = "active" },
                    new JsonObject { ["name"] = "type", ["valueCode"] = "event-notification" },
                    new JsonObject { ["name"] = "events-since-subscription-start", ["valueString"] = count },
                    new JsonObject
                    {
                        ["name"] = "notification-event",
                        ["part"] = new JsonArray(
                            new JsonObject { ["name"] = "event-number", ["valueString"] = count },
                            new JsonObject { ["name"] = "timestamp", ["valueInstant"] = "2025-01-02T03:04:05+00:00" },
                            ReferenceParameter("focus", focus),
                            ReferenceParameter("additional-context", context)),
                    }),
            };
        }
        else
        {
            // R4B SubscriptionStatus uses string counts too; R5 integer64 JSON is string-encoded.
            status = new JsonObject
            {
                ["resourceType"] = "SubscriptionStatus",
                ["id"] = statusId,
                ["status"] = "active",
                ["type"] = "event-notification",
                ["eventsSinceSubscriptionStart"] = count,
                ["notificationEvent"] = new JsonArray(new JsonObject
                {
                    ["eventNumber"] = count,
                    ["timestamp"] = "2025-01-02T03:04:05+00:00",
                    ["focus"] = new JsonObject { ["reference"] = focus },
                    ["additionalContext"] = new JsonArray(new JsonObject { ["reference"] = context }),
                }),
                ["subscription"] = new JsonObject { ["reference"] = subscription },
                ["topic"] = Topic,
            };
        }

        JsonObject bundle = new()
        {
            ["resourceType"] = "Bundle",
            ["id"] = bundleId,
            ["type"] = version == "R5" ? "subscription-notification" : "history",
            ["timestamp"] = "2025-01-02T03:04:05+00:00",
            ["entry"] = new JsonArray(
                Entry(version, "urn:uuid:" + statusId, status, "GET", "Subscription/perf-subscription/$status"),
                Entry(version, focus, Observation(focusId, context), "PUT", "Observation/" + focusId),
                Entry(version, context, Patient(contextId), "PUT", "Patient/" + contextId)),
        };
        if (version != "R5")
        {
            bundle["meta"] = new JsonObject
            {
                ["profile"] = new JsonArray(Backport + (version == "R4"
                    ? "backport-subscription-notification-r4" : "backport-subscription-notification")),
            };
        }

        return JsonSerializer.SerializeToUtf8Bytes(bundle);
    }

    private static JsonObject ReferenceParameter(string name, string reference) => new()
    {
        ["name"] = name,
        ["valueReference"] = new JsonObject { ["reference"] = reference },
    };

    private static JsonObject Entry(string version, string fullUrl, JsonObject resource, string method, string url)
    {
        JsonObject entry = new() { ["fullUrl"] = fullUrl, ["resource"] = resource };
        if (version != "R5")
        {
            // History entries carry request and response components (bdl-3 / bdl-4).
            entry["request"] = new JsonObject { ["method"] = method, ["url"] = url };
            entry["response"] = new JsonObject { ["status"] = "200" };
        }

        return entry;
    }
}

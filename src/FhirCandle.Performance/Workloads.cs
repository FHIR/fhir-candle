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

/// <summary>One deterministic clinical input, without server-assigned metadata.</summary>
public sealed record CorpusResource(string ResourceType, string Id, byte[] Body)
{
    /// <summary>The ordinary instance URL relative to the tenant.</summary>
    public string Identity => ResourceType + "/" + Id;
}

/// <summary>Logical-index-ordered resources and frozen, dependency-safe request permutations.</summary>
public sealed record WorkloadCorpus(
    string FhirVersion, int PatientCount, int ObservationCount, CorpusResource[] Resources,
    int[] PatientOrder, int[] ObservationOrder)
{
    /// <summary>The version/scale fixture identity, shared by twelve independent case processes.</summary>
    public string Key => FhirVersion + "-" + PatientCount.ToString(CultureInfo.InvariantCulture) +
        "p-" + ObservationCount.ToString(CultureInfo.InvariantCulture) + "o";
}

/// <summary>One of the seventy-two paired rows, not a variant or a measured trial.</summary>
public sealed record WorkloadCase(string CaseId, string FhirVersion, int PatientCount, int ObservationCount)
{
    /// <summary>The stable, filesystem-safe full row identity.</summary>
    public string RowKey => FhirVersion + "-" + PatientCount.ToString(CultureInfo.InvariantCulture) +
        "p-" + ObservationCount.ToString(CultureInfo.InvariantCulture) + "o-" + CaseId;
}

/// <summary>A single full-content event with independently checkable wire identities.</summary>
public sealed record NotificationInput(
    string FhirVersion, long EventNumber, string BundleId, string StatusId, string FocusId, string ContextId, byte[] Body);

/// <summary>A pre-generated logical operation. Search continuations are additional HTTP requests.</summary>
public sealed record WorkloadRequest(
    int Number, string Method, string RelativeUrl, byte[]? Body, string? Prefer,
    string[] ExpectedIdentities, NotificationInput? Notification = null);

/// <summary>All actual-case inputs are generated once before either warm-up or counter windows.</summary>
public sealed record TrialInputs(WorkloadRequest[] Warmup, WorkloadRequest[] Measured, WorkloadRequest? Restore);

/// <summary>BCL-only input generation; no product or Firely serializers are involved.</summary>
public static class Workloads
{
    /// <summary>The frozen clinical and notification input algorithm.</summary>
    public const string Algorithm = "candle-perf-v1";

    /// <summary>The request-order seed, with an explicitly specified 32-bit shuffle.</summary>
    public const int Seed = 100101;

    /// <summary>The clock-aware evidence contract; clinical algorithm, bytes, seed and request order are unchanged.</summary>
    public const string ContractVersion = "candle-perf-preflight-v3";

    /// <summary>Exactly four logical operations per untimed case, with no measured warm-up.</summary>
    public const int PreflightOperations = 4;

    /// <summary>Exactly this many actual-case operations precede every pilot and accepted trial.</summary>
    public const int WarmupOperations = 128;

    private static readonly string[] _caseIds =
    [
        "patient-create", "observation-create", "patient-update", "observation-update",
        "patient-token", "patient-string", "observation-token", "observation-reference",
        "empty-search", "patient-read", "observation-read", "subscription-receive",
    ];

    /// <summary>Creates the exact requested population; every ten Observations share one existing Patient.</summary>
    public static WorkloadCorpus CreateCorpus(string fhirVersion, int patientCount)
    {
        ValidateVersion(fhirVersion);
        if (patientCount is not (100 or 1000))
        {
            throw new ArgumentOutOfRangeException(nameof(patientCount), "Only the two contracted clinical scales are supported.");
        }

        int observationCount = patientCount * 10;
        CorpusResource[] resources = new CorpusResource[patientCount + observationCount];
        for (int index = 0; index < patientCount; index++)
        {
            string id = PatientId(index);
            resources[index] = new CorpusResource("Patient", id, JsonSerializer.SerializeToUtf8Bytes(Patient(id, index)));
        }

        for (int index = 0; index < observationCount; index++)
        {
            string id = ObservationId(index);
            resources[patientCount + index] = new CorpusResource("Observation", id,
                JsonSerializer.SerializeToUtf8Bytes(Observation(id, "Patient/" + PatientId(index / 10), index)));
        }

        uint state = Seed;
        int[] patientOrder = Shuffle(patientCount, ref state);
        int[] observationOrder = Shuffle(observationCount, ref state);
        return new WorkloadCorpus(fhirVersion, patientCount, observationCount, resources, patientOrder, observationOrder);
    }

    /// <summary>Patients are seeded first; each population follows its frozen permutation.</summary>
    public static IEnumerable<CorpusResource> SeedResources(WorkloadCorpus corpus) =>
        corpus.PatientOrder.Select(index => corpus.Resources[index]).Concat(
            corpus.ObservationOrder.Select(index => corpus.Resources[corpus.PatientCount + index]));

    /// <summary>Version, scale, then case order defines exactly seventy-two paired rows.</summary>
    public static WorkloadCase[] CreateCases()
    {
        List<WorkloadCase> cases = [];
        foreach (string version in new[] { "R4", "R4B", "R5" })
        {
            foreach (int patients in new[] { 100, 1000 })
            {
                cases.AddRange(_caseIds.Select(id => new WorkloadCase(id, version, patients, patients * 10)));
            }
        }

        return cases.ToArray();
    }

    /// <summary>Generates immutable batch inputs before any future operation window.</summary>
    public static WorkloadRequest[] CreateRequests(
        WorkloadCase workload, WorkloadCorpus corpus, int count = PreflightOperations, int offset = 0)
    {
        if (!_caseIds.Contains(workload.CaseId, StringComparer.Ordinal) ||
            workload.FhirVersion != corpus.FhirVersion || workload.PatientCount != corpus.PatientCount ||
            workload.ObservationCount != corpus.ObservationCount || count < 1 || offset < 0 ||
            (long)offset + count > 99999999)
        {
            throw new ArgumentException("Invalid case, corpus or fixed operation range.", nameof(workload));
        }

        WorkloadRequest[] requests = new WorkloadRequest[count];
        for (int index = 0; index < count; index++)
        {
            int number = offset + index + 1;
            int patientIndex = corpus.PatientOrder[(number - 1) % corpus.PatientCount];
            int observationIndex = corpus.ObservationOrder[(number - 1) % corpus.ObservationCount];
            string patient = PatientId(patientIndex);
            string observation = ObservationId(observationIndex);
            string suffix = number.ToString("D8", CultureInfo.InvariantCulture);
            switch (workload.CaseId)
            {
                case "patient-create":
                    requests[index] = new(number, "POST", "Patient", JsonSerializer.SerializeToUtf8Bytes(
                        Patient(null, number, "created-patient-" + suffix)), "return=representation", []);
                    break;
                case "observation-create":
                    requests[index] = new(number, "POST", "Observation", JsonSerializer.SerializeToUtf8Bytes(
                        Observation(null, "Patient/" + patient, number, "created-observation-" + suffix)),
                        "return=representation", []);
                    break;
                case "patient-update":
                    patientIndex = corpus.PatientOrder[0];
                    patient = PatientId(patientIndex);
                    requests[index] = new(number, "PUT", "Patient/" + patient, JsonSerializer.SerializeToUtf8Bytes(
                        Patient(patient, patientIndex, active: number % 2 == 0)), "return=representation", ["Patient/" + patient]);
                    break;
                case "observation-update":
                    observationIndex = corpus.ObservationOrder[0];
                    observation = ObservationId(observationIndex);
                    requests[index] = new(number, "PUT", "Observation/" + observation, JsonSerializer.SerializeToUtf8Bytes(
                        Observation(observation, "Patient/" + PatientId(observationIndex / 10), observationIndex,
                            quantity: number % 2 == 1 ? 38.5m : 39.5m,
                            effective: number % 2 == 1 ? "2025-01-02T03:04:06Z" : "2025-01-02T03:04:07Z")),
                        "return=representation", ["Observation/" + observation]);
                    break;
                case "patient-token":
                    requests[index] = Search(number, "Patient", "identifier",
                        "https://example.org/candle-perf/patient|" + patient, ["Patient/" + patient]);
                    break;
                case "patient-string":
                    requests[index] = Search(number, "Patient", "family", "Family017",
                        Enumerable.Range(0, corpus.PatientCount).Where(value => value % 100 == 17)
                            .Select(value => "Patient/" + PatientId(value)).ToArray());
                    break;
                case "observation-token":
                    requests[index] = Search(number, "Observation", "code", "https://example.org/candle-perf/code|Code017",
                        Enumerable.Range(0, corpus.ObservationCount).Where(value => value % 1000 == 17)
                            .Select(value => "Observation/" + ObservationId(value)).ToArray());
                    break;
                case "observation-reference":
                    patientIndex = corpus.PatientOrder[0];
                    requests[index] = Search(number, "Observation", "subject", "Patient/" + PatientId(patientIndex),
                        Enumerable.Range(patientIndex * 10, 10).Select(value => "Observation/" + ObservationId(value)).ToArray());
                    break;
                case "empty-search":
                    requests[index] = Search(number, "Observation", "code", "https://example.org/candle-perf/code|OutsideDomain", []);
                    break;
                case "patient-read":
                    requests[index] = new(number, "GET", "Patient/" + patient, null, null, ["Patient/" + patient]);
                    break;
                case "observation-read":
                    requests[index] = new(number, "GET", "Observation/" + observation, null, null, ["Observation/" + observation]);
                    break;
                case "subscription-receive":
                    NotificationInput notification = CreateNotification(workload.FhirVersion, number);
                    requests[index] = new(number, "POST", "$subscription-hook", notification.Body,
                        "return=OperationOutcome", ["Bundle/" + notification.BundleId], notification);
                    break;
                default:
                    throw new InvalidDataException("Unknown workload case.");
            }
        }

        return requests;
    }

    /// <summary>Uses disjoint create/event numbers and preserves the same update permutation/history on both sides.</summary>
    public static TrialInputs CreateTrialInputs(WorkloadCase workload, WorkloadCorpus corpus, int measuredCount)
    {
        WorkloadRequest[] warmup = CreateRequests(workload, corpus, WarmupOperations);
        WorkloadRequest[] measured = CreateRequests(workload, corpus, measuredCount, WarmupOperations);
        WorkloadRequest? restore = null;
        if (workload.CaseId.EndsWith("-update", StringComparison.Ordinal))
        {
            string identity = warmup[0].RelativeUrl;
            CorpusResource original = corpus.Resources.Single(resource => resource.Identity == identity);
            restore = new(0, "PUT", identity, original.Body, "return=representation", [identity]);
        }

        return new(warmup, measured, restore);
    }

    /// <summary>Creates a unique Bundle/status pair and an increasing, single full-content event.</summary>
    public static NotificationInput CreateNotification(string fhirVersion, long eventNumber = 1)
    {
        ValidateVersion(fhirVersion);
        if (eventNumber is < 1 or > 99999999)
        {
            throw new ArgumentOutOfRangeException(nameof(eventNumber));
        }

        string suffix = eventNumber.ToString("D8", CultureInfo.InvariantCulture);
        string bundle = "notification-" + suffix;
        string status = "00000000-0000-4000-8000-" + eventNumber.ToString("D12", CultureInfo.InvariantCulture);
        string focus = "notification-observation-" + suffix;
        string context = "notification-patient-" + suffix;
        return new(fhirVersion, eventNumber, bundle, status, focus, context,
            CreateNotification(fhirVersion, eventNumber, bundle, status, focus, context));
    }

    /// <summary>Produces a PUT Patient, ID-less POST Observation, and a separate full-content event.</summary>
    public static ProbeChallenge CreateProbeChallenge(string fhirVersion, long eventNumber = 1)
    {
        NotificationInput notification = CreateNotification(fhirVersion, eventNumber);
        string patientId = "patient-00000000";
        byte[] patient = JsonSerializer.SerializeToUtf8Bytes(Patient(patientId));
        byte[] observation = JsonSerializer.SerializeToUtf8Bytes(Observation(null, "Patient/" + patientId));
        return new ProbeChallenge(fhirVersion, eventNumber, patientId, notification.BundleId, notification.StatusId,
            notification.FocusId, notification.ContextId, patient, observation, notification.Body);
    }

    private static JsonObject Patient(string? id, int index = 0, string? identifier = null, bool active = true)
    {
        JsonObject patient = new()
        {
            ["resourceType"] = "Patient",
        };
        if (id is not null)
        {
            patient["id"] = id;
        }

        patient["identifier"] = new JsonArray(new JsonObject
        {
            ["system"] = "https://example.org/candle-perf/patient", ["value"] = identifier ?? id,
        });
        patient["active"] = active;
        patient["name"] = new JsonArray(new JsonObject
        {
            ["use"] = "official",
            ["family"] = "Family" + (index % 100).ToString("D3", CultureInfo.InvariantCulture),
            ["given"] = new JsonArray("Given" + index.ToString("D8", CultureInfo.InvariantCulture)),
        });
        patient["gender"] = "unknown";
        patient["birthDate"] = "1980-01-02";
        return patient;
    }

    private static JsonObject Observation(
        string? id, string subject, int index = 0, string? identifier = null,
        decimal quantity = 37.5m, string effective = "2025-01-02T03:04:05Z")
    {
        JsonObject observation = new()
        {
            ["resourceType"] = "Observation",
            ["status"] = "final",
            ["identifier"] = new JsonArray(new JsonObject
            {
                ["system"] = "https://example.org/candle-perf/observation",
                ["value"] = identifier ?? id ?? "observation-00000000",
            }),
            ["code"] = new JsonObject
            {
                ["coding"] = new JsonArray(new JsonObject
                {
                    ["system"] = "https://example.org/candle-perf/code",
                    ["code"] = "Code" + (index % 1000).ToString("D3", CultureInfo.InvariantCulture),
                    ["display"] = "Synthetic measurement",
                }),
            },
            ["subject"] = new JsonObject { ["reference"] = subject },
            ["effectiveDateTime"] = effective,
            ["valueQuantity"] = new JsonObject
            {
                ["value"] = quantity,
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

    private static string PatientId(int index) => "patient-" + index.ToString("D8", CultureInfo.InvariantCulture);

    private static string ObservationId(int index) => "observation-" + index.ToString("D8", CultureInfo.InvariantCulture);

    private static WorkloadRequest Search(int number, string type, string parameter, string value, string[] identities) =>
        new(number, "GET", type + "?" + parameter + "=" + Uri.EscapeDataString(value) + "&_count=100", null, null, identities);

    private static int[] Shuffle(int count, ref uint state)
    {
        int[] order = Enumerable.Range(0, count).ToArray();
        for (int index = count - 1; index > 0; index--)
        {
            state = unchecked((1664525U * state) + 1013904223U);
            int other = (int)(state % (uint)(index + 1));
            (order[index], order[other]) = (order[other], order[index]);
        }

        return order;
    }

    private static void ValidateVersion(string version)
    {
        if (version is not ("R4" or "R4B" or "R5"))
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Expected R4, R4B or R5.");
        }
    }
}

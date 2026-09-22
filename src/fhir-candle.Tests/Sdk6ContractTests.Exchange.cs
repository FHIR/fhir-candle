// <copyright file="Sdk6ContractTests.Exchange.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;

namespace fhir.candle.Tests;

public partial class Sdk6ContractTests
{
    private static readonly XNamespace _exchangeFhir = "http://hl7.org/fhir";
    private const string ExchangeEvidence =
        "FhirStoreTests.cs: TestPatientCRUD, TestResourceWrongLocation, TestResourceInvalidElement, " +
        "TestDuplicateExplicitIdCreate, TestPostPutIdSemantics, TestStrictModeIdSemantics, " +
        "TestLenientModeIdSemantics, TestConditionalControlParameters; R4Tests.cs: R4TestConditionals, " +
        "R4TestConditionalUpdates, R4TestConditionalCreateViaUrlQuery, R4TestBundleConditionals, R4TestsSummary; " +
        "docs/user/strict-mode.md; data/sdk6-compatibility/README.md";

    /// <summary>Every exchange case names its version, boundary, and same-format oracle.</summary>
    public static IEnumerable<object[]> ExchangeCases
    {
        get
        {
            foreach (object[] row in ReplayCases)
            {
                yield return [$"exchange / {row[1]} / {row[2]} / {row[3]}", row[1], row[2], row[3]];
            }
        }
    }

    /// <summary>Strict tenants are separate replays, not lenient assertions with relaxed statuses.</summary>
    public static IEnumerable<object[]> ExchangePolicyCases
    {
        get
        {
            foreach (object[] row in ExchangeCases)
            {
                foreach (bool strict in new[] { false, true })
                {
                    yield return [$"{row[0]} / {(strict ? "strict" : "default")}", row[1], row[2], row[3], strict];
                }
            }
        }
    }

    /// <summary>The explicit-ID override exists on the store interface, not as an HTTP header.</summary>
    public static IEnumerable<object[]> ExchangeExplicitCreateCases =>
        ExchangePolicyCases.Where(row => (CompatibilityTransport)row[2] == CompatibilityTransport.Direct);

    /// <summary>Both existing bundle processors receive independent JSON and XML requests.</summary>
    public static IEnumerable<object[]> ExchangeBundleCases
    {
        get
        {
            foreach (object[] row in ExchangeCases)
            {
                foreach (string type in new[] { "batch", "transaction" })
                {
                    yield return [$"{row[0]} / {type}", row[1], row[2], row[3], type];
                }
            }
        }
    }

    /// <summary>Separate rows ensure a failed summary does not prevent the other format cases running.</summary>
    public static IEnumerable<object[]> ExchangeSerializationCases
    {
        get
        {
            foreach (object[] row in ExchangeCases)
            {
                foreach (string mode in new[]
                {
                    "summary-false", "summary-true", "summary-text", "summary-data", "summary-count",
                    "pretty", "mime-alias", "mime-parameters", "tolerated-code", "input-fallback", "output-fallback",
                })
                {
                    yield return [$"{row[0]} / {mode}", row[1], row[2], row[3], mode];
                }

                string boundaryMode = (CompatibilityTransport)row[2] == CompatibilityTransport.Http
                    ? "negotiation"
                    : "short-aliases";
                yield return [$"{row[0]} / {boundaryMode}", row[1], row[2], row[3], boundaryMode];
            }
        }
    }

    /// <summary>Cross-format expectations come from the other authored fixture, never the SDK output.</summary>
    public static IEnumerable<object[]> ExchangeConversionCases
    {
        get
        {
            foreach (object[] row in ExchangeCases)
            {
                foreach (string resourceType in new[] { "Patient", "Observation", "Binary" })
                {
                    yield return [$"{row[0]} / {resourceType}", row[1], row[2], row[3], resourceType];
                }
            }
        }
    }

    /// <summary>Independent failures exercise parsing, dispatch, and semantic location checks.</summary>
    public static IEnumerable<object[]> ExchangeInvalidCases
    {
        get
        {
            foreach (object[] row in ExchangeCases)
            {
                foreach (string failure in new[]
                {
                    "malformed", "invalid-resource-type", "wrong-create-location", "wrong-update-location",
                    "non-bundle", "invalid-primitive", "convert-malformed", "unknown-format-malformed",
                })
                {
                    yield return [$"{row[0]} / {failure}", row[1], row[2], row[3], failure];
                }
            }
        }
    }

    /// <summary>Hand-authored tag sequences, independent of Firely and of any observed response.</summary>
    public static IEnumerable<object[]> SummaryMetadataControls
    {
        get
        {
            foreach (CompatibilityFormat format in new[] { CompatibilityFormat.Json, CompatibilityFormat.Xml })
            {
                bool json = format == CompatibilityFormat.Json;
                string first = json
                    ? """{"system":"https://example.test/tags","code":"first","display":"Keep this display"}"""
                    : """<tag><system value="https://example.test/tags"/><code value="first"/><display value="Keep this display"/></tag>""";
                string second = json
                    ? """{"system":"https://example.test/tags","code":"second"}"""
                    : """<tag><system value="https://example.test/tags"/><code value="second"/></tag>""";
                string unrelated = json
                    ? """{"system":"https://example.test/unrelated","code":"SUBSETTED","display":"Original, not a native marker"}"""
                    : """<tag><system value="https://example.test/unrelated"/><code value="SUBSETTED"/><display value="Original, not a native marker"/></tag>""";
                string legacy = json
                    ? """{"system":"http://hl7.org/fhir/v3/ObservationValue","code":"SUBSETTED"}"""
                    : """<tag><system value="http://hl7.org/fhir/v3/ObservationValue"/><code value="SUBSETTED"/></tag>""";
                string current = json
                    ? """{"system":"http://terminology.hl7.org/CodeSystem/v3-ObservationValue","code":"SUBSETTED"}"""
                    : """<tag><system value="http://terminology.hl7.org/CodeSystem/v3-ObservationValue"/><code value="SUBSETTED"/></tag>""";
                string wrongSystem = json
                    ? """{"system":"https://example.test/wrong","code":"SUBSETTED"}"""
                    : """<tag><system value="https://example.test/wrong"/><code value="SUBSETTED"/></tag>""";
                string wrongCode = json
                    ? """{"system":"http://terminology.hl7.org/CodeSystem/v3-ObservationValue","code":"WRONG"}"""
                    : """<tag><system value="http://terminology.hl7.org/CodeSystem/v3-ObservationValue"/><code value="WRONG"/></tag>""";
                string extraMember = json
                    ? """{"system":"http://terminology.hl7.org/CodeSystem/v3-ObservationValue","code":"SUBSETTED","userSelected":true}"""
                    : """<tag><system value="http://terminology.hl7.org/CodeSystem/v3-ObservationValue"/><code value="SUBSETTED"/><userSelected value="true"/></tag>""";
                string changedOriginal = json
                    ? """{"system":"https://example.test/tags","code":"first","display":"Changed display"}"""
                    : """<tag><system value="https://example.test/tags"/><code value="first"/><display value="Changed display"/></tag>""";
                string addedDisplay = json
                    ? """{"system":"http://terminology.hl7.org/CodeSystem/v3-ObservationValue","code":"SUBSETTED","display":"Subsetted"}"""
                    : """<tag><system value="http://terminology.hl7.org/CodeSystem/v3-ObservationValue"/><code value="SUBSETTED"/><display value="Subsetted"/></tag>""";
                string primitiveMember = json
                    ? """{"system":"http://terminology.hl7.org/CodeSystem/v3-ObservationValue","_system":{"extension":[{"url":"https://example.test/extra","valueBoolean":true}]},"code":"SUBSETTED"}"""
                    : """<tag><system value="http://terminology.hl7.org/CodeSystem/v3-ObservationValue"><extension url="https://example.test/extra"><valueBoolean value="true"/></extension></system><code value="SUBSETTED"/></tag>""";
                string duplicateCode = json
                    ? """{"system":"http://terminology.hl7.org/CodeSystem/v3-ObservationValue","code":"SUBSETTED","code":"SUBSETTED"}"""
                    : """<tag><system value="http://terminology.hl7.org/CodeSystem/v3-ObservationValue"/><code value="SUBSETTED"/><code value="SUBSETTED"/></tag>""";

                (string Name, string[] Original, string[] Actual, bool Accepted)[] controls =
                [
                    ("native-pairs", [first], [first, legacy, current], true),
                    ("native-pairs-reversed", [first], [first, current, legacy], true),
                    ("unrelated-subsetted-code", [first, unrelated], [first, unrelated, legacy, current], true),
                    ("original-duplicates", [first, unrelated, first, second], [first, unrelated, first, second, current, legacy], true),
                    ("missing-both", [first], [first], false),
                    ("missing-legacy", [first], [first, current], false),
                    ("missing-current", [first], [first, legacy], false),
                    ("wrong-system", [first], [first, wrongSystem, current], false),
                    ("wrong-code", [first], [first, legacy, wrongCode], false),
                    ("repeated-legacy", [first], [first, legacy, legacy], false),
                    ("repeated-current", [first], [first, current, current], false),
                    ("repeated-pair", [first], [first, legacy, current, legacy, current], false),
                    ("extra-tag", [first], [first, legacy, current, second], false),
                    ("extra-native-member", [first], [first, legacy, extraMember], false),
                    ("changed-original", [first], [changedOriginal, legacy, current], false),
                    ("dropped-original", [first, second], [first, legacy, current], false),
                    ("reordered-original", [first, second], [second, first, legacy, current], false),
                    ("added-native-display", [first], [first, legacy, addedDisplay], false),
                    ("extra-primitive-member", [first], [first, legacy, primitiveMember], false),
                    ("duplicate-native-code-member", [first], [first, legacy, duplicateCode], false),
                ];
                foreach ((string name, string[] original, string[] actual, bool accepted) in controls)
                {
                    yield return [$"{format} / {name}", format,
                        SummaryMetadataControlResource(format, original), SummaryMetadataControlResource(format, actual), accepted];
                }
            }
        }
    }

    /// <summary>The same oracle must accept reviewed examples and reject same-sized but incorrect pairs/content.</summary>
    [Theory]
    [MemberData(nameof(SummaryMetadataControls))]
    public void SummaryMetadataOracleChecksExactPairs(
        string label,
        CompatibilityFormat format,
        string original,
        string actual,
        bool accepted)
    {
        Assert.NotEmpty(label);
        // Malformed control syntax is not evidence that the semantic assertion rejects a defect.
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument originalDocument = JsonDocument.Parse(original);
            using JsonDocument actualDocument = JsonDocument.Parse(actual);
            Assert.Equal(JsonValueKind.Object, originalDocument.RootElement.ValueKind);
            Assert.Equal(JsonValueKind.Object, actualDocument.RootElement.ValueKind);
        }
        else
        {
            Assert.Equal(_exchangeFhir + "Patient", ExchangeXml(original).Name);
            Assert.Equal(_exchangeFhir + "Patient", ExchangeXml(actual).Name);
        }

        if (accepted)
        {
            AssertSummaryExchangeMetadata(original, actual, format);
            return;
        }

        Exception? failure = Record.Exception(() => AssertSummaryExchangeMetadata(original, actual, format));
        Assert.NotNull(failure);
        if (failure is InvalidOperationException)
        {
            Assert.StartsWith("Wire contract:", failure.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.IsAssignableFrom<Xunit.Sdk.XunitException>(failure);
        }
    }

    private static string SummaryMetadataControlResource(CompatibilityFormat format, string[] tags) =>
        format == CompatibilityFormat.Json
            ? $$$"""{"resourceType":"Patient","id":"summary-control","meta":{"versionId":"1","lastUpdated":"2026-09-22T10:00:00Z","tag":[{{{string.Join(",", tags)}}}]}}"""
            : $"""<Patient xmlns="http://hl7.org/fhir"><id value="summary-control"/><meta><versionId value="1"/><lastUpdated value="2026-09-22T10:00:00Z"/>{string.Concat(tags)}</meta></Patient>""";

    /// <summary>Patient, Observation, and Binary each follow 201/200/200/200/200/404 with exact content.</summary>
    [Theory]
    [MemberData(nameof(ExchangeCases))]
    public async Task ResourceExchangePreservesWireContent(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        CompatibilityTenant tenant = Tenant(version, $"wire-{VersionName(version)}");
        string patient = ExchangeFixture(version, format, "Patient");
        GeneratedResourceExpectation patientV1 = new("patient-v1", "Patient", "1");
        CompatibilityScenario scenario = new(label, ExchangeEvidence, [tenant],
        [
            new("create-patient", new CompatibilityRequest(tenant.Name, "POST", "Patient") { Format = format, Content = patient },
                ResourceResponse("create-patient", patient, patientV1, HttpStatusCode.Created)),
            BoundRead("read-patient", tenant, format, patient, patientV1),
        ]);

        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        GeneratedResourceBinding patientIdentity = replay.Bindings.Resources[patientV1.Name];
        string updatedPatient = WithWireId(PatientLanguage(patient, format, "en-US"), format, patientIdentity.Id);
        GeneratedResourceExpectation patientV2 = await UpdateExchangeResource(
            replay, tenant, format, patientV1, updatedPatient);

        string observation = ExchangeFixture(version, format, "Observation", patientIdentity.Location);
        GeneratedResourceExpectation observationV1 = new("observation-v1", "Observation", "1");
        await replay.StepAsync(new CompatibilityStep(
            "create-observation", new CompatibilityRequest(tenant.Name, "POST", "Observation") { Format = format, Content = observation },
            ResourceResponse("create-observation", observation, observationV1, HttpStatusCode.Created)));
        await replay.StepAsync(BoundRead("read-observation", tenant, format, observation, observationV1));
        string updatedObservation = WithWireId(
            ReplaceFixtureValue(observation, "1234567890.123400", "1234567890.123450"),
            format, replay.Bindings.Resources[observationV1.Name].Id);
        GeneratedResourceExpectation observationV2 = await UpdateExchangeResource(
            replay, tenant, format, observationV1, updatedObservation);

        string binary = ExchangeFixture(version, format, "Binary", patientIdentity.Location);
        GeneratedResourceExpectation binaryV1 = new("binary-v1", "Binary", "1");
        CompatibilityResponse binaryCreated = await replay.StepAsync(new CompatibilityStep(
            "create-binary", new CompatibilityRequest(tenant.Name, "POST", "Binary") { Format = format, Content = binary },
            ResourceResponse("create-binary", binary, binaryV1, HttpStatusCode.Created)));
        AssertBinaryBytes(binaryCreated, format, [0, 1, 2, 3, 254, 255, 65, 66, 67, 10]);
        await replay.StepAsync(BoundRead("read-binary", tenant, format, binary, binaryV1));
        string updatedBinary = WithWireId(
            ReplaceFixtureValue(binary, "AAECA/7/QUJDCg==", "CQgHBgUEMjEwCg=="),
            format, replay.Bindings.Resources[binaryV1.Name].Id);
        GeneratedResourceExpectation binaryV2 = await UpdateExchangeResource(
            replay, tenant, format, binaryV1, updatedBinary);
        CompatibilityResponse binaryRead = await replay.StepAsync(BoundRead(
            "read-updated-binary-bytes", tenant, format, updatedBinary, binaryV2));
        AssertBinaryBytes(binaryRead, format, [9, 8, 7, 6, 5, 4, 50, 49, 48, 10]);

        // The Patient update must not rewrite the two resources' exact tenant-qualified references.
        await replay.StepAsync(BoundRead("reference-still-points-to-patient", tenant, format, updatedObservation, observationV2));
        await replay.StepAsync(BoundRead("patient-still-en-US", tenant, format, updatedPatient, patientV2));
        await DeleteExchangeResource(replay, tenant, format, updatedBinary, binaryV2);
        await DeleteExchangeResource(replay, tenant, format, updatedObservation, observationV2);
        await DeleteExchangeResource(replay, tenant, format, updatedPatient, patientV2);
    }

    /// <summary>Prefer changes only the HTTP body selection; reads prove every submitted update was stored.</summary>
    [Theory]
    [MemberData(nameof(ExchangeCases))]
    public async Task ResourceExchangePreservesWireContentForReturnPreferences(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        CompatibilityTenant tenant = Tenant(version, $"prefer-{VersionName(version)}");
        string original = ExchangeFixture(version, format, "Patient");
        GeneratedResourceExpectation initial = new("prefer-v1", "Patient", "1");
        CompatibilityScenario scenario = new(label + "/prefer", ExchangeEvidence + "; FhirController.AddFhirResponse", [tenant],
        [
            new("initial", new CompatibilityRequest(tenant.Name, "POST", "Patient") { Format = format, Content = original },
                ResourceResponse("initial", original, initial, HttpStatusCode.Created)),
        ]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        string id = replay.Bindings.Resources[initial.Name].Id;
        string previous = initial.Name;
        string[] preferences = ["return=minimal", "return=OperationOutcome", "return=representation"];
        string[] languages = ["fr", "de", "en-US"];
        for (int index = 0; index < preferences.Length; index++)
        {
            string stepId = $"prefer-{index}";
            string content = WithWireId(PatientLanguage(original, format, languages[index]), format, id);
            GeneratedResourceExpectation updated = new(stepId, "Patient", (index + 2).ToString(CultureInfo.InvariantCulture),
                id, PreviousBinding: previous);
            CompatibilityResponse write = await replay.SendAsync(new CompatibilityRequest(tenant.Name, "PUT", $"Patient/{id}")
            {
                Format = format,
                Content = content,
                IfMatchBinding = previous,
                Headers = new Dictionary<string, string> { ["Prefer"] = preferences[index] },
            });
            WireContractAssertions.Status(HttpStatusCode.OK, write.Status);

            // Capture the new metadata on a real read, including when HTTP deliberately had no resource body.
            await replay.StepAsync(new CompatibilityStep(stepId + "-read",
                new CompatibilityRequest(tenant.Name, "GET", $"Patient/{id}") { Format = format },
                ResourceResponse(stepId + "-read", content, updated, HttpStatusCode.OK)));
            CompatibilityResponseExpectation expected = ResourceResponse(stepId + "-write", content,
                updated with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK);
            expected = expected with
            {
                HttpBody = preferences[index] switch
                {
                    "return=minimal" => new EmptyBodyExpectation(),
                    "return=OperationOutcome" => SuccessOutcome(stepId + "-wire"),
                    _ => expected.HttpBody,
                },
            };
            WireContractAssertions.Response(expected, write, format, tenant.BaseUrl, replay.Bindings);
            previous = updated.Name;
        }

        string finalContent = WithWireId(PatientLanguage(original, format, "en-US"), format, id);
        await replay.StepAsync(new CompatibilityStep("read-ignores-outcome-preference",
            new CompatibilityRequest(tenant.Name, "GET", $"Patient/{id}")
            {
                Format = format,
                Headers = new Dictionary<string, string> { ["Prefer"] = "return=OperationOutcome" },
            },
            ResourceResponse("read-ignores-outcome-preference", finalContent,
                new(previous, "Patient", "4", id, CompatibilityBindingMode.Reuse), HttpStatusCode.OK)));
    }

    /// <summary>Condition cardinality, strict/default IDs, headers, and failed writes preserve exact stored state.</summary>
    [Theory]
    [MemberData(nameof(ExchangePolicyCases))]
    public async Task ConditionalInteractionsPreserveState(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        bool strict)
    {
        Assert.NotEmpty(label);
        CompatibilityTenant tenant = Tenant(version, $"condition-{(strict ? "strict" : "default")}-{VersionName(version)}")
            with { Strict = strict, SupportNotChanged = true };
        string patient = ExchangeFixture(version, format, "Patient");
        string condition = $"identifier=https://example.test/identifier|wire-{VersionName(version)}";
        GeneratedResourceExpectation first = new("first-v1", "Patient", "1");
        CompatibilityScenario scenario = new(label + "/conditional", ExchangeEvidence, [tenant],
        [
            new("zero-match-create", new CompatibilityRequest(tenant.Name, "POST", "Patient")
            {
                Format = format,
                Content = patient,
                Headers = new Dictionary<string, string> { ["If-None-Exist"] = condition },
            }, ResourceResponse("zero-match-create", patient, first, HttpStatusCode.Created)),
            new("one-match-keeps-original", new CompatibilityRequest(tenant.Name, "POST", "Patient")
            {
                Format = format,
                Content = PatientLanguage(patient, format, "fr"),
                Headers = new Dictionary<string, string> { ["If-None-Exist"] = condition },
            }, ResourceResponse("one-match-keeps-original", patient, first with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK)),
            BoundRead("one-match-not-a-new-version", tenant, format, patient, first),
        ]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        string firstId = replay.Bindings.Resources[first.Name].Id;
        GeneratedResourceExpectation second = new("second-v1", "Patient", "1");
        await replay.StepAsync(new CompatibilityStep("create-second",
            new CompatibilityRequest(tenant.Name, "POST", "Patient") { Format = format, Content = patient },
            ResourceResponse("create-second", patient, second, HttpStatusCode.Created)));
        string secondId = replay.Bindings.Resources[second.Name].Id;
        Assert.NotEqual(firstId, secondId);

        await AssertExchangeFailure(replay, tenant, format, "multiple-match-create",
            new CompatibilityRequest(tenant.Name, "POST", "Patient")
            {
                Format = format, Content = PatientLanguage(patient, format, "fr"),
                Headers = new Dictionary<string, string> { ["If-None-Exist"] = condition },
            },
            HttpStatusCode.PreconditionFailed, "exception", "If-None-Exist query returned too many matches: 2");
        await AssertExchangeFailure(replay, tenant, format, "multiple-match-update",
            new CompatibilityRequest(tenant.Name, "PUT", $"Patient?_id={firstId},{secondId}")
            {
                Format = format, Content = patient,
            },
            HttpStatusCode.PreconditionFailed, "exception", "Conditional update query returned too many matches: 2");
        await replay.StepAsync(BoundRead("first-survives-multiple-matches", tenant, format, patient, first));
        await replay.StepAsync(BoundRead("second-survives-multiple-matches", tenant, format, patient, second));

        string changed = WithWireId(PatientLanguage(patient, format, "en-US"), format, firstId);
        GeneratedResourceExpectation current = new("first-v2", "Patient", "2", firstId, PreviousBinding: first.Name);
        await replay.StepAsync(new CompatibilityStep("one-match-update-with-id",
            new CompatibilityRequest(tenant.Name, "PUT", $"Patient?_id={firstId}")
            {
                Format = format, Content = changed, IfMatchBinding = first.Name,
            }, ResourceResponse("one-match-update-with-id", changed, current, HttpStatusCode.OK)));
        await replay.StepAsync(BoundRead("conditional-update-persists", tenant, format, changed, current));
        await AssertExchangeFailure(replay, tenant, format, "stale-read-etag",
            new CompatibilityRequest(tenant.Name, "GET", $"Patient/{firstId}") { Format = format, IfMatchBinding = first.Name },
            HttpStatusCode.PreconditionFailed, "business-rule", "If-Match: W/\"1\" does not equal found eTag: W/\"2\"");
        OutcomeBodyExpectation notModified = new("not-modified-outcome",
            [new("error", "exception", $"Read Patient/{firstId} found version: W/\"2\", equals If-None-Match: W/\"2\"")]);
        await replay.StepAsync(new CompatibilityStep("if-none-match",
            new CompatibilityRequest(tenant.Name, "GET", $"Patient/{firstId}")
            {
                Format = format, Headers = new Dictionary<string, string> { ["If-None-Match"] = "W/\"2\"" },
            },
            new CompatibilityResponseExpectation(HttpStatusCode.NotModified, false,
                new EmptyBodyExpectation(), new EmptyBodyExpectation(), notModified)
            {
                HttpHeaders = NoExchangeHeaders(),
                DirectHeaders = NoExchangeHeaders(),
            }));

        await AssertExchangeFailure(replay, tenant, format, "url-body-disagree",
            new CompatibilityRequest(tenant.Name, "PUT", $"Patient/{firstId}")
            {
                Format = format, Content = WithWireId(patient, format, "different-id"),
            },
            HttpStatusCode.UnprocessableEntity, "invalid",
            $"URL id '{firstId}' does not match resource id 'different-id' (see http://hl7.org/fhir/{version}/http.html#update)");
        await replay.StepAsync(BoundRead("mismatch-did-not-replace", tenant, format, changed, current));
        await replay.StepAsync(MissingPatient("mismatch-did-not-create", tenant, format, "different-id"));

        string noId = PatientLanguage(patient, format, "de");
        if (strict)
        {
            await AssertExchangeFailure(replay, tenant, format, "strict-empty-put-id",
                new CompatibilityRequest(tenant.Name, "PUT", $"Patient/{firstId}") { Format = format, Content = noId },
                HttpStatusCode.UnprocessableEntity, "required",
                $"Resource.id is required on PUT and must equal the URL id '{firstId}'. (see http://hl7.org/fhir/{version}/http.html#update)");
            await AssertExchangeFailure(replay, tenant, format, "strict-duplicate-client-post",
                new CompatibilityRequest(tenant.Name, "POST", "Patient") { Format = format, Content = changed },
                HttpStatusCode.BadRequest, "invalid",
                $"POST/create must not include Resource.id; the server assigns ids on create. (see http://hl7.org/fhir/{version}/http.html#create)");
            await AssertExchangeFailure(replay, tenant, format, "strict-missing-put",
                new CompatibilityRequest(tenant.Name, "PUT", "Patient/missing-put")
                {
                    Format = format, Content = WithWireId(patient, format, "missing-put"),
                },
                HttpStatusCode.NotFound, "not-found",
                $"Resource Patient/missing-put does not exist; PUT cannot create resources under strict mode. (see http://hl7.org/fhir/{version}/http.html#upsert)");
            await replay.StepAsync(BoundRead("strict-rejections-keep-content", tenant, format, changed, current));
            await replay.StepAsync(MissingPatient("strict-rejection-keeps-missing", tenant, format, "missing-put"));
        }
        else
        {
            GeneratedResourceExpectation stamped = new("first-v3", "Patient", "3", firstId, PreviousBinding: current.Name);
            await replay.StepAsync(new CompatibilityStep("default-empty-put-id",
                new CompatibilityRequest(tenant.Name, "PUT", $"Patient/{firstId}")
                {
                    Format = format, Content = noId, IfMatchBinding = current.Name,
                }, ResourceResponse("default-empty-put-id", noId, stamped, HttpStatusCode.OK)));
            current = stamped;
            changed = noId;
            GeneratedResourceExpectation reassigned = new("reassigned-v1", "Patient", "1");
            await replay.StepAsync(new CompatibilityStep("default-client-post-reassigns",
                new CompatibilityRequest(tenant.Name, "POST", "Patient")
                {
                    Format = format, Content = WithWireId(patient, format, firstId),
                }, ResourceResponse("default-client-post-reassigns", patient, reassigned, HttpStatusCode.Created)));
            Assert.NotEqual(firstId, replay.Bindings.Resources[reassigned.Name].Id);
            await replay.StepAsync(BoundRead("reassigned-content", tenant, format, patient, reassigned));

            string upsert = WithWireId(patient, format, "zero-update");
            GeneratedResourceExpectation upserted = new("zero-update-v1", "Patient", "1", "zero-update");
            await replay.StepAsync(new CompatibilityStep("zero-match-update",
                new CompatibilityRequest(tenant.Name, "PUT", "Patient?_id=zero-update") { Format = format, Content = upsert },
                ResourceResponse("zero-match-update", upsert, upserted, HttpStatusCode.Created)));
            await replay.StepAsync(BoundRead("zero-match-update-persists", tenant, format, upsert, upserted));

            // Unlike the controller, the direct dispatcher also supports conditional POST via the URL.
            if (transport == CompatibilityTransport.Direct)
            {
                await replay.StepAsync(new CompatibilityStep("url-query-one-match",
                    new CompatibilityRequest(tenant.Name, "POST", $"Patient?_id={firstId}")
                    {
                        Format = format, Content = patient,
                    }, ResourceResponse("url-query-one-match", changed, current with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK)));
            }
        }

        string controlFormat = Uri.EscapeDataString(WireContractAssertions.MediaType(format));
        string controlContent = PatientLanguage(patient, format, "es");
        GeneratedResourceExpectation controlCreated = new("control-create-v1", "Patient", "1");
        await replay.StepAsync(new CompatibilityStep("control-parameters-are-not-criteria",
            new CompatibilityRequest(tenant.Name, "POST", $"Patient?_format={controlFormat}")
            {
                Format = format, Content = controlContent, Pretty = true,
                Headers = new Dictionary<string, string> { ["If-None-Exist"] = "_pretty=true" },
            }, ResourceResponse("control-parameters-are-not-criteria", controlContent, controlCreated, HttpStatusCode.Created)));
        Assert.NotEqual(firstId, replay.Bindings.Resources[controlCreated.Name].Id);
        Assert.NotEqual(secondId, replay.Bindings.Resources[controlCreated.Name].Id);
        await replay.StepAsync(BoundRead("control-parameter-create-persists", tenant, format, controlContent, controlCreated));
        await AssertExchangeFailure(replay, tenant, format, "control-only-delete-is-rejected",
            new CompatibilityRequest(tenant.Name, "DELETE", $"Patient?_format={controlFormat}") { Format = format },
            HttpStatusCode.BadRequest, "required", "Type-level delete of Patient requires search criteria");

        // Header criteria take precedence over conflicting URL criteria at both boundaries.
        await replay.StepAsync(new CompatibilityStep("if-none-exist-precedence",
            new CompatibilityRequest(tenant.Name, "POST", $"Patient?_id={firstId}")
            {
                Format = format, Content = PatientLanguage(patient, format, "fr"),
                Headers = new Dictionary<string, string> { ["If-None-Exist"] = $"_id={secondId}" },
            }, ResourceResponse("if-none-exist-precedence", patient, second with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK)));
        await AssertExchangeFailure(replay, tenant, format, "conditional-delete-multiple",
            new CompatibilityRequest(tenant.Name, "DELETE", $"Patient?_id={firstId},{secondId}") { Format = format },
            HttpStatusCode.PreconditionFailed, "multiple-matches", "Too many matches found for type (Patient) delete: (2)");
        await AssertExchangeFailure(replay, tenant, format, "conditional-delete-zero",
            new CompatibilityRequest(tenant.Name, "DELETE", "Patient?_id=absent") { Format = format },
            HttpStatusCode.NotFound, "not-found", "No matches found for type (Patient) delete");
        await replay.StepAsync(BoundRead("first-survives-delete-failures", tenant, format, changed, current));
        await replay.StepAsync(BoundRead("second-survives-delete-failures", tenant, format, patient, second));
        await replay.StepAsync(new CompatibilityStep("conditional-delete-one",
            new CompatibilityRequest(tenant.Name, "DELETE", $"Patient?_id={secondId}") { Format = format },
            ResourceResponse("conditional-delete-one", patient, second with { Mode = CompatibilityBindingMode.Reuse },
                HttpStatusCode.OK, resourceHeaders: false)));
        await replay.StepAsync(MissingPatient("conditional-delete-is-persistent", tenant, format, secondId));
        await replay.StepAsync(BoundRead("delete-does-not-touch-first", tenant, format, changed, current));
    }

    /// <summary>The internal-ingest override must reject a duplicate, even in a strict store, without overwriting it.</summary>
    [Theory]
    [MemberData(nameof(ExchangeExplicitCreateCases))]
    public void ConditionalInteractionsPreserveStateForExplicitCreate(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        bool strict)
    {
        Assert.NotEmpty(label);
        Assert.Equal(CompatibilityTransport.Direct, transport);
        CompatibilityTenant tenant = Tenant(version, $"forced-{(strict ? "strict" : "default")}-{VersionName(version)}");
        CompatibilityBindings bindings = new();
        WireContractAssertions.BindBaseUrl(bindings, tenant.Name, tenant.BaseUrl);
        using IFhirStore store = version switch
        {
            FhirReleases.FhirSequenceCodes.R4 => new candleR4::FhirCandle.Storage.VersionedFhirStore(),
            FhirReleases.FhirSequenceCodes.R4B => new candleR4B::FhirCandle.Storage.VersionedFhirStore(),
            FhirReleases.FhirSequenceCodes.R5 => new candleR5::FhirCandle.Storage.VersionedFhirStore(),
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        };
        store.Init(new TenantConfiguration
        {
            FhirVersion = version, ControllerName = tenant.Name, BaseUrl = tenant.BaseUrl, Strict = strict,
            AllowExistingId = true, AllowCreateAsUpdate = true,
        });
        string patient = WithWireId(ExchangeFixture(version, format, "Patient"), format, "explicit-create");
        GeneratedResourceExpectation generated = new("explicit-v1", "Patient", "1", "explicit-create");
        FhirRequestContext context = new()
        {
            TenantName = tenant.Name, Store = store, HttpMethod = "POST", Url = tenant.BaseUrl + "/Patient",
            Authorization = null, SourceFormat = WireContractAssertions.MediaType(format), SourceContent = patient,
            DestinationFormat = WireContractAssertions.MediaType(format),
        };
        bool created = store.InstanceCreate(context, out FhirResponseContext creation, forceAllowExistingId: true);
        AssertDirectExchangeResponse(ResourceResponse("explicit-create", patient, generated, HttpStatusCode.Created),
            created, creation, format, tenant, bindings);
        bool duplicate = store.InstanceCreate(context with { SourceContent = PatientLanguage(patient, format, "fr") },
            out FhirResponseContext rejection, forceAllowExistingId: true);
        OutcomeBodyExpectation outcome = new("duplicate-outcome",
            [new("error", "duplicate", "Resource Patient/explicit-create already exists; POST-base create interaction cannot overwrite existing resources")]);
        AssertDirectExchangeResponse(new CompatibilityResponseExpectation(HttpStatusCode.Conflict, false,
            outcome, new EmptyBodyExpectation(), outcome)
        {
            DirectHeaders = NoExchangeHeaders(),
        }, duplicate, rejection, format, tenant, bindings);
        FhirRequestContext readContext = new()
        {
            TenantName = tenant.Name, Store = store, HttpMethod = "GET",
            Url = tenant.BaseUrl + "/Patient/explicit-create", Authorization = null,
            DestinationFormat = WireContractAssertions.MediaType(format),
        };
        bool read = store.PerformInteraction(readContext, out FhirResponseContext persisted, serializeReturn: true);
        AssertDirectExchangeResponse(ResourceResponse("after-duplicate", patient,
            generated with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK),
            read, persisted, format, tenant, bindings);
    }

    /// <summary>Pin existing per-entry processing, including failures, conditional results, and transaction ID references.</summary>
    [Theory]
    [MemberData(nameof(ExchangeBundleCases))]
    public async Task BundleEntriesPreserveOutcomes(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string bundleType)
    {
        Assert.NotEmpty(label);
        CompatibilityTenant tenant = Tenant(version, $"bundle-{bundleType}-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, ExchangeEvidence + "; VersionedFhirStore.ProcessBatch/ProcessTransaction",
            [tenant], []);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.AssertNoClinicalResourcesAsync(format);
        string patient = ExchangeFixture(version, format, "Patient");
        if (bundleType == "batch")
        {
            string initial = WithWireId(patient, format, "batch-patient");
            string changed = WithWireId(PatientLanguage(patient, format, "en-US"), format, "batch-patient");
            GeneratedResourceExpectation first = new("batch-v1", "Patient", "1", "batch-patient");
            GeneratedResourceExpectation second = new("batch-v2", "Patient", "2", "batch-patient", PreviousBinding: first.Name);
            string request = ExchangeBundleContent(bundleType, format,
            [
                new(BundleFullUrl(1), "PUT", "Patient/batch-patient", initial),
                new(BundleFullUrl(2), "GET", "Patient/batch-patient"),
                new(BundleFullUrl(3), "POST", "Patient", PatientLanguage(patient, format, "fr"), IfNoneExist: "_id=batch-patient"),
                new(BundleFullUrl(4), "PUT", "Patient/batch-patient", changed, IfMatch: "W/\"1\""),
                new(BundleFullUrl(5), "GET", "Patient/absent"),
                new(BundleFullUrl(6), null, null),
            ]);
            CompatibilityResponse response = await replay.SendAsync(new CompatibilityRequest(tenant.Name, "POST", string.Empty)
            {
                Format = format, Content = request,
            });
            AssertExchangeBundle(replay, tenant, format, "batch", response, "batch-response",
            [
                new(BundleFullUrl(1), HttpStatusCode.Created, initial, first),
                new(BundleFullUrl(2), HttpStatusCode.OK, initial, first with { Mode = CompatibilityBindingMode.Reuse }),
                new(BundleFullUrl(3), HttpStatusCode.OK, initial, first with { Mode = CompatibilityBindingMode.Reuse }),
                new(BundleFullUrl(4), HttpStatusCode.OK, changed, second),
                new(BundleFullUrl(5), HttpStatusCode.NotFound, Issues:
                [
                    new("error", "exception", "Resource: Patient/absent not found"),
                    new("error", "not-supported", "Unsupported request: GET Patient/absent, parsed interaction: InstanceRead"),
                ]),
                new(BundleFullUrl(6), HttpStatusCode.BadRequest, Issues: [new("error", "required", "Entry is missing a request")]),
            ]);
            await replay.StepAsync(BoundRead("batch-failure-did-not-undo-update", tenant, format, changed, second));
            await replay.StepAsync(MissingPatient("batch-missing-stays-missing", tenant, format, "absent"));

            string deletion = ExchangeBundleContent("batch", format,
            [
                new(BundleFullUrl(7), "DELETE", "Patient/batch-patient"),
                new(BundleFullUrl(8), "GET", "Patient/batch-patient"),
            ]);
            CompatibilityResponse deleted = await replay.SendAsync(new CompatibilityRequest(tenant.Name, "POST", string.Empty)
            {
                Format = format, Content = deletion,
            });
            AssertExchangeBundle(replay, tenant, format, "batch-delete", deleted, "batch-response",
            [
                new(BundleFullUrl(7), HttpStatusCode.OK, changed, second with { Mode = CompatibilityBindingMode.Reuse }, ResourceHeaders: false),
                new(BundleFullUrl(8), HttpStatusCode.NotFound, Issues:
                [
                    new("error", "exception", "Resource: Patient/batch-patient not found"),
                    new("error", "not-supported", "Unsupported request: GET Patient/batch-patient, parsed interaction: InstanceRead"),
                ]),
            ]);
            await replay.StepAsync(MissingPatient("batch-delete-persists", tenant, format, "batch-patient"));
            return;
        }

        Assert.Equal("transaction", bundleType);
        GeneratedResourceExpectation transactionPatient = new("transaction-patient-v1", "Patient", "1");
        GeneratedResourceExpectation transactionObservation = new("transaction-observation-v1", "Observation", "1");
        string observation = ExchangeFixture(version, format, "Observation");
        string transaction = ExchangeBundleContent("transaction", format,
        [
            new(BundleFullUrl(11), "POST", "Patient", WithWireId(patient, format, "transaction-client")),
            new(BundleFullUrl(12), "POST", "Observation", ReplaceFixtureValue(observation, "Patient/wire-patient", BundleFullUrl(11))),
            new(BundleFullUrl(13), "GET", "Patient/transaction-client"),
            new(BundleFullUrl(14), "GET", "Patient/absent"),
        ]);
        CompatibilityResponse transactionResponse = await replay.SendAsync(new CompatibilityRequest(tenant.Name, "POST", string.Empty)
        {
            Format = format, Content = transaction,
        });
        AssertExchangeBundle(replay, tenant, format, "transaction", transactionResponse, "transaction-response",
        [
            new(BundleFullUrl(11), HttpStatusCode.Created, patient, transactionPatient),
            new(BundleFullUrl(12), HttpStatusCode.Created, observation, transactionObservation,
                PatientReferenceBinding: transactionPatient.Name),
            new(BundleFullUrl(13), HttpStatusCode.OK, patient, transactionPatient with { Mode = CompatibilityBindingMode.Reuse }),
            new(BundleFullUrl(14), HttpStatusCode.NotFound, Issues:
            [
                new("error", "exception", "Resource: Patient/absent not found"),
                new("error", "not-supported", "Unsupported request: GET Patient/absent, parsed interaction: InstanceRead"),
            ]),
        ]);
        GeneratedResourceBinding patientIdentity = replay.Bindings.Resources[transactionPatient.Name];
        Assert.NotEqual("transaction-client", patientIdentity.Id);
        string linkedObservation = ReplaceFixtureValue(observation, "Patient/wire-patient", $"Patient/{patientIdentity.Id}");
        await replay.StepAsync(BoundRead("transaction-patient-persists", tenant, format, patient, transactionPatient));
        await replay.StepAsync(BoundRead("transaction-reference-persists", tenant, format, linkedObservation, transactionObservation));
        await replay.StepAsync(MissingPatient("transaction-client-id-not-stored", tenant, format, "transaction-client"));

        string conditional = ExchangeBundleContent("transaction", format,
        [
            new(BundleFullUrl(15), "POST", $"Patient?identifier=https://example.test/identifier|wire-{VersionName(version)}",
                PatientLanguage(patient, format, "fr")),
        ]);
        CompatibilityResponse matched = await replay.SendAsync(new CompatibilityRequest(tenant.Name, "POST", string.Empty)
        {
            Format = format, Content = conditional,
        });
        AssertExchangeBundle(replay, tenant, format, "transaction-conditional-create", matched, "transaction-response",
        [
            new(BundleFullUrl(15), HttpStatusCode.OK, patient, transactionPatient with { Mode = CompatibilityBindingMode.Reuse }),
        ]);
        await replay.StepAsync(BoundRead("transaction-conditional-create-did-not-update", tenant, format, patient, transactionPatient));

        string upsert = WithWireId(patient, format, "bundle-upsert");
        GeneratedResourceExpectation upserted = new("bundle-upsert-v1", "Patient", "1", "bundle-upsert");
        string conditionalUpdate = ExchangeBundleContent("transaction", format,
        [
            new(BundleFullUrl(16), "PUT", "Patient?_id=bundle-upsert", upsert),
        ]);
        CompatibilityResponse created = await replay.SendAsync(new CompatibilityRequest(tenant.Name, "POST", string.Empty)
        {
            Format = format, Content = conditionalUpdate,
        });
        AssertExchangeBundle(replay, tenant, format, "transaction-conditional-zero", created, "transaction-response",
        [
            new(BundleFullUrl(16), HttpStatusCode.Created, upsert, upserted),
        ]);
        string updatedUpsert = WithWireId(PatientLanguage(patient, format, "en-US"), format, "bundle-upsert");
        GeneratedResourceExpectation updated = new("bundle-upsert-v2", "Patient", "2", "bundle-upsert", PreviousBinding: upserted.Name);
        string update = ExchangeBundleContent("transaction", format,
        [
            new(BundleFullUrl(17), "PUT", "Patient?_id=bundle-upsert", updatedUpsert, IfMatch: "W/\"1\""),
        ]);
        CompatibilityResponse changedResponse = await replay.SendAsync(new CompatibilityRequest(tenant.Name, "POST", string.Empty)
        {
            Format = format, Content = update,
        });
        AssertExchangeBundle(replay, tenant, format, "transaction-conditional-one", changedResponse, "transaction-response",
        [
            new(BundleFullUrl(17), HttpStatusCode.OK, updatedUpsert, updated),
        ]);
        await replay.StepAsync(BoundRead("transaction-conditional-update-persists", tenant, format, updatedUpsert, updated));
    }

    /// <summary>Exercise summary content, formatting, parser modes, aliases, and boundary-specific negotiation.</summary>
    [Theory]
    [MemberData(nameof(ExchangeSerializationCases))]
    public async Task SerializationModesPreserveContract(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string mode)
    {
        Assert.NotEmpty(label);
        CompatibilityTenant tenant = Tenant(version, $"serialize-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, ExchangeEvidence +
            "; SerializationUtils.TryDeserializeFhir/SerializeFhir; FhirController.getMimeType; " +
            "FHIR search.html#summary; Firely 6.2.1 FhirXmlDeserializer.BACKWARDSCOMPATIBLE API documentation",
            [tenant], []);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.AssertNoClinicalResourcesAsync(format);
        string patient = ExchangeFixture(version, format, "Patient");

        if (mode is "input-fallback" or "output-fallback")
        {
            CompatibilityFormat outputFormat = mode == "output-fallback" ? CompatibilityFormat.Json : format;
            string expected = ExchangeFixture(version, outputFormat, "Patient");
            CompatibilityResponse converted = await replay.SendAsync(new CompatibilityRequest(tenant.Name, "POST", "$convert")
            {
                Format = outputFormat,
                Content = patient,
                SourceMediaType = mode == "input-fallback" ? "text/plain" : WireContractAssertions.MediaType(format),
                DestinationMediaType = mode == "output-fallback" ? "application/x-corpus-unknown" : WireContractAssertions.MediaType(format),
            });
            string mediaType = mode == "output-fallback" && transport == CompatibilityTransport.Direct
                ? "application/x-corpus-unknown"
                : WireContractAssertions.MediaType(outputFormat);
            AssertExchangeContainer(replay, tenant, outputFormat, mode, converted, "Patient", string.Empty,
                new OutcomeBodyExpectation(mode + "-outcome", [new("success", "success", "Successful conversion")]), mediaType);
            WireContractAssertions.Content(expected, ExchangeResource(converted), outputFormat);
            await AssertEmptyExchangeSearches(replay, tenant, outputFormat, "fallback-is-stateless");
            return;
        }

        if (mode == "short-aliases")
        {
            Assert.Equal(CompatibilityTransport.Direct, transport);
            string suffix = format == CompatibilityFormat.Json ? "json" : "xml";
            foreach (string alias in new[] { suffix, "fhir+" + suffix })
            {
                GeneratedResourceExpectation aliasBinding = new(alias, "Patient", "1");
                await replay.StepAsync(new CompatibilityStep("short-alias-" + alias,
                    new CompatibilityRequest(tenant.Name, "POST", "Patient")
                    {
                        Format = format, Content = patient, SourceMediaType = alias, DestinationMediaType = alias,
                    }, ResourceResponse("short-alias-" + alias, patient, aliasBinding, HttpStatusCode.Created) with { MediaType = alias }));
                await replay.StepAsync(BoundRead("short-alias-read-" + alias, tenant, format, patient, aliasBinding));
            }
            return;
        }

        string sourceMediaType = WireContractAssertions.MediaType(format);
        string outputMediaType = sourceMediaType;
        if (mode == "mime-alias")
        {
            sourceMediaType = format == CompatibilityFormat.Json ? "application/json" : "application/xml";
            outputMediaType = sourceMediaType;
        }
        else if (mode == "mime-parameters")
        {
            sourceMediaType += $"; charset=utf-8; fhirVersion={ExpectedFhirVersion(version)}";
        }
        else if (mode == "tolerated-code")
        {
            // Backwards-compatible parsing explicitly tolerates an unknown coded value, not an invalid boolean.
            patient = ReplaceFixtureValue(patient,
                format == CompatibilityFormat.Json ? "\"gender\": \"other\"" : "<gender value=\"other\"/>",
                format == CompatibilityFormat.Json ? "\"gender\": \"future-value\"" : "<gender value=\"future-value\"/>");
        }

        GeneratedResourceExpectation created = new("serialized-v1", "Patient", "1");
        await replay.StepAsync(new CompatibilityStep("ordinary-create",
            new CompatibilityRequest(tenant.Name, "POST", "Patient")
            {
                Format = format, Content = patient, SourceMediaType = sourceMediaType, DestinationMediaType = outputMediaType,
            }, ResourceResponse("ordinary-create", patient, created, HttpStatusCode.Created) with { MediaType = outputMediaType }));
        await replay.StepAsync(BoundRead("ordinary-read", tenant, format, patient, created));

        if (mode.StartsWith("summary-", StringComparison.Ordinal))
        {
            string flag = mode["summary-".Length..];
            string observation = ExchangeFixture(version, format, "Observation", replay.Bindings.Resources[created.Name].Location);
            GeneratedResourceExpectation observationCreated = new("summary-observation-v1", "Observation", "1");
            await replay.StepAsync(new CompatibilityStep("summary-observation-create",
                new CompatibilityRequest(tenant.Name, "POST", "Observation") { Format = format, Content = observation },
                ResourceResponse("summary-observation-create", observation, observationCreated, HttpStatusCode.Created)));
            await replay.StepAsync(BoundRead("summary-observation-read", tenant, format, observation, observationCreated));

            foreach ((string original, GeneratedResourceExpectation generated) in new[]
            {
                (patient, created), (observation, observationCreated),
            })
            {
                string resourceType = generated.ResourceType;
                foreach (bool pretty in new[] { false, true })
                {
                    (string Name, CompatibilityRequest Request)[] requests =
                    [
                        ("context", new CompatibilityRequest(tenant.Name, "GET", resourceType)
                        {
                            Format = format, Summary = flag, Pretty = pretty,
                        }),
                        ("query-only", new CompatibilityRequest(tenant.Name, "GET", $"{resourceType}?_summary={flag}")
                        {
                            Format = format, Pretty = pretty,
                        }),
                        ("form", new CompatibilityRequest(tenant.Name, "POST", $"{resourceType}/_search")
                        {
                            Format = format, Pretty = pretty,
                            Content = $"_summary={flag}", SourceMediaType = "application/x-www-form-urlencoded",
                        }),
                    ];
                    foreach ((string name, CompatibilityRequest request) in requests)
                    {
                        CompatibilityResponse search = await replay.SendAsync(request);
                        string stepId = $"summary-{resourceType}-{flag}-{pretty}-{name}";
                        AssertExchangeContainer(replay, tenant, format, stepId, search, "Bundle", string.Empty,
                            new OutcomeBodyExpectation(stepId + "-outcome", [new("information", "success", "Type search successful")]));
                        AssertSummaryExchangeBundle(replay, tenant, format, original, generated, flag, ExchangeResource(search));
                        AssertExchangeFormatting(ExchangeResource(search), format, pretty);
                    }

                    if (flag != "count")
                    {
                        CompatibilityResponse read = await replay.SendAsync(new CompatibilityRequest(tenant.Name, "GET", string.Empty)
                        {
                            Format = format, TargetBinding = generated.Name, Summary = flag, Pretty = pretty,
                        });
                        AssertSummaryExchangeRead(replay, tenant, format, original, generated, flag,
                            $"summary-read-{resourceType}-{flag}-{pretty}", read);
                        AssertExchangeFormatting(ExchangeResource(read), format, pretty);
                    }
                }

                await replay.StepAsync(BoundRead($"summary-does-not-mutate-{resourceType}", tenant, format, original, generated));
                string fullSearchId = $"full-search-after-summary-{resourceType}";
                CompatibilityResponse fullSearch = await replay.SendAsync(
                    new CompatibilityRequest(tenant.Name, "GET", $"{resourceType}?_summary=false")
                    {
                        Format = format,
                    });
                AssertExchangeContainer(replay, tenant, format, fullSearchId, fullSearch, "Bundle", string.Empty,
                    new OutcomeBodyExpectation(fullSearchId + "-outcome", [new("information", "success", "Type search successful")]));
                AssertSummaryExchangeBundle(replay, tenant, format, original, generated, "false", ExchangeResource(fullSearch));
            }
            return;
        }

        if (mode == "pretty")
        {
            foreach (bool pretty in new[] { false, true })
            {
                string stepId = "pretty-" + pretty;
                CompatibilityResponse read = await replay.StepAsync(new CompatibilityStep(stepId,
                    new CompatibilityRequest(tenant.Name, "GET", string.Empty)
                    {
                        Format = format, TargetBinding = created.Name, Pretty = pretty,
                    }, ResourceResponse(stepId, patient, created with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK)));
                AssertExchangeFormatting(ExchangeResource(read), format, pretty);
            }
            return;
        }

        if (mode == "negotiation")
        {
            Assert.Equal(CompatibilityTransport.Http, transport);
            string id = replay.Bindings.Resources[created.Name].Id;
            CompatibilityFormat otherFormat = format == CompatibilityFormat.Json ? CompatibilityFormat.Xml : CompatibilityFormat.Json;
            string mime = WireContractAssertions.MediaType(format);
            string otherMime = WireContractAssertions.MediaType(otherFormat);
            string alias = format == CompatibilityFormat.Json ? "application/json" : "application/xml";
            foreach ((string name, string query, string expectedMediaType) in new[]
            {
                ("format-over-accept", Uri.EscapeDataString(mime), mime),
                ("format-mime-alias", Uri.EscapeDataString(alias), alias),
                ("format-unescaped-plus", mime, mime),
            })
            {
                await replay.StepAsync(new CompatibilityStep(name,
                    new CompatibilityRequest(tenant.Name, "GET", $"Patient/{id}?_format={query}")
                    {
                        Format = format, DestinationMediaType = otherMime,
                    }, ResourceResponse(name, patient, created with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK) with
                    {
                        MediaType = expectedMediaType,
                    }));
            }
            string otherPatient = ExchangeFixture(version, otherFormat, "Patient");
            await replay.StepAsync(new CompatibilityStep("unknown-format-uses-accept",
                new CompatibilityRequest(tenant.Name, "GET", $"Patient/{id}?_format=not-a-format")
                {
                    Format = otherFormat, DestinationMediaType = otherMime,
                }, ResourceResponse("unknown-format-uses-accept", otherPatient,
                    created with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK)));
            await replay.StepAsync(new CompatibilityStep("unknown-accept-defaults-json",
                new CompatibilityRequest(tenant.Name, "GET", $"Patient/{id}")
                {
                    Format = CompatibilityFormat.Json, DestinationMediaType = "application/x-corpus-unknown",
                }, ResourceResponse("unknown-accept-defaults-json", ExchangeFixture(version, CompatibilityFormat.Json, "Patient"),
                    created with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK)));
            return;
        }

        Assert.Contains(mode, new[] { "mime-alias", "mime-parameters", "tolerated-code" });
    }

    /// <summary>Convert JSON to XML and XML to JSON without using either SDK parser on the expected side.</summary>
    [Theory]
    [MemberData(nameof(ExchangeConversionCases))]
    public async Task ConvertPreservesCrossFormatContent(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat inputFormat,
        string resourceType)
    {
        Assert.NotEmpty(label);
        CompatibilityFormat outputFormat = inputFormat == CompatibilityFormat.Json ? CompatibilityFormat.Xml : CompatibilityFormat.Json;
        CompatibilityTenant tenant = Tenant(version, $"convert-{VersionName(version)}");
        string id = "convert-" + resourceType.ToLowerInvariant();
        string input = WithWireId(ExchangeFixture(version, inputFormat, resourceType), inputFormat, id);
        string expected = WithWireId(ExchangeFixture(version, outputFormat, resourceType), outputFormat, id);
        CompatibilityScenario scenario = new(label, ExchangeEvidence + "; OpConvert.DoOperation", [tenant], []);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.AssertNoClinicalResourcesAsync(outputFormat);
        CompatibilityResponse response = await replay.SendAsync(new CompatibilityRequest(tenant.Name, "POST", "$convert")
        {
            Format = outputFormat, Content = input,
            SourceMediaType = WireContractAssertions.MediaType(inputFormat),
            DestinationMediaType = WireContractAssertions.MediaType(outputFormat),
        });
        AssertExchangeContainer(replay, tenant, outputFormat, "convert", response, resourceType, id,
            new OutcomeBodyExpectation("convert-outcome", [new("success", "success", "Successful conversion")]));
        WireContractAssertions.Content(expected, ExchangeResource(response), outputFormat);
        if (resourceType == "Binary")
        {
            AssertBinaryBytes(response, outputFormat, [0, 1, 2, 3, 254, 255, 65, 66, 67, 10]);
        }
        await AssertEmptyExchangeSearches(replay, tenant, outputFormat, "convert-does-not-persist");
        await AssertExchangeFailure(replay, tenant, outputFormat, "converted-id-is-not-stored",
            new CompatibilityRequest(tenant.Name, "GET", $"{resourceType}/{id}") { Format = outputFormat },
            HttpStatusCode.NotFound, "exception", $"Resource: {resourceType}/{id} not found");
    }

    /// <summary>Malformed syntax, unknown resource types, bad primitives, and wrong destinations remain distinct failures.</summary>
    [Theory]
    [MemberData(nameof(ExchangeInvalidCases))]
    public async Task InvalidInputPreservesFailureMapping(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string failure)
    {
        Assert.NotEmpty(label);
        CompatibilityTenant tenant = Tenant(version, $"invalid-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, ExchangeEvidence +
            "; SerializationUtils.TryDeserializeFhir; VersionedFhirStore.InstanceCreate/InstanceUpdate/ProcessBundle/DoSystemOperation",
            [tenant], []);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.AssertNoClinicalResourcesAsync(format);
        string malformed = format == CompatibilityFormat.Json
            ? """{"resourceType":"Patient","active":"""
            : """<Patient xmlns="http://hl7.org/fhir"><active value="true"></Patient>""";
        string content = malformed;
        string method = "POST";
        string path = "Patient";
        string sourceMediaType = WireContractAssertions.MediaType(format);
        HttpStatusCode status = HttpStatusCode.UnprocessableEntity;
        string code = "structure";
        string? diagnostics = null;
        bool checkMediaType = false;
        switch (failure)
        {
            case "malformed":
                break;
            case "invalid-resource-type":
                content = format == CompatibilityFormat.Json
                    ? """{"resourceType":"NotAResource","id":"invalid"}"""
                    : """<NotAResource xmlns="http://hl7.org/fhir"><id value="invalid"/></NotAResource>""";
                // Native DynamicResource parsing reaches Candle's existing type/location rejection.
                // TestResourceInvalidElement pins failure, not a parser-specific issue code (see corpus README).
                code = "invalid";
                diagnostics = "Resource type: NotAResource does not match request: Patient";
                checkMediaType = true;
                break;
            case "invalid-primitive":
                content = format == CompatibilityFormat.Json
                    ? """{"resourceType":"Patient","active":"not-a-boolean"}"""
                    : """<Patient xmlns="http://hl7.org/fhir"><active value="not-a-boolean"/></Patient>""";
                break;
            case "wrong-create-location":
            case "wrong-update-location":
                content = ExchangeFixture(version, format, "Patient");
                path = "Observation";
                if (failure == "wrong-update-location")
                {
                    content = WithWireId(content, format, "wrong-location");
                    method = "PUT";
                    path += "/wrong-location";
                }
                code = "invalid";
                diagnostics = "Resource type: Patient does not match request: Observation";
                checkMediaType = true;
                break;
            case "non-bundle":
                content = ExchangeFixture(version, format, "Patient");
                path = string.Empty;
                code = "invalid";
                diagnostics = "Cannot process non-Bundle resource type (Patient) as a Bundle";
                break;
            case "convert-malformed":
            case "unknown-format-malformed":
                path = "$convert";
                status = HttpStatusCode.UnsupportedMediaType;
                code = "invalid";
                checkMediaType = true;
                if (failure == "unknown-format-malformed")
                {
                    sourceMediaType = "text/plain";
                    content = "neither JSON nor XML";
                    diagnostics = "Operation $convert does not consume non-FHIR content.";
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failure));
        }
        CompatibilityResponse response = await replay.SendAsync(new CompatibilityRequest(tenant.Name, method, path)
        {
            Format = format, Content = content, SourceMediaType = sourceMediaType,
        });
        AssertExchangeFailureResponse(replay.Bindings, tenant, format, failure, response, status, code, diagnostics, checkMediaType);
        if (diagnostics is null)
        {
            string outcome = transport == CompatibilityTransport.Http ? response.HttpBody! : response.Direct!.SerializedOutcome;
            string issue = Assert.Single(ExchangeRepeated(outcome, format, "issue"));
            string actualDiagnostic = ExchangePrimitive(issue, format, "diagnostics");
            Assert.StartsWith(path == "$convert"
                ? "Operation $convert does not consume non-FHIR content."
                : "Failed to deserialize resource, format: ", actualDiagnostic, StringComparison.Ordinal);
        }
        await AssertEmptyExchangeSearches(replay, tenant, format, "invalid-input-has-no-side-effects");
    }

    /// <summary>Pin the non-empty matrix, including the store-only explicit-ID override.</summary>
    [Fact]
    public void ExchangeTheoryDataIsNonEmptyAndLabeled()
    {
        Assert.Equal(12, ExchangeCases.Count());
        Assert.Equal(24, ExchangePolicyCases.Count());
        Assert.Equal(12, ExchangeExplicitCreateCases.Count());
        Assert.Equal(24, ExchangeBundleCases.Count());
        Assert.Equal(144, ExchangeSerializationCases.Count());
        Assert.Equal(36, ExchangeConversionCases.Count());
        Assert.Equal(96, ExchangeInvalidCases.Count());
        Assert.Equal(40, SummaryMetadataControls.Count());
        Assert.Equal(8, SummaryMetadataControls.Count(row => (bool)row[4]));
        foreach (IEnumerable<object[]> rows in new[]
        {
            ExchangeCases, ExchangePolicyCases, ExchangeExplicitCreateCases, ExchangeBundleCases,
            ExchangeSerializationCases, ExchangeConversionCases, ExchangeInvalidCases,
        })
        {
            object[][] cases = rows.ToArray();
            Assert.Equal(cases.Length, cases.Select(row => (string)row[0]).Distinct(StringComparer.Ordinal).Count());
            Assert.All(cases, row =>
            {
                Assert.Contains(row[1].ToString()!, (string)row[0], StringComparison.Ordinal);
                Assert.Contains(row[2].ToString()!, (string)row[0], StringComparison.Ordinal);
                Assert.Contains(row[3].ToString()!, (string)row[0], StringComparison.Ordinal);
            });
        }
    }

    private static void AssertSummaryExchangeBundle(
        CompatibilityReplay replay,
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        string original,
        GeneratedResourceExpectation generated,
        string flag,
        string body)
    {
        Assert.Equal("Bundle", ExchangeResourceType(body, format));
        Assert.Equal("searchset", ExchangePrimitive(body, format, "type"));
        AssertExchangeMembers(body, format, flag == "count"
            ? ["resourceType", "type", "total", "link"]
            : ["resourceType", "type", "total", "link", "entry"]);
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(body);
            Assert.Equal("1", document.RootElement.GetProperty("total").GetRawText());
        }
        else
        {
            Assert.Equal("1", ExchangePrimitive(body, format, "total"));
        }
        string link = Assert.Single(ExchangeRepeated(body, format, "link"));
        AssertExchangeMembers(link, format, ["relation", "url"]);
        Assert.Equal("self", ExchangePrimitive(link, format, "relation"));
        Assert.Equal($"{tenant.BaseUrl}/{generated.ResourceType}?_summary={flag}", ExchangePrimitive(link, format, "url"));
        if (flag == "count")
        {
            Assert.False(ExchangeHasMember(body, format, "entry"));
            return;
        }

        string entry = Assert.Single(ExchangeRepeated(body, format, "entry"));
        AssertExchangeMembers(entry, format, ["fullUrl", "resource", "search"]);
        Assert.Equal(replay.Bindings.Resources[generated.Name].Location, ExchangePrimitive(entry, format, "fullUrl"));
        string search = ExchangeChild(entry, format, "search");
        AssertExchangeMembers(search, format, ["mode"]);
        Assert.Equal("match", ExchangePrimitive(search, format, "mode"));
        string resource = ExchangeNestedResource(entry, format, "resource");
        AssertSummaryExchangeResource(replay, tenant, format, original, generated, flag, resource);
    }

    private static void AssertSummaryExchangeRead(
        CompatibilityReplay replay,
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        string original,
        GeneratedResourceExpectation generated,
        string flag,
        string name,
        CompatibilityResponse response)
    {
        WireContractAssertions.Status(HttpStatusCode.OK, response.Status);
        Assert.Equal(WireContractAssertions.MediaType(format), response.MediaType);
        AssertSummaryExchangeResource(replay, tenant, format, original, generated, flag, ExchangeResource(response));
        GeneratedResourceBinding binding = replay.Bindings.Resources[generated.Name];
        if (response.Transport == CompatibilityTransport.Http)
        {
            Assert.Null(response.Direct);
            WireContractAssertions.ResourceHeaders(binding,
                Assert.Single(response.HttpHeaders["ETag"]),
                Assert.Single(response.HttpHeaders["Location"]),
                Assert.Single(response.HttpHeaders["Last-Modified"]));
            return;
        }

        Assert.Null(response.HttpBody);
        Assert.Empty(response.HttpHeaders);
        CompatibilityDirectResponse direct = Assert.IsType<CompatibilityDirectResponse>(response.Direct);
        Assert.True(direct.Success);
        Assert.Equal(binding.ResourceType, direct.ResourceType);
        Assert.Equal(binding.Id, direct.Id);
        WireContractAssertions.ResourceHeaders(binding, direct.ETag, direct.Location, direct.LastModified);
        WireContractAssertions.Body(new OutcomeBodyExpectation(name + "-outcome",
            [new("information", "success", $"Read {binding.ResourceType}/{binding.Id}")]),
            direct.SerializedOutcome, format, tenant.BaseUrl, replay.Bindings);
    }

    private static void AssertSummaryExchangeResource(
        CompatibilityReplay replay,
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        string original,
        GeneratedResourceExpectation generated,
        string flag,
        string resource)
    {
        Assert.Equal(generated.ResourceType, ExchangeResourceType(resource, format));
        GeneratedResourceExpectation reuse = generated with { Mode = CompatibilityBindingMode.Reuse };
        if (flag == "false")
        {
            WireContractAssertions.Body(new ResourceBodyExpectation(original, reuse), resource, format, tenant.BaseUrl, replay.Bindings);
            return;
        }

        WireContractAssertions.BindResource(reuse, resource, format, tenant.BaseUrl, replay.Bindings);
        AssertSummaryExchangeMetadata(original, resource, format);
        Assert.Contains(generated.ResourceType, new[] { "Patient", "Observation" });
        if (flag == "text")
        {
            // Patient has no mandatory data elements; Observation.status and Observation.code are 1..1.
            string[] required = generated.ResourceType == "Observation" ? ["status", "code"] : [];
            string[] narrative = ExchangeHasMember(original, format, "text") ? ["text"] : [];
            AssertExchangeMembers(resource, format, ["resourceType", "id", "meta", .. narrative, .. required]);
            foreach (string name in narrative.Concat(required))
            {
                AssertExchangeMemberContent(original, resource, format, name);
            }
            return;
        }

        Assert.False(ExchangeHasMember(resource, format, "text"), $"_summary={flag} must omit narrative.");
        if (flag == "data")
        {
            // The expected side is every submitted non-narrative member, not the serializer's output.
            string[] members = ExchangeMemberNames(original, format).Where(name => name != "text")
                .Concat(["resourceType", "id", "meta"]).Distinct(StringComparer.Ordinal).ToArray();
            AssertExchangeMembers(resource, format, members);
            foreach (string name in members.Where(name => name is not ("resourceType" or "id" or "meta")))
            {
                AssertExchangeMemberContent(original, resource, format, name);
            }
            return;
        }

        Assert.Equal("true", flag);
        // R4/R4B/R5 Patient and Observation element definitions mark these submitted fields isSummary.
        // Element.extension, Patient.multipleBirth[x]/generalPractitioner/photo, and Resource.language are not.
        string[] summaryMembers = generated.ResourceType == "Patient"
            ? ["identifier", "active", "name", "gender", "birthDate", "managingOrganization"]
            : ["identifier", "status", "code", "subject", "effectiveDateTime", "valueQuantity", "derivedFrom", "component"];
        AssertExchangeMembers(resource, format, ["resourceType", "id", "meta", .. summaryMembers]);
        foreach (string name in summaryMembers.Where(name => name != "birthDate"))
        {
            AssertExchangeMemberContent(original, resource, format, name);
        }
        if (generated.ResourceType == "Patient")
        {
            Assert.Equal("1980-02", ExchangePrimitive(resource, format, "birthDate"));
            WireContractAssertions.Content(format == CompatibilityFormat.Json
                ? "\"1980-02\""
                : """<birthDate xmlns="http://hl7.org/fhir" value="1980-02"/>""",
                ExchangeChild(resource, format, "birthDate"), format);
        }
    }

    internal static void AssertSummaryExchangeMetadata(
        string original,
        string summarized,
        CompatibilityFormat format,
        bool storedResource = true)
    {
        string originalMetadata = ExchangeHasMember(original, format, "meta")
            ? ExchangeChild(original, format, "meta")
            : format == CompatibilityFormat.Json ? "{}" : """<meta xmlns="http://hl7.org/fhir"/>""";
        string metadata = ExchangeChild(summarized, format, "meta");
        string[] originalMembers = ExchangeMemberNames(originalMetadata, format);
        string[] generatedMembers = storedResource ? ["versionId", "lastUpdated", "tag"] : ["tag"];
        AssertExchangeMembers(metadata, format, originalMembers.Concat(generatedMembers).Distinct(StringComparer.Ordinal).ToArray());
        foreach (string name in originalMembers.Where(name => name != "tag"))
        {
            AssertExchangeMemberContent(originalMetadata, metadata, format, name);
        }

        string[] expectedTags = ExchangeHasMember(originalMetadata, format, "tag")
            ? ExchangeRepeated(originalMetadata, format, "tag")
            : [];
        string[] actualTags = ExchangeRepeated(metadata, format, "tag");
        Assert.Equal(expectedTags.Length + 2, actualTags.Length);
        for (int index = 0; index < expectedTags.Length; index++)
        {
            WireContractAssertions.Content(expectedTags[index], actualTags[index], format);
        }

        // User-reviewed native representation, not a code-only uniqueness or global array-order rule.
        string[] systems =
        [
            "http://hl7.org/fhir/v3/ObservationValue",
            "http://terminology.hl7.org/CodeSystem/v3-ObservationValue",
        ];
        string[] addedSystems = new string[2];
        for (int index = 0; index < addedSystems.Length; index++)
        {
            string tag = actualTags[expectedTags.Length + index];
            AssertExchangeMembers(tag, format, ["system", "code"]);
            string system = ExchangePrimitive(tag, format, "system");
            Assert.Contains(system, systems);
            Assert.Equal("SUBSETTED", ExchangePrimitive(tag, format, "code"));
            string expected = format == CompatibilityFormat.Json
                ? $$"""{"system":"{{system}}","code":"SUBSETTED"}"""
                : $"""<tag xmlns="http://hl7.org/fhir"><system value="{system}"/><code value="SUBSETTED"/></tag>""";
            WireContractAssertions.Content(expected, tag, format);
            addedSystems[index] = system;
        }
        Assert.Equal(systems.Order(StringComparer.Ordinal), addedSystems.Order(StringComparer.Ordinal));
    }

    private static void AssertExchangeMemberContent(string expected, string actual, CompatibilityFormat format, string name)
    {
        if (format == CompatibilityFormat.Json)
        {
            WireContractAssertions.Content(ExchangeChild(expected, format, name), ExchangeChild(actual, format, name), format);
            return;
        }
        XElement[] expectedElements = ExchangeXml(expected).Elements(_exchangeFhir + name).ToArray();
        XElement[] actualElements = ExchangeXml(actual).Elements(_exchangeFhir + name).ToArray();
        Assert.NotEmpty(expectedElements);
        Assert.Equal(expectedElements.Length, actualElements.Length);
        for (int index = 0; index < expectedElements.Length; index++)
        {
            WireContractAssertions.Content(expectedElements[index].ToString(SaveOptions.DisableFormatting),
                actualElements[index].ToString(SaveOptions.DisableFormatting), format);
        }
    }

    private static void AssertExchangeFormatting(string body, CompatibilityFormat format, bool pretty)
    {
        if (format == CompatibilityFormat.Json)
        {
            Assert.Equal(pretty, body.Contains('\n'));
        }
        else
        {
            // Do not confuse a narrative's required mixed-content newline with serializer indentation.
            bool hasIndentation = ExchangeXml(body).Nodes().OfType<XText>()
                .Any(text => string.IsNullOrWhiteSpace(text.Value) && text.Value.Contains('\n'));
            Assert.Equal(pretty, hasIndentation);
        }
    }

    private static async Task AssertEmptyExchangeSearches(
        CompatibilityReplay replay,
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        string name)
    {
        foreach (string type in new[] { "Patient", "Observation" })
        {
            string content = format == CompatibilityFormat.Json
                ? $$$"""{"resourceType":"Bundle","type":"searchset","total":0,"link":[{"relation":"self","url":"{{{tenant.BaseUrl}}}/{{{type}}}"}]}"""
                : $"""<Bundle xmlns="http://hl7.org/fhir"><type value="searchset"/><total value="0"/><link><relation value="self"/><url value="{tenant.BaseUrl}/{type}"/></link></Bundle>""";
            ExactBodyExpectation empty = new(content);
            await replay.StepAsync(new CompatibilityStep(name + "-" + type,
                new CompatibilityRequest(tenant.Name, "GET", type) { Format = format },
                new CompatibilityResponseExpectation(HttpStatusCode.OK, true, empty, empty,
                    new OutcomeBodyExpectation(name + "-" + type + "-outcome", [new("information", "success", "Type search successful")]))));
        }
    }

    private sealed record ExchangeBundleRequest(
        string FullUrl,
        string? Method,
        string? Url,
        string? Content = null,
        string? IfNoneExist = null,
        string? IfMatch = null);

    private sealed record ExchangeBundleEntry(
        string FullUrl,
        HttpStatusCode Status,
        string? Content = null,
        GeneratedResourceExpectation? Generated = null,
        IReadOnlyList<OutcomeIssueExpectation>? Issues = null,
        bool ResourceHeaders = true,
        string? PatientReferenceBinding = null);

    private static string BundleFullUrl(int number) => $"urn:uuid:00000000-0000-0000-0000-{number:000000000000}";

    private static string ExchangeBundleContent(string type, CompatibilityFormat format, IReadOnlyList<ExchangeBundleRequest> entries)
    {
        if (format == CompatibilityFormat.Json)
        {
            using MemoryStream buffer = new();
            using (Utf8JsonWriter writer = new(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("resourceType", "Bundle");
                writer.WriteString("type", type);
                writer.WriteStartArray("entry");
                foreach (ExchangeBundleRequest entry in entries)
                {
                    writer.WriteStartObject();
                    writer.WriteString("fullUrl", entry.FullUrl);
                    if (entry.Content is not null)
                    {
                        writer.WritePropertyName("resource");
                        writer.WriteRawValue(entry.Content);
                    }
                    if (entry.Method is not null)
                    {
                        writer.WriteStartObject("request");
                        writer.WriteString("method", entry.Method);
                        writer.WriteString("url", entry.Url);
                        if (entry.IfNoneExist is not null)
                        {
                            writer.WriteString("ifNoneExist", entry.IfNoneExist);
                        }
                        if (entry.IfMatch is not null)
                        {
                            writer.WriteString("ifMatch", entry.IfMatch);
                        }
                        writer.WriteEndObject();
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        XElement bundle = new(_exchangeFhir + "Bundle", new XElement(_exchangeFhir + "type", new XAttribute("value", type)));
        foreach (ExchangeBundleRequest entry in entries)
        {
            XElement element = new(_exchangeFhir + "entry",
                new XElement(_exchangeFhir + "fullUrl", new XAttribute("value", entry.FullUrl)));
            if (entry.Content is not null)
            {
                element.Add(new XElement(_exchangeFhir + "resource", ExchangeXml(entry.Content)));
            }
            if (entry.Method is not null)
            {
                XElement request = new(_exchangeFhir + "request",
                    new XElement(_exchangeFhir + "method", new XAttribute("value", entry.Method)),
                    new XElement(_exchangeFhir + "url", new XAttribute("value", entry.Url!)));
                if (entry.IfNoneExist is not null)
                {
                    request.Add(new XElement(_exchangeFhir + "ifNoneExist", new XAttribute("value", entry.IfNoneExist)));
                }
                if (entry.IfMatch is not null)
                {
                    request.Add(new XElement(_exchangeFhir + "ifMatch", new XAttribute("value", entry.IfMatch)));
                }
                element.Add(request);
            }
            bundle.Add(element);
        }
        return bundle.ToString(SaveOptions.DisableFormatting);
    }

    private static void AssertExchangeBundle(
        CompatibilityReplay replay,
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        string name,
        CompatibilityResponse response,
        string type,
        IReadOnlyList<ExchangeBundleEntry> expected)
    {
        AssertExchangeContainer(replay, tenant, format, name, response, string.Empty, string.Empty,
            new OutcomeBodyExpectation(name + "-outer-outcome",
                [new("information", "success", $"Processed {(type == "batch-response" ? "Batch" : "Transaction")} bundle")]));
        string body = ExchangeResource(response);
        Assert.Equal("Bundle", ExchangeResourceType(body, format));
        AssertExchangeMembers(body, format, ["resourceType", "id", "type", "entry"]);
        Assert.Equal(type, ExchangePrimitive(body, format, "type"));
        string id = ExchangePrimitive(body, format, "id");
        Assert.True(Guid.TryParseExact(id, "D", out _), "The response Bundle must have a generated UUID id.");
        Assert.True(replay.Bindings.Values.TryAdd(name + "-bundle-id", new(name + "-bundle-id", id)));
        string[] entries = ExchangeRepeated(body, format, "entry");
        Assert.Equal(expected.Count, entries.Length);
        for (int index = 0; index < expected.Count; index++)
        {
            ExchangeBundleEntry entry = expected[index];
            string actual = entries[index];
            AssertExchangeMembers(actual, format, entry.Content is null ? ["fullUrl", "response"] : ["fullUrl", "resource", "response"]);
            Assert.Equal(entry.FullUrl, ExchangePrimitive(actual, format, "fullUrl"));
            string result = ExchangeChild(actual, format, "response");
            Assert.Equal($"{(int)entry.Status} {entry.Status}", ExchangePrimitive(result, format, "status"));
            string outcome = ExchangeNestedResource(result, format, "outcome");
            WireContractAssertions.Body(new OutcomeBodyExpectation($"{name}-entry-{index}-outcome",
                entry.Issues ?? [new("information", "success")]), outcome, format, tenant.BaseUrl, replay.Bindings);

            if (entry.Content is null)
            {
                Assert.Null(entry.Generated);
                AssertExchangeMembers(result, format, ["status", "outcome"]);
                continue;
            }

            GeneratedResourceExpectation generated = Assert.IsType<GeneratedResourceExpectation>(entry.Generated);
            string content = entry.Content;
            if (entry.PatientReferenceBinding is string referenceBinding)
            {
                GeneratedResourceBinding target = replay.Bindings.Resources[referenceBinding];
                Assert.Equal(tenant.BaseUrl, target.BaseUrl);
                content = ReplaceFixtureValue(content, "Patient/wire-patient", $"Patient/{target.Id}");
            }
            string resource = ExchangeNestedResource(actual, format, "resource");
            WireContractAssertions.Body(new ResourceBodyExpectation(content, generated), resource, format, tenant.BaseUrl, replay.Bindings);
            GeneratedResourceBinding bound = replay.Bindings.Resources[generated.Name];
            Assert.Equal(bound.LastUpdated, ExchangePrimitive(result, format, "lastModified"));
            if (entry.ResourceHeaders)
            {
                AssertExchangeMembers(result, format, ["status", "location", "etag", "lastModified", "outcome"]);
                Assert.Equal(bound.Location, ExchangePrimitive(result, format, "location"));
                Assert.Equal($"W/\"{bound.Version}\"", ExchangePrimitive(result, format, "etag"));
            }
            else
            {
                AssertExchangeMembers(result, format, ["status", "lastModified", "outcome"]);
            }
        }
    }

    private static void AssertExchangeContainer(
        CompatibilityReplay replay,
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        string name,
        CompatibilityResponse response,
        string directResourceType,
        string directId,
        OutcomeBodyExpectation? outcome = null,
        string? mediaType = null)
    {
        WireContractAssertions.Status(HttpStatusCode.OK, response.Status);
        Assert.Equal(mediaType ?? WireContractAssertions.MediaType(format), response.MediaType);
        Assert.NotEmpty(ExchangeResource(response));
        if (response.Transport == CompatibilityTransport.Http)
        {
            Assert.Null(response.Direct);
            foreach (CompatibilityHeaderExpectation header in NoExchangeHeaders())
            {
                Assert.False(response.HttpHeaders.ContainsKey(header.Name));
            }
            return;
        }
        CompatibilityDirectResponse direct = Assert.IsType<CompatibilityDirectResponse>(response.Direct);
        Assert.True(direct.Success);
        Assert.Equal(directResourceType, direct.ResourceType);
        Assert.Equal(directId, direct.Id);
        Assert.Empty(direct.ETag);
        Assert.Empty(direct.Location);
        Assert.Empty(direct.LastModified);
        WireContractAssertions.Body(outcome ?? SuccessOutcome(name), direct.SerializedOutcome, format, tenant.BaseUrl, replay.Bindings);
    }

    private static string ExchangeResourceType(string content, CompatibilityFormat format) =>
        format == CompatibilityFormat.Json
            ? ExchangePrimitive(content, format, "resourceType")
            : ExchangeXml(content).Name.LocalName;

    private static string ExchangeChild(string content, CompatibilityFormat format, string name)
    {
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(content);
            return document.RootElement.GetProperty(name).GetRawText();
        }
        return Assert.Single(ExchangeXml(content).Elements(_exchangeFhir + name)).ToString(SaveOptions.DisableFormatting);
    }

    private static string ExchangeNestedResource(string content, CompatibilityFormat format, string name)
    {
        string child = ExchangeChild(content, format, name);
        return format == CompatibilityFormat.Json
            ? child
            : Assert.Single(ExchangeXml(child).Elements()).ToString(SaveOptions.DisableFormatting);
    }

    private static string[] ExchangeRepeated(string content, CompatibilityFormat format, string name)
    {
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(content);
            return document.RootElement.GetProperty(name).EnumerateArray().Select(element => element.GetRawText()).ToArray();
        }
        return ExchangeXml(content).Elements(_exchangeFhir + name).Select(element => element.ToString(SaveOptions.DisableFormatting)).ToArray();
    }

    private static bool ExchangeHasMember(string content, CompatibilityFormat format, string name)
    {
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(content);
            return document.RootElement.TryGetProperty(name, out _);
        }
        return ExchangeXml(content).Elements(_exchangeFhir + name).Any();
    }

    private static void AssertExchangeMembers(string content, CompatibilityFormat format, string[] expected)
    {
        string[] actual = ExchangeMemberNames(content, format);
        if (format == CompatibilityFormat.Xml)
        {
            expected = expected.Where(name => name != "resourceType").ToArray();
        }
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }

    private static string[] ExchangeMemberNames(string content, CompatibilityFormat format)
    {
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(content);
            string[] actual = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
            Assert.Equal(actual.Length, actual.Distinct(StringComparer.Ordinal).Count());
            return actual;
        }

        XElement root = ExchangeXml(content);
        Assert.All(root.Elements(), element => Assert.Equal(_exchangeFhir, element.Name.Namespace));
        return root.Elements().Select(element => element.Name.LocalName).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static async Task<GeneratedResourceExpectation> UpdateExchangeResource(
        CompatibilityReplay replay,
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        GeneratedResourceExpectation original,
        string content)
    {
        GeneratedResourceBinding identity = replay.Bindings.Resources[original.Name];
        GeneratedResourceExpectation updated = new(original.ResourceType.ToLowerInvariant() + "-v2",
            original.ResourceType, "2", identity.Id, PreviousBinding: original.Name);
        await replay.StepAsync(new CompatibilityStep("update-" + original.ResourceType,
            new CompatibilityRequest(tenant.Name, "PUT", string.Empty)
            {
                Format = format, Content = content, TargetBinding = original.Name, IfMatchBinding = original.Name,
            }, ResourceResponse("update-" + original.ResourceType, content, updated, HttpStatusCode.OK)));
        await replay.StepAsync(BoundRead("read-updated-" + original.ResourceType, tenant, format, content, updated));
        return updated;
    }

    private static async Task DeleteExchangeResource(
        CompatibilityReplay replay,
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        string content,
        GeneratedResourceExpectation generated)
    {
        await replay.StepAsync(new CompatibilityStep("delete-" + generated.ResourceType,
            new CompatibilityRequest(tenant.Name, "DELETE", string.Empty) { Format = format, TargetBinding = generated.Name },
            ResourceResponse("delete-" + generated.ResourceType, content, generated with { Mode = CompatibilityBindingMode.Reuse },
                HttpStatusCode.OK, resourceHeaders: false)));
        string id = replay.Bindings.Resources[generated.Name].Id;
        await AssertExchangeFailure(replay, tenant, format, "missing-" + generated.ResourceType,
            new CompatibilityRequest(tenant.Name, "GET", $"{generated.ResourceType}/{id}") { Format = format },
            HttpStatusCode.NotFound, "exception", $"Resource: {generated.ResourceType}/{id} not found");
    }

    private static string ExchangeFixture(
        FhirReleases.FhirSequenceCodes version,
        CompatibilityFormat format,
        string resourceType,
        string reference = "Patient/wire-patient")
    {
        string content;
        if (resourceType == "Binary" && format == CompatibilityFormat.Xml)
        {
            // Independently authored XML; there is deliberately no JSON-to-XML expectation generator.
            content = $"""
                <Binary xmlns="http://hl7.org/fhir">
                  <meta><tag><system value="https://example.test/corpus"/><code value="synthetic-{VersionName(version)}"/></tag></meta>
                  <contentType value="application/octet-stream"/>
                  <securityContext><reference value="Patient/wire-patient"/></securityContext>
                  <data value="AAECA/7/QUJDCg=="/>
                </Binary>
                """;
        }
        else
        {
            content = CompatibilityReplay.LoadFixture(version,
                resourceType.ToLowerInvariant() + (format == CompatibilityFormat.Json ? ".json" : ".xml"));
        }

        return resourceType == "Patient"
            ? content
            : ReplaceFixtureValue(content, "Patient/wire-patient", reference);
    }

    private static string ReplaceFixtureValue(string content, string original, string replacement)
    {
        int position = content.IndexOf(original, StringComparison.Ordinal);
        Assert.True(position >= 0, $"The authored input must contain {original}.");
        Assert.Equal(-1, content.IndexOf(original, position + original.Length, StringComparison.Ordinal));
        return content.Replace(original, replacement, StringComparison.Ordinal);
    }

    private static string WithWireId(string content, CompatibilityFormat format, string id)
    {
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(content);
            Assert.False(document.RootElement.TryGetProperty("id", out _), "Only an authored id-less input can receive an explicit id.");
            using MemoryStream buffer = new();
            using (Utf8JsonWriter writer = new(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("id", id);
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    property.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        XElement root = ExchangeXml(content);
        Assert.Null(root.Element(_exchangeFhir + "id"));
        root.AddFirst(new XElement(_exchangeFhir + "id", new XAttribute("value", id)));
        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static string PatientLanguage(string content, CompatibilityFormat format, string language)
    {
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(content);
            Assert.Equal("en", document.RootElement.GetProperty("language").GetString());
            using MemoryStream buffer = new();
            using (Utf8JsonWriter writer = new(buffer))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    if (property.Name == "language")
                    {
                        writer.WriteString("language", language);
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        XElement root = ExchangeXml(content);
        XElement element = Assert.Single(root.Elements(_exchangeFhir + "language"));
        Assert.Equal("en", (string?)element.Attribute("value"));
        element.SetAttributeValue("value", language);
        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement ExchangeXml(string content)
    {
        using StringReader text = new(content);
        using XmlReader reader = XmlReader.Create(text, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XElement.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static string ExchangeResource(CompatibilityResponse response) =>
        response.Transport == CompatibilityTransport.Http
            ? response.HttpBody ?? throw new InvalidOperationException("HTTP body is missing.")
            : response.Direct?.SerializedResource ?? throw new InvalidOperationException("Direct resource field is missing.");

    private static string ExchangePrimitive(string content, CompatibilityFormat format, string name)
    {
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(content);
            return document.RootElement.GetProperty(name).GetString() ?? throw new InvalidOperationException($"Missing {name}.");
        }
        return (string?)Assert.Single(ExchangeXml(content).Elements(_exchangeFhir + name)).Attribute("value")
            ?? throw new InvalidOperationException($"Missing {name}/@value.");
    }

    private static void AssertBinaryBytes(CompatibilityResponse response, CompatibilityFormat format, byte[] expected) =>
        Assert.Equal(expected, Convert.FromBase64String(ExchangePrimitive(ExchangeResource(response), format, "data")));

    private static CompatibilityHeaderExpectation[] NoExchangeHeaders() =>
        [new("ETag", null), new("Location", null), new("Last-Modified", null)];

    private static async Task AssertExchangeFailure(
        CompatibilityReplay replay,
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        string stepId,
        CompatibilityRequest request,
        HttpStatusCode status,
        string code,
        string? diagnostics,
        bool checkMediaType = true)
    {
        CompatibilityResponse response = await replay.SendAsync(request);
        AssertExchangeFailureResponse(replay.Bindings, tenant, format, stepId, response, status, code, diagnostics, checkMediaType);
    }

    private static void AssertExchangeFailureResponse(
        CompatibilityBindings bindings,
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        string stepId,
        CompatibilityResponse response,
        HttpStatusCode status,
        string code,
        string? diagnostics,
        bool checkMediaType)
    {
        WireContractAssertions.Status(status, response.Status);
        if (checkMediaType)
        {
            Assert.Equal(WireContractAssertions.MediaType(format), response.MediaType);
        }
        OutcomeBodyExpectation outcome = new(stepId + "-outcome", [new("error", code, diagnostics)]);
        if (response.Transport == CompatibilityTransport.Http)
        {
            Assert.Null(response.Direct);
            WireContractAssertions.Body(outcome, response.HttpBody!, format, tenant.BaseUrl, bindings);
            foreach (CompatibilityHeaderExpectation header in NoExchangeHeaders())
            {
                Assert.False(response.HttpHeaders.ContainsKey(header.Name), $"{stepId}: unexpected {header.Name}");
            }
        }
        else
        {
            CompatibilityDirectResponse direct = Assert.IsType<CompatibilityDirectResponse>(response.Direct);
            Assert.False(direct.Success);
            Assert.Empty(direct.SerializedResource);
            Assert.Empty(direct.ResourceType);
            Assert.Empty(direct.Id);
            Assert.Empty(direct.ETag);
            Assert.Empty(direct.Location);
            Assert.Empty(direct.LastModified);
            WireContractAssertions.Body(outcome, direct.SerializedOutcome, format, tenant.BaseUrl, bindings);
        }
    }

    private static void AssertDirectExchangeResponse(
        CompatibilityResponseExpectation expected,
        bool success,
        FhirResponseContext response,
        CompatibilityFormat format,
        CompatibilityTenant tenant,
        CompatibilityBindings bindings) =>
        WireContractAssertions.Response(expected,
            new CompatibilityResponse(CompatibilityTransport.Direct, response.StatusCode!.Value, response.MimeType)
            {
                Direct = new(success, response.SerializedResource, response.SerializedOutcome,
                    response.ResourceType, response.Id, response.ETag, response.LastModified, response.Location),
            }, format, tenant.BaseUrl, bindings);
}

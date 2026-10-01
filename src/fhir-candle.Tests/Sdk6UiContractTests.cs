// <copyright file="Sdk6UiContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using fhir.candle.Tests.Controllers;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;

namespace fhir.candle.Tests;

/// <summary>Independent contracts for the store data consumed by the current UI, not browser tests.</summary>
[Collection(CompatibilityHostCollection.Name)]
public class Sdk6UiContractTests
{
    private const string Evidence =
        "Pages/Store/FhirStorePage.GetFilteredItems, ResourceList.GetFilteredItems, ResourceViewer.LoadResourceContent; " +
        "IResourceStore.GetInstanceTableView/InstanceTableRec; ResourceStore.GetName/GetIdentifier/DisplayFor; " +
        "FhirStoreTests.TestPatientCRUD/PutOnMissingLenientCreatesWith201; authored Patient/Observation fixture pairs.";

    private static readonly FhirReleases.FhirSequenceCodes[] _versions =
    [
        FhirReleases.FhirSequenceCodes.R4,
        FhirReleases.FhirSequenceCodes.R4B,
        FhirReleases.FhirSequenceCodes.R5,
    ];

    private static readonly CompatibilityFormat[] _viewerFormats =
    [
        CompatibilityFormat.Json,
        CompatibilityFormat.Xml,
        CompatibilityFormat.Json,
    ];

    private static readonly XNamespace _fhir = "http://hl7.org/fhir";

    /// <summary>All three versions ingest both authored formats before exposing table data.</summary>
    public static IEnumerable<object[]> TableCases => UiRows("tables");

    /// <summary>Each input format independently exercises the viewer's JSON, XML, JSON sequence.</summary>
    public static IEnumerable<object[]> ViewerCases => UiRows("viewer");

    /// <summary>Counts and every display column track creates, updates, and deletes without sibling leakage.</summary>
    [Theory]
    [MemberData(nameof(TableCases))]
    public async Task StoreAndResourceTablesPreserveDisplayValues(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        string suffix = version.ToString().ToLowerInvariant();
        CompatibilityTenant tenant = Tenant(version, $"ui-tables-{suffix}");
        CompatibilityTenant sibling = Tenant(version, $"ui-tables-sibling-{suffix}");
        string patient = PatientFixture(version, format, "ui-patient");
        string siblingPatient = PatientFixture(version, format, "ui-patient", sibling: true);
        string observation = ObservationFixture(version, format, "Patient/ui-patient", "ui-observation");
        string profile = ProfileContent(version, format);
        GeneratedResourceExpectation patientV1 = new("patient-v1", "Patient", "1", "ui-patient");
        GeneratedResourceExpectation observationV1 = new("observation-v1", "Observation", "1", "ui-observation");
        GeneratedResourceExpectation profileV1 = new("profile-v1", "StructureDefinition", "1", "ui-profile");
        CompatibilityScenario scenario = new(label, Evidence, [tenant, sibling],
        [
            ResourceStep(tenant, format, "PUT", "Patient/ui-patient", patient, patientV1, HttpStatusCode.Created),
            ResourceStep(tenant, format, "PUT", "Observation/ui-observation", observation, observationV1, HttpStatusCode.Created),
            ResourceStep(tenant, format, "PUT", "StructureDefinition/ui-profile", profile, profileV1, HttpStatusCode.Created),
            ResourceStep(sibling, format, "PUT", "Patient/ui-patient", siblingPatient,
                patientV1 with { Name = "sibling-patient-v1" }, HttpStatusCode.Created),
            ResourceStep(sibling, format, "PUT", "Observation/ui-observation", observation,
                observationV1 with { Name = "sibling-observation-v1" }, HttpStatusCode.Created),
            ResourceStep(sibling, format, "PUT", "StructureDefinition/ui-profile", profile,
                profileV1 with { Name = "sibling-profile-v1" }, HttpStatusCode.Created),
        ]);
        await using CompatibilityReplay replay = new(scenario, CompatibilityTransport.Direct);
        IFhirStore store = replay.GetOwnedStore(tenant.Name);
        IFhirStore siblingStore = replay.GetOwnedStore(sibling.Name);
        AssertTableCounts(store, 0, 0, 0);
        AssertTableCounts(siblingStore, 0, 0, 0);
        Assert.True(store["Patient"].ResourcesHaveName);
        Assert.True(store["Patient"].ResourcesAreIdentifiable);
        Assert.False(store["Patient"].ResourcesAreConformance);
        Assert.False(store["Observation"].ResourcesHaveName);
        Assert.True(store["Observation"].ResourcesAreIdentifiable);
        Assert.False(store["Observation"].ResourcesAreConformance);
        Assert.True(store["StructureDefinition"].ResourcesHaveName);
        Assert.True(store["StructureDefinition"].ResourcesAreIdentifiable);
        Assert.True(store["StructureDefinition"].ResourcesAreConformance);

        await replay.RunAsync();
        InstanceTableRec patientRow = new()
        {
            Id = "ui-patient",
            Name = "Wire, Avery Quinn",
            Identifiers = $"https://example.test/identifier|wire-{suffix}, https://example.test/secondary|second-{suffix}",
        };
        InstanceTableRec siblingPatientRow = patientRow with
        {
            Name = "Sibling, Avery Quinn",
            Identifiers = $"https://example.test/identifier|sibling-{suffix}, https://example.test/secondary|second-{suffix}",
        };
        InstanceTableRec observationRow = new()
        {
            Id = "ui-observation",
            Identifiers = $"https://example.test/identifier|quantity-{suffix}",
        };
        InstanceTableRec profileRow = new()
        {
            Id = "ui-profile",
            Name = "UiPatientProfile",
            Url = "https://example.test/fhir/StructureDefinition/ui-profile",
            Description = "Synthetic table profile.",
            Identifiers = "https://example.test/ui-profiles|ui-profile",
        };
        AssertTableCounts(store, 1, 1, 1);
        AssertTableRow(store["Patient"], patientRow);
        AssertTableRow(store["Observation"], observationRow);
        AssertTableRow(store["StructureDefinition"], profileRow);
        AssertSiblingTables();

        // These are the same case-insensitive column predicates used by ResourceList.
        InstanceTableRec[] filteredPatients = store["Patient"].GetInstanceTableView()
            .Where(row => row.Id.Contains("UI-PAT", StringComparison.OrdinalIgnoreCase))
            .Where(row => row.Name.Contains("avery", StringComparison.OrdinalIgnoreCase))
            .Where(row => row.Identifiers.Contains($"WIRE-{suffix}", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.Equal("ui-patient", Assert.Single(filteredPatients).Id);
        InstanceTableRec[] filteredProfiles = store["StructureDefinition"].GetInstanceTableView()
            .Where(row => row.Url.Contains("/UI-PROFILE", StringComparison.OrdinalIgnoreCase))
            .Where(row => row.Description.Contains("TABLE PROFILE", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.Equal("ui-profile", Assert.Single(filteredProfiles).Id);
        Assert.DoesNotContain(store["Patient"].GetInstanceTableView(),
            row => row.Name.Contains("not-present", StringComparison.OrdinalIgnoreCase));

        string changedPatient = PatientFixture(version, format, "ui-patient", updated: true);
        string changedObservation = ObservationFixture(version, format, "Patient/ui-patient", "ui-observation", updated: true);
        string changedProfile = ProfileContent(version, format, updated: true);
        GeneratedResourceExpectation patientV2 = new("patient-v2", "Patient", "2", "ui-patient", PreviousBinding: patientV1.Name);
        GeneratedResourceExpectation observationV2 = new("observation-v2", "Observation", "2", "ui-observation", PreviousBinding: observationV1.Name);
        GeneratedResourceExpectation profileV2 = new("profile-v2", "StructureDefinition", "2", "ui-profile", PreviousBinding: profileV1.Name);
        await replay.StepAsync(ResourceStep(tenant, format, "PUT", "Patient/ui-patient", changedPatient, patientV2, HttpStatusCode.OK));
        await replay.StepAsync(ResourceStep(tenant, format, "PUT", "Observation/ui-observation", changedObservation, observationV2, HttpStatusCode.OK));
        await replay.StepAsync(ResourceStep(tenant, format, "PUT", "StructureDefinition/ui-profile", changedProfile, profileV2, HttpStatusCode.OK));
        AssertTableCounts(store, 1, 1, 1);
        AssertTableRow(store["Patient"], patientRow with
        {
            Name = "Revised, Avery Quinn",
            Identifiers = $"https://example.test/identifier|updated-{suffix}, https://example.test/secondary|second-{suffix}",
        });
        AssertTableRow(store["Observation"], observationRow with
        {
            Identifiers = $"https://example.test/identifier|updated-quantity-{suffix}",
        });
        AssertTableRow(store["StructureDefinition"], profileRow with
        {
            Name = "UiPatientProfileUpdated",
            Url = "https://example.test/fhir/StructureDefinition/ui-profile-updated",
            Description = "Updated synthetic table profile.",
            Identifiers = "https://example.test/ui-profiles|ui-profile-updated",
        });
        AssertSiblingTables();
        Assert.DoesNotContain(store["Patient"].GetInstanceTableView(),
            row => row.Name.Contains("Wire", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(store["StructureDefinition"].GetInstanceTableView(),
            row => row.Url == "https://example.test/fhir/StructureDefinition/ui-profile");

        await replay.StepAsync(ResourceStep(tenant, format, "DELETE", "Patient/ui-patient", changedPatient,
            patientV2 with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK));
        AssertTableCounts(store, 0, 1, 1);
        await replay.StepAsync(ResourceStep(tenant, format, "DELETE", "Observation/ui-observation", changedObservation,
            observationV2 with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK));
        AssertTableCounts(store, 0, 0, 1);
        await replay.StepAsync(ResourceStep(tenant, format, "DELETE", "StructureDefinition/ui-profile", changedProfile,
            profileV2 with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK));
        AssertTableCounts(store, 0, 0, 0);
        AssertSiblingTables();

        void AssertSiblingTables()
        {
            AssertTableCounts(siblingStore, 1, 1, 1);
            AssertTableRow(siblingStore["Patient"], siblingPatientRow);
            AssertTableRow(siblingStore["Observation"], observationRow);
            AssertTableRow(siblingStore["StructureDefinition"], profileRow);
        }
    }

    /// <summary>The viewer's exact pretty metadata/read paths preserve both authored formats and current state.</summary>
    [Theory]
    [MemberData(nameof(ViewerCases))]
    public async Task ResourceViewerReadsMetadataAndBothFormats(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityFormat inputFormat)
    {
        Assert.NotEmpty(label);
        string suffix = version.ToString().ToLowerInvariant();
        CompatibilityTenant tenant = Tenant(version, $"ui-viewer-{suffix}");
        CompatibilityTenant sibling = Tenant(version, $"ui-viewer-sibling-{suffix}");
        GeneratedResourceExpectation patientV1 = new("patient-v1", "Patient", "1");
        CompatibilityScenario scenario = new(label, Evidence, [tenant, sibling],
        [
            ResourceStep(tenant, inputFormat, "POST", "Patient", PatientFixture(version, inputFormat),
                patientV1, HttpStatusCode.Created),
        ]);
        await using CompatibilityReplay replay = new(scenario, CompatibilityTransport.Direct);
        await replay.RunAsync();
        IFhirStore store = replay.GetOwnedStore(tenant.Name);
        IFhirStore siblingStore = replay.GetOwnedStore(sibling.Name);
        GeneratedResourceBinding patient = replay.Bindings.Resources[patientV1.Name];
        GeneratedResourceExpectation observationV1 = new("observation-v1", "Observation", "1");
        await replay.StepAsync(ResourceStep(tenant, inputFormat, "POST", "Observation",
            ObservationFixture(version, inputFormat, patient.Location), observationV1, HttpStatusCode.Created));
        GeneratedResourceBinding observation = replay.Bindings.Resources[observationV1.Name];
        GeneratedResourceExpectation siblingV1 = new("sibling-patient-v1", "Patient", "1", patient.Id);
        await replay.StepAsync(ResourceStep(sibling, inputFormat, "PUT", $"Patient/{patient.Id}",
            PatientFixture(version, inputFormat, patient.Id, sibling: true), siblingV1, HttpStatusCode.Created));
        Assert.NotEqual(patient.Location, replay.Bindings.Resources[siblingV1.Name].Location);

        for (int index = 0; index < _viewerFormats.Length; index++)
        {
            CompatibilityFormat format = _viewerFormats[index];
            AssertViewerMetadata(replay, store, version, format, $"metadata-{index}", capture: index == 0);
            AssertViewerRead(replay, store, format, $"original-patient-{index}",
                PatientFixture(version, format), patientV1);
            AssertViewerRead(replay, store, format, $"original-observation-{index}",
                ObservationFixture(version, format, patient.Location), observationV1);
            AssertViewerRead(replay, siblingStore, format, $"original-sibling-{index}",
                PatientFixture(version, format, sibling: true), siblingV1);
            AssertViewerMissing(replay, siblingStore, format, $"sibling-missing-observation-{index}", "Observation", observation.Id);
        }

        GeneratedResourceExpectation patientV2 = new("patient-v2", "Patient", "2", patient.Id, PreviousBinding: patientV1.Name);
        GeneratedResourceExpectation observationV2 = new("observation-v2", "Observation", "2", observation.Id, PreviousBinding: observationV1.Name);
        await replay.StepAsync(ResourceStep(tenant, inputFormat, "PUT", $"Patient/{patient.Id}",
            PatientFixture(version, inputFormat, patient.Id, updated: true), patientV2, HttpStatusCode.OK));
        await replay.StepAsync(ResourceStep(tenant, inputFormat, "PUT", $"Observation/{observation.Id}",
            ObservationFixture(version, inputFormat, patient.Location, observation.Id, updated: true), observationV2, HttpStatusCode.OK));
        for (int index = 0; index < _viewerFormats.Length; index++)
        {
            CompatibilityFormat format = _viewerFormats[index];
            AssertViewerRead(replay, store, format, $"updated-patient-{index}",
                PatientFixture(version, format, updated: true), patientV2);
            AssertViewerRead(replay, store, format, $"updated-observation-{index}",
                ObservationFixture(version, format, patient.Location, updated: true), observationV2);
            AssertViewerRead(replay, siblingStore, format, $"unchanged-sibling-{index}",
                PatientFixture(version, format, sibling: true), siblingV1);
        }

        await replay.StepAsync(ResourceStep(tenant, inputFormat, "DELETE", $"Patient/{patient.Id}",
            PatientFixture(version, inputFormat, updated: true),
            patientV2 with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK));
        await replay.StepAsync(ResourceStep(tenant, inputFormat, "DELETE", $"Observation/{observation.Id}",
            ObservationFixture(version, inputFormat, patient.Location, updated: true),
            observationV2 with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK));
        for (int index = 0; index < _viewerFormats.Length; index++)
        {
            CompatibilityFormat format = _viewerFormats[index];
            AssertViewerMissing(replay, store, format, $"deleted-patient-{index}", "Patient", patient.Id);
            AssertViewerMissing(replay, store, format, $"deleted-observation-{index}", "Observation", observation.Id);
            AssertViewerRead(replay, siblingStore, format, $"surviving-sibling-{index}",
                PatientFixture(version, format, sibling: true), siblingV1);
        }
        AssertTableCounts(store, 0, 0, 0);
        AssertTableCounts(siblingStore, 1, 0, 0);
    }

    /// <summary>Both independent scenarios retain non-empty, labeled coverage of every product version and input format.</summary>
    [Fact]
    public void UiTheoryDataIsNonEmptyAndLabeled()
    {
        foreach (object[][] rows in new[] { TableCases.ToArray(), ViewerCases.ToArray() })
        {
            Assert.NotEmpty(rows);
            Assert.Equal(6, rows.Length);
            Assert.Equal(6, rows.Select(row => (string)row[0]).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(_versions, rows.Select(row => (FhirReleases.FhirSequenceCodes)row[1]).Distinct().ToArray());
            foreach (FhirReleases.FhirSequenceCodes version in _versions)
            {
                Assert.Equal(new[] { CompatibilityFormat.Json, CompatibilityFormat.Xml },
                    rows.Where(row => (FhirReleases.FhirSequenceCodes)row[1] == version)
                        .Select(row => (CompatibilityFormat)row[2]).ToArray());
            }
            Assert.All(rows, row =>
            {
                Assert.Contains(row[1].ToString()!, (string)row[0], StringComparison.Ordinal);
                Assert.Contains(row[2].ToString()!, (string)row[0], StringComparison.Ordinal);
            });
        }
        Assert.Equal(new[] { CompatibilityFormat.Json, CompatibilityFormat.Xml, CompatibilityFormat.Json }, _viewerFormats);
    }

    private static IEnumerable<object[]> UiRows(string name)
    {
        foreach (FhirReleases.FhirSequenceCodes version in _versions)
        {
            foreach (CompatibilityFormat format in new[] { CompatibilityFormat.Json, CompatibilityFormat.Xml })
            {
                yield return [$"ui-{name} / {version} / {format}", version, format];
            }
        }
    }

    private static CompatibilityTenant Tenant(FhirReleases.FhirSequenceCodes version, string name) =>
        new(name, version, $"http://localhost/fhir/{name}");

    private static string FhirVersion(FhirReleases.FhirSequenceCodes version) => version switch
    {
        FhirReleases.FhirSequenceCodes.R4 => "4.0.1",
        FhirReleases.FhirSequenceCodes.R4B => "4.3.0",
        FhirReleases.FhirSequenceCodes.R5 => "5.0.0",
        _ => throw new ArgumentOutOfRangeException(nameof(version)),
    };

    private static CompatibilityStep ResourceStep(
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        string method,
        string path,
        string content,
        GeneratedResourceExpectation generated,
        HttpStatusCode status)
    {
        string name = $"{tenant.Name}-{method}-{generated.Name}";
        bool deleted = method == "DELETE";
        return new CompatibilityStep(name, new CompatibilityRequest(tenant.Name, method, path)
        {
            Format = format,
            Content = deleted ? null : content,
            IfMatchBinding = method == "PUT" ? generated.PreviousBinding : null,
        }, ResourceResponse(name, content, generated, status, resourceHeaders: !deleted));
    }

    private static CompatibilityResponseExpectation ResourceResponse(
        string name,
        string content,
        GeneratedResourceExpectation generated,
        HttpStatusCode status,
        bool resourceHeaders = true)
    {
        ResourceBodyExpectation body = new(content, generated);
        return new CompatibilityResponseExpectation(status, true, body, body,
            new OutcomeBodyExpectation(name + "-outcome", [new("information", "success")]))
        {
            DirectResourceBinding = generated.Name,
            CheckDirectResourceHeaders = resourceHeaders,
            DirectHeaders = resourceHeaders ? [] : [new("ETag", null), new("Last-Modified", null), new("Location", null)],
        };
    }

    private static void AssertTableCounts(IFhirStore store, int patients, int observations, int profiles)
    {
        (string Name, int Count)[] expected = [("Observation", observations), ("Patient", patients), ("StructureDefinition", profiles)];
        foreach ((string name, int count) in expected)
        {
            KeyValuePair<string, IResourceStore> entry = Assert.Single(store, entry => entry.Key == name);
            Assert.Equal(count, entry.Value.Count);
            Assert.Equal(count, entry.Value.GetInstanceTableView().Count());
        }

        // FhirStorePage projects these per-type counts; other initialized conformance stores are not assumed empty.
        string[] names = expected.Select(entry => entry.Name).ToArray();
        Assert.Equal(names, store.Where(entry => names.Contains(entry.Key, StringComparer.Ordinal))
            .Select(entry => entry.Key).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.Equal(expected.Where(entry => entry.Count > 0).Select(entry => entry.Name).ToArray(),
            store.Where(entry => entry.Value.Count > 0 && names.Contains(entry.Key, StringComparer.Ordinal))
                .Select(entry => entry.Key).OrderBy(name => name, StringComparer.Ordinal).ToArray());
    }

    private static void AssertTableRow(IResourceStore store, InstanceTableRec expected) =>
        Assert.Equal(expected, Assert.Single(store.GetInstanceTableView()));

    private static bool ReadForViewer(
        IFhirStore store,
        string resourceType,
        string id,
        CompatibilityFormat format,
        out FhirResponseContext response)
    {
        bool metadata = resourceType == "CapabilityStatement" && id == "metadata";
        string url = metadata ? $"{store.Config.BaseUrl}/metadata" : $"{store.Config.BaseUrl}/{resourceType}/{id}";
        // The fourth constructor argument is SourceObject, just as in LoadResourceContent; it is not authorization.
        FhirRequestContext context = new(store, "GET", url, new Uri(store.Config.BaseUrl).Authority)
        {
            Authorization = null,
            DestinationFormat = WireContractAssertions.MediaType(format),
            SerializePretty = true,
        };
        Assert.Equal(store.Config.ControllerName, context.TenantName);
        Assert.Same(store, context.Store);
        Assert.Null(context.Authorization);
        if (!metadata)
        {
            Assert.Equal(resourceType, context.ResourceType);
            Assert.Equal(id, context.Id);
        }
        return metadata ? store.GetMetadata(context, out response) : store.InstanceRead(context, out response);
    }

    private static void AssertViewerRead(
        CompatibilityReplay replay,
        IFhirStore store,
        CompatibilityFormat format,
        string name,
        string content,
        GeneratedResourceExpectation generated)
    {
        GeneratedResourceBinding binding = replay.Bindings.Resources[generated.Name];
        bool success = ReadForViewer(store, generated.ResourceType, binding.Id, format, out FhirResponseContext response);
        Assert.NotNull(response.Resource);
        Assert.NotNull(response.Outcome);
        Assert.NotSame(response.Resource, response.Outcome);
        WireContractAssertions.Response(
            ResourceResponse(name, content, generated with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK),
            DirectResponse(success, response), format, store.Config.BaseUrl, replay.Bindings);
        AssertPretty(response.SerializedResource, format);
    }

    private static void AssertViewerMetadata(
        CompatibilityReplay replay,
        IFhirStore store,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityFormat format,
        string name,
        bool capture)
    {
        bool success = ReadForViewer(store, "CapabilityStatement", "metadata", format, out FhirResponseContext response);
        Assert.NotNull(response.Resource);
        Assert.NotNull(response.Outcome);
        Assert.NotSame(response.Resource, response.Outcome);
        string dateBinding = capture ? "metadata-date" : name + "-date";
        MetadataBodyExpectation body = new(FhirVersion(version),
            new GeneratedResourceExpectation("metadata", "CapabilityStatement", null, "metadata",
                capture ? CompatibilityBindingMode.Capture : CompatibilityBindingMode.Reuse), dateBinding);
        CompatibilityResponseExpectation expected = new(HttpStatusCode.OK, true, body, body,
            new OutcomeBodyExpectation(name + "-outcome", [new("information", "success", "Retrieved current CapabilityStatement")]))
        {
            DirectResourceBinding = "metadata",
        };
        WireContractAssertions.Response(expected, DirectResponse(success, response), format, store.Config.BaseUrl, replay.Bindings);
        Assert.Equal(replay.Bindings.Values["metadata-date"].Value, replay.Bindings.Values[dateBinding].Value);
        AssertPretty(response.SerializedResource, format);
    }

    private static void AssertViewerMissing(
        CompatibilityReplay replay,
        IFhirStore store,
        CompatibilityFormat format,
        string name,
        string resourceType,
        string id)
    {
        bool success = ReadForViewer(store, resourceType, id, format, out FhirResponseContext response);
        Assert.Null(response.Resource);
        Assert.NotNull(response.Outcome);
        Assert.Empty(response.Id);
        Assert.Empty(response.ResourceType);
        OutcomeBodyExpectation outcome = new(name + "-outcome", [new("error", "exception", $"Resource: {resourceType}/{id} not found")]);
        CompatibilityResponseExpectation expected = new(HttpStatusCode.NotFound, false, outcome, new EmptyBodyExpectation(), outcome)
        {
            DirectHeaders = [new("ETag", null), new("Last-Modified", null), new("Location", null)],
        };
        WireContractAssertions.Response(expected, DirectResponse(success, response), format, store.Config.BaseUrl, replay.Bindings);
        // The current viewer displays SerializedResource, not SerializedOutcome. This is not a browser/error-banner assertion.
        Assert.Empty(response.SerializedResource);
        AssertPretty(response.SerializedOutcome, format);
    }

    private static CompatibilityResponse DirectResponse(bool success, FhirResponseContext response) =>
        new(CompatibilityTransport.Direct, response.StatusCode ?? throw new InvalidOperationException("Missing direct status."), response.MimeType)
        {
            Direct = new CompatibilityDirectResponse(success, response.SerializedResource, response.SerializedOutcome,
                response.ResourceType, response.Id, response.ETag, response.LastModified, response.Location),
        };

    private static void AssertPretty(string content, CompatibilityFormat format)
    {
        if (format == CompatibilityFormat.Json)
        {
            Assert.Matches("\\r?\\n[ \\t]+\"resourceType\"\\s*:", content);
            return;
        }

        // Only root-level FHIR indentation counts; the required XHTML narrative newline is content.
        XElement root = XElement.Parse(content, LoadOptions.PreserveWhitespace);
        Assert.Contains(root.Nodes().OfType<XText>(),
            text => string.IsNullOrWhiteSpace(text.Value) && text.Value.Contains('\n'));
    }

    private static string PatientFixture(
        FhirReleases.FhirSequenceCodes version,
        CompatibilityFormat format,
        string? id = null,
        bool updated = false,
        bool sibling = false)
    {
        string content = CompatibilityReplay.LoadFixture(version, format == CompatibilityFormat.Json ? "patient.json" : "patient.xml");
        if (updated || sibling)
        {
            string language = sibling ? "de" : "en-US";
            string family = sibling ? "Sibling" : "Revised";
            string suffix = version.ToString().ToLowerInvariant();
            content = ReplaceOnce(content, format == CompatibilityFormat.Json ? "\"language\": \"en\"" : "<language value=\"en\"/>",
                format == CompatibilityFormat.Json ? $"\"language\": \"{language}\"" : $"<language value=\"{language}\"/>");
            content = ReplaceOnce(content, format == CompatibilityFormat.Json ? "\"family\": \"Wire\"" : "<family value=\"Wire\"/>",
                format == CompatibilityFormat.Json ? $"\"family\": \"{family}\"" : $"<family value=\"{family}\"/>");
            content = ReplaceOnce(content, $"wire-{suffix}", $"{(sibling ? "sibling" : "updated")}-{suffix}");
        }
        return id is null ? content : WithId(content, format, id);
    }

    private static string ObservationFixture(
        FhirReleases.FhirSequenceCodes version,
        CompatibilityFormat format,
        string reference,
        string? id = null,
        bool updated = false)
    {
        string content = CompatibilityReplay.LoadFixture(version, format == CompatibilityFormat.Json ? "observation.json" : "observation.xml");
        content = ReplaceOnce(content, "Patient/wire-patient", reference);
        if (updated)
        {
            string suffix = version.ToString().ToLowerInvariant();
            content = ReplaceOnce(content, $"quantity-{suffix}", $"updated-quantity-{suffix}");
            content = ReplaceOnce(content, "1234567890.123400", "1234567890.123450");
            content = ReplaceOnce(content, "2024-06-03T10:11:12.3400-05:00", "2024-06-04T10:11:12.3400-05:00");
        }
        return id is null ? content : WithId(content, format, id);
    }

    private static string ProfileContent(FhirReleases.FhirSequenceCodes version, CompatibilityFormat format, bool updated = false)
    {
        // Both formats are authored here; neither is produced by converting an actual store response.
        string content = format == CompatibilityFormat.Json
            ? $$$"""
                {"resourceType":"StructureDefinition","id":"ui-profile",
                 "url":"https://example.test/fhir/StructureDefinition/ui-profile",
                 "identifier":[{"system":"https://example.test/ui-profiles","value":"ui-profile"}],
                 "name":"UiPatientProfile","title":"UI profile (not the Name column)","status":"draft",
                 "description":"Synthetic table profile.","fhirVersion":"{{{FhirVersion(version)}}}",
                 "kind":"resource","abstract":false,"type":"Patient",
                 "baseDefinition":"http://hl7.org/fhir/StructureDefinition/Patient","derivation":"constraint",
                 "differential":{"element":[{"id":"Patient","path":"Patient"}]}}
                """
            : $"""
                <StructureDefinition xmlns="http://hl7.org/fhir"><id value="ui-profile"/>
                  <url value="https://example.test/fhir/StructureDefinition/ui-profile"/>
                  <identifier><system value="https://example.test/ui-profiles"/><value value="ui-profile"/></identifier>
                  <name value="UiPatientProfile"/><title value="UI profile (not the Name column)"/><status value="draft"/>
                  <description value="Synthetic table profile."/><fhirVersion value="{FhirVersion(version)}"/>
                  <kind value="resource"/><abstract value="false"/><type value="Patient"/>
                  <baseDefinition value="http://hl7.org/fhir/StructureDefinition/Patient"/><derivation value="constraint"/>
                  <differential><element id="Patient"><path value="Patient"/></element></differential>
                </StructureDefinition>
                """;
        if (updated)
        {
            content = ReplaceOnce(content, "https://example.test/fhir/StructureDefinition/ui-profile",
                "https://example.test/fhir/StructureDefinition/ui-profile-updated");
            content = ReplaceOnce(content, "UiPatientProfile", "UiPatientProfileUpdated");
            content = ReplaceOnce(content, "Synthetic table profile.", "Updated synthetic table profile.");
            content = ReplaceOnce(content, format == CompatibilityFormat.Json ? "\"value\":\"ui-profile\"" : "<value value=\"ui-profile\"/>",
                format == CompatibilityFormat.Json ? "\"value\":\"ui-profile-updated\"" : "<value value=\"ui-profile-updated\"/>");
        }
        return content;
    }

    private static string ReplaceOnce(string content, string original, string replacement)
    {
        int position = content.IndexOf(original, StringComparison.Ordinal);
        Assert.True(position >= 0, $"The authored input must contain {original}.");
        Assert.Equal(-1, content.IndexOf(original, position + original.Length, StringComparison.Ordinal));
        return content.Replace(original, replacement, StringComparison.Ordinal);
    }

    private static string WithId(string content, CompatibilityFormat format, string id)
    {
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(content);
            Assert.False(document.RootElement.TryGetProperty("id", out _));
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

        XElement root = XElement.Parse(content, LoadOptions.PreserveWhitespace);
        Assert.Null(root.Element(_fhir + "id"));
        root.AddFirst(new XElement(_fhir + "id", new XAttribute("value", id)));
        return root.ToString(SaveOptions.DisableFormatting);
    }
}

// <copyright file="Sdk6ContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using fhir.candle.Services;
using fhir.candle.Tests.Controllers;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace fhir.candle.Tests;

/// <summary>SDK-independent consumer contracts replayed through fresh HTTP and direct-store boundaries.</summary>
[Collection(CompatibilityHostCollection.Name)]
public partial class Sdk6ContractTests
{
    private static readonly FhirReleases.FhirSequenceCodes[] _versions =
    [
        FhirReleases.FhirSequenceCodes.R4,
        FhirReleases.FhirSequenceCodes.R4B,
        FhirReleases.FhirSequenceCodes.R5,
    ];

    /// <summary>Labeled, non-empty inputs reusable by later domain-specific partial classes.</summary>
    public static IEnumerable<object[]> ReplayCases
    {
        get
        {
            foreach (FhirReleases.FhirSequenceCodes version in _versions)
            {
                foreach (CompatibilityTransport transport in Enum.GetValues<CompatibilityTransport>())
                {
                    foreach (CompatibilityFormat format in Enum.GetValues<CompatibilityFormat>())
                    {
                        yield return [$"foundation / {version} / {transport} / {format}", version, transport, format];
                    }
                }
            }
        }
    }

    /// <summary>The mixed case hosts all six tenants together, once per boundary and format.</summary>
    public static IEnumerable<object[]> MixedTenantCases
    {
        get
        {
            foreach (CompatibilityTransport transport in Enum.GetValues<CompatibilityTransport>())
            {
                foreach (CompatibilityFormat format in Enum.GetValues<CompatibilityFormat>())
                {
                    yield return [$"mixed R4+R4B+R5 / {transport} / {format}", transport, format];
                }
            }
        }
    }

    /// <summary>Every supported version exercises raw bodies, generated identities, references, and headers.</summary>
    [Theory]
    [MemberData(nameof(ReplayCases))]
    public async Task ReplaySmoke(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        CompatibilityTenant tenant = Tenant(version, $"smoke-{VersionName(version)}");
        string patient = PatientContent(format, null, "en");
        GeneratedResourceExpectation created = new("patient-v1", "Patient", "1");
        CompatibilityScenario scenario = new(
            $"smoke-{version}-{format}",
            "FhirStoreTests.cs: MetadataJson/MetadataXml, TestPostPutIdSemantics.PostWithoutClientIdAssignsServerId, TestPatientCRUD.PatientCRUD; docs/user/strict-mode.md",
            [tenant],
            [
                MetadataStep(tenant, format),
                new("create-patient", new CompatibilityRequest(tenant.Name, "POST", "Patient") { Format = format, Content = patient },
                    ResourceResponse("create-patient", patient, created, HttpStatusCode.Created)),
                BoundRead("read-patient", tenant, format, patient, created),
            ]);

        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();

        GeneratedResourceBinding identity = replay.Bindings.Resources["patient-v1"];
        string observation = ObservationContent(format, "linked-observation", identity.Location);
        GeneratedResourceExpectation linked = new("observation-v1", "Observation", "1", "linked-observation");
        await replay.StepAsync(new CompatibilityStep(
            "create-linked-observation",
            new CompatibilityRequest(tenant.Name, "PUT", "Observation/linked-observation") { Format = format, Content = observation },
            ResourceResponse("create-linked-observation", observation, linked, HttpStatusCode.Created)));

        string updatedPatient = PatientContent(format, identity.Id, "en-US");
        GeneratedResourceExpectation updated = new("patient-v2", "Patient", "2", identity.Id, PreviousBinding: "patient-v1");
        await replay.StepAsync(new CompatibilityStep(
            "update-generated-patient",
            new CompatibilityRequest(tenant.Name, "PUT", string.Empty)
            {
                Format = format,
                Content = updatedPatient,
                TargetBinding = "patient-v1",
                IfMatchBinding = "patient-v1",
            },
            ResourceResponse("update-generated-patient", updatedPatient, updated, HttpStatusCode.OK)));
        await replay.StepAsync(BoundRead("read-updated-patient", tenant, format, updatedPatient, updated));
        await replay.StepAsync(BoundRead("read-linked-observation", tenant, format, observation, linked));

        Assert.Equal(tenant.BaseUrl, replay.Bindings.BaseUrls[tenant.Name]);
        Assert.Equal(identity.Id, replay.Bindings.Resources["patient-v2"].Id);
        Assert.Equal("2", replay.Bindings.Resources["patient-v2"].Version);
        Assert.All(replay.Responses, response => Assert.Equal(transport, response.Value.Transport));
    }

    /// <summary>Named same-version siblings and all three versions coexist without content or URL leakage.</summary>
    [Theory]
    [MemberData(nameof(MixedTenantCases))]
    public async Task MixedTenantMetadataAndIsolation(string label, CompatibilityTransport transport, CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        List<CompatibilityTenant> tenants = [];
        List<CompatibilitySeed> seeds = [];
        List<CompatibilityStep> steps = [];
        foreach (FhirReleases.FhirSequenceCodes version in _versions)
        {
            CompatibilityTenant oak = Tenant(version, $"oak-{VersionName(version)}");
            CompatibilityTenant elm = Tenant(version, $"elm-{VersionName(version)}");
            tenants.AddRange([oak, elm]);
            string oakContent = PatientContent(format, "shared", "en", "Oak");
            string elmContent = PatientContent(format, "shared", "de", "Elm");
            string changedOakContent = PatientContent(format, "shared", "en-US", "Oak");
            GeneratedResourceExpectation oakV1 = new($"{oak.Name}-v1", "Patient", "1", "shared");
            GeneratedResourceExpectation elmV1 = new($"{elm.Name}-v1", "Patient", "1", "shared");
            GeneratedResourceExpectation oakV2 = new($"{oak.Name}-v2", "Patient", "2", "shared", PreviousBinding: oakV1.Name);
            seeds.Add(PatientSeed(oak, format, oakContent, oakV1));
            seeds.Add(PatientSeed(elm, format, elmContent, elmV1));
            steps.Add(MetadataStep(oak, format));
            steps.Add(MetadataStep(elm, format));
            steps.Add(BoundRead($"{oak.Name}-read-initial", oak, format, oakContent, oakV1));
            steps.Add(BoundRead($"{elm.Name}-read-initial", elm, format, elmContent, elmV1));
            steps.Add(new CompatibilityStep(
                $"{oak.Name}-update",
                new CompatibilityRequest(oak.Name, "PUT", "Patient/shared")
                {
                    Format = format,
                    Content = changedOakContent,
                    IfMatchBinding = oakV1.Name,
                },
                ResourceResponse($"{oak.Name}-update", changedOakContent, oakV2, HttpStatusCode.OK)));
            steps.Add(BoundRead($"{oak.Name}-read-updated", oak, format, changedOakContent, oakV2));
            steps.Add(BoundRead($"{elm.Name}-unchanged-after-update", elm, format, elmContent, elmV1));
            steps.Add(new CompatibilityStep(
                $"{oak.Name}-delete",
                new CompatibilityRequest(oak.Name, "DELETE", "Patient/shared") { Format = format },
                ResourceResponse($"{oak.Name}-delete", changedOakContent, oakV2 with { Mode = CompatibilityBindingMode.Reuse },
                    HttpStatusCode.OK, resourceHeaders: false)));
            steps.Add(MissingPatient($"{oak.Name}-missing", oak, format, "shared"));
            steps.Add(BoundRead($"{elm.Name}-unchanged-after-delete", elm, format, elmContent, elmV1));
        }

        CompatibilityScenario scenario = new(
            $"mixed-tenants-{format}",
            "docs/user/tenants.md; docs/user/strict-mode.md capability URL/version contract; FhirStoreTests.cs: TestPatientCRUD and PutOnMissingLenientCreatesWith201",
            tenants,
            steps)
        {
            Seeds = seeds,
        };
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();

        Assert.Equal(6, replay.Bindings.BaseUrls.Count);
        foreach (FhirReleases.FhirSequenceCodes version in _versions)
        {
            string oak = $"oak-{VersionName(version)}";
            string elm = $"elm-{VersionName(version)}";
            Assert.Equal("2", replay.Bindings.Resources[$"{oak}-v2"].Version);
            Assert.Equal("1", replay.Bindings.Resources[$"{elm}-v1"].Version);
            Assert.NotEqual(replay.Bindings.Resources[$"{oak}-v1"].Location, replay.Bindings.Resources[$"{elm}-v1"].Location);
        }

        // Two actual empty searches per tenant ran before any raw seed was submitted.
        Assert.Equal(12, replay.Responses.Count(response => response.Key.StartsWith("initial-empty/", StringComparison.Ordinal)));
    }

    /// <summary>Reusing a scenario definition must not reuse stores, resources, bindings, or temporary directories.</summary>
    [Theory]
    [MemberData(nameof(ReplayCases))]
    public async Task ReplayUsesFreshScenarioState(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format)
    {
        Assert.NotEmpty(label);
        CompatibilityTenant tenant = Tenant(version, $"fresh-{VersionName(version)}");
        string original = PatientContent(format, "seed", "en");
        string changed = PatientContent(format, "seed", "en-US");
        string generated = PatientContent(format, null, "de");
        GeneratedResourceExpectation seeded = new("seed-v1", "Patient", "1", "seed");
        GeneratedResourceExpectation updated = new("seed-v2", "Patient", "2", "seed", PreviousBinding: "seed-v1");
        GeneratedResourceExpectation created = new("generated-v1", "Patient", "1");
        CompatibilityScenario scenario = new(
            $"fresh-state-{version}-{format}",
            "FhirStoreTests.cs: TestPatientCRUD, PostWithoutClientIdAssignsServerId, PutOnMissingLenientCreatesWith201; docs/user/loading-data.md",
            [tenant],
            [
                BoundRead("read-original-seed", tenant, format, original, seeded),
                new("mutate-seed", new CompatibilityRequest(tenant.Name, "PUT", "Patient/seed") { Format = format, Content = changed },
                    ResourceResponse("mutate-seed", changed, updated, HttpStatusCode.OK)),
                new("leave-created-resource", new CompatibilityRequest(tenant.Name, "POST", "Patient") { Format = format, Content = generated },
                    ResourceResponse("leave-created-resource", generated, created, HttpStatusCode.Created)),
                BoundRead("read-created-resource", tenant, format, generated, created),
            ])
        {
            Seeds = [PatientSeed(tenant, format, original, seeded)],
        };

        List<CompatibilityBindings> states = [];
        List<string> generatedIds = [];
        List<string> directories = [];
        for (int iteration = 0; iteration < 2; iteration++)
        {
            CompatibilityReplay replay = new(scenario, transport);
            await using (replay)
            {
                Assert.Empty(replay.Bindings.Resources);
                Assert.Empty(replay.Bindings.Values);
                states.Add(replay.Bindings);
                if (replay.OwnedDirectory is string directory)
                {
                    Assert.True(Directory.Exists(directory));
                    directories.Add(directory);
                }

                await replay.RunAsync();
                Assert.Equal("2", replay.Bindings.Resources["seed-v2"].Version);
                generatedIds.Add(replay.Bindings.Resources["generated-v1"].Id);
                Assert.Equal(original, scenario.Seeds[0].Content);
                await Assert.ThrowsAsync<InvalidOperationException>(() => replay.RunAsync());
            }

            if (replay.OwnedDirectory is string disposedDirectory)
            {
                Assert.False(Directory.Exists(disposedDirectory), "Owned directories must be removed after host disposal.");
            }

            await Assert.ThrowsAsync<ObjectDisposedException>(() => replay.SendAsync(new CompatibilityRequest(tenant.Name, "GET", "Patient")));
        }

        Assert.NotSame(states[0], states[1]);
        Assert.NotSame(states[0].Resources, states[1].Resources);
        Assert.NotSame(states[0].Values, states[1].Values);
        Assert.NotSame(states[0].BaseUrls, states[1].BaseUrls);
        Assert.NotEqual(generatedIds[0], generatedIds[1]);
        Assert.Equal(transport == CompatibilityTransport.Http ? 2 : 0, directories.Count);
        Assert.Equal(directories.Count, directories.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>The parameterless fixture retains r4 while isolating both data discovery and package state.</summary>
    [Fact]
    public async Task FactoryDefaultsAreIsolatedAndKeepR4Identity()
    {
        CandleWebApplicationFactory factory = new();
        string ownedDirectory = factory.OwnedDirectory;
        await using (factory)
        {
            CandleConfig config = factory.Services.GetRequiredService<CandleConfig>();
            IFhirStoreManager manager = factory.Services.GetRequiredService<IFhirStoreManager>();
            Assert.Equal("r4", Assert.Single(manager).Key);
            Assert.Equal("http://localhost/fhir/r4", manager["r4"].Config.BaseUrl);
            Assert.False(config.UseOfficialRegistries);
            Assert.Empty(config.AdditionalFhirRegistryUrls);
            Assert.Empty(config.AdditionalNpmRegistryUrls);
            Assert.Empty(config.PublishedPackages);
            Assert.Empty(config.CiPackages);
            Assert.Null(config.ReferenceImplementation);
            Assert.NotNull(config.SourceDirectory);
            Assert.NotNull(config.FhirCacheDirectory);
            Assert.StartsWith(ownedDirectory + Path.DirectorySeparatorChar, config.SourceDirectory, StringComparison.Ordinal);
            Assert.StartsWith(ownedDirectory + Path.DirectorySeparatorChar, config.FhirCacheDirectory, StringComparison.Ordinal);
            Assert.True(Directory.Exists(config.SourceDirectory));
            Assert.True(Directory.Exists(config.FhirCacheDirectory));
            Assert.Empty(Directory.EnumerateFileSystemEntries(config.SourceDirectory));
            Assert.NotEqual(config.SourceDirectory, config.FhirCacheDirectory);
        }

        Assert.False(Directory.Exists(ownedDirectory));
        await factory.DisposeAsync();
    }

    /// <summary>Explicit loading inputs are caller-owned; tenant policy resolution cannot mutate caller data.</summary>
    [Fact]
    public async Task FactoryDisposesOnlyOwnedDirectories()
    {
        string callerDirectory = Path.Combine(Path.GetTempPath(), $"fhir-candle-controlled-{Guid.NewGuid():N}");
        string source = Directory.CreateDirectory(Path.Combine(callerDirectory, "source")).FullName;
        string cache = Directory.CreateDirectory(Path.Combine(callerDirectory, "cache")).FullName;
        try
        {
            TenantConfiguration tenant = new()
            {
                FhirVersion = FhirReleases.FhirSequenceCodes.R4B,
                ControllerName = "controlled-r4b",
                BaseUrl = "http://localhost/fhir/controlled-r4b",
                Strict = true,
            };
            CandleConfig configuration = new()
            {
                SourceDirectory = source,
                FhirCacheDirectory = cache,
                UseOfficialRegistries = false,
            };
            CandleWebApplicationFactory factory = new([tenant], configuration);
            string ownedDirectory = factory.OwnedDirectory;
            await using (factory)
            {
                IFhirStoreManager manager = factory.Services.GetRequiredService<IFhirStoreManager>();
                Assert.Equal("controlled-r4b", Assert.Single(manager).Key);
                Assert.NotSame(tenant, manager[tenant.ControllerName].Config);
                Assert.False(manager[tenant.ControllerName].Config.AllowExistingId);
                Assert.True(tenant.AllowExistingId);
                Assert.NotSame(configuration, factory.Services.GetRequiredService<CandleConfig>());
            }

            Assert.False(Directory.Exists(ownedDirectory));
            Assert.True(Directory.Exists(source));
            Assert.True(Directory.Exists(cache));
        }
        finally
        {
            Directory.Delete(callerDirectory, recursive: true);
        }
    }

    /// <summary>A theory cannot silently become empty or lose a version/transport/format label.</summary>
    [Fact]
    public void CommonTheoryDataIsNonEmptyAndLabeled()
    {
        object[][] rows = ReplayCases.ToArray();
        Assert.Equal(12, rows.Length);
        Assert.Equal(12, rows.Select(row => (string)row[0]).Distinct(StringComparer.Ordinal).Count());
        Assert.All(rows, row =>
        {
            Assert.Contains(row[1].ToString()!, (string)row[0], StringComparison.Ordinal);
            Assert.Contains(row[2].ToString()!, (string)row[0], StringComparison.Ordinal);
            Assert.Contains(row[3].ToString()!, (string)row[0], StringComparison.Ordinal);
        });
        Assert.Equal(4, MixedTenantCases.Count());
    }

    private static CompatibilityTenant Tenant(FhirReleases.FhirSequenceCodes version, string name) =>
        new(name, version, $"http://localhost/fhir/{name}");

    private static string VersionName(FhirReleases.FhirSequenceCodes version) => version.ToString().ToLowerInvariant();

    private static string ExpectedFhirVersion(FhirReleases.FhirSequenceCodes version) => version switch
    {
        FhirReleases.FhirSequenceCodes.R4 => "4.0.1",
        FhirReleases.FhirSequenceCodes.R4B => "4.3.0",
        FhirReleases.FhirSequenceCodes.R5 => "5.0.0",
        _ => throw new ArgumentOutOfRangeException(nameof(version)),
    };

    private static CompatibilityStep MetadataStep(CompatibilityTenant tenant, CompatibilityFormat format)
    {
        string name = $"{tenant.Name}-metadata";
        MetadataBodyExpectation body = new(
            ExpectedFhirVersion(tenant.Version),
            new GeneratedResourceExpectation(name, "CapabilityStatement", null, "metadata"),
            name + "-date");
        return new CompatibilityStep(name, new CompatibilityRequest(tenant.Name, "GET", "metadata") { Format = format },
            new CompatibilityResponseExpectation(HttpStatusCode.OK, true, body, body, SuccessOutcome(name))
            {
                HttpResourceHeadersBinding = name,
                DirectResourceBinding = name,
            });
    }

    private static CompatibilitySeed PatientSeed(
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        string content,
        GeneratedResourceExpectation generated) =>
        new($"seed-{generated.Name}", tenant.Name, "Patient", generated.ExpectedId!, format, content,
            ResourceResponse($"seed-{generated.Name}", content, generated, HttpStatusCode.Created));

    private static CompatibilityStep BoundRead(
        string id,
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        string content,
        GeneratedResourceExpectation generated) =>
        new(id, new CompatibilityRequest(tenant.Name, "GET", string.Empty) { Format = format, TargetBinding = generated.Name },
            ResourceResponse(id, content, generated with { Mode = CompatibilityBindingMode.Reuse }, HttpStatusCode.OK));

    private static CompatibilityResponseExpectation ResourceResponse(
        string stepId,
        string content,
        GeneratedResourceExpectation generated,
        HttpStatusCode status,
        bool resourceHeaders = true)
    {
        ResourceBodyExpectation body = new(content, generated);
        CompatibilityHeaderExpectation[] absentHeaders = [new("ETag", null), new("Last-Modified", null), new("Location", null)];
        return new CompatibilityResponseExpectation(status, true, body, body, SuccessOutcome(stepId))
        {
            HttpResourceHeadersBinding = resourceHeaders ? generated.Name : null,
            DirectResourceBinding = generated.Name,
            CheckDirectResourceHeaders = resourceHeaders,
            HttpHeaders = resourceHeaders ? [] : absentHeaders,
            DirectHeaders = resourceHeaders ? [] : absentHeaders,
        };
    }

    private static OutcomeBodyExpectation SuccessOutcome(string stepId) =>
        new(stepId + "-outcome", [new("information", "success")]);

    private static CompatibilityStep MissingPatient(string id, CompatibilityTenant tenant, CompatibilityFormat format, string resourceId)
    {
        OutcomeBodyExpectation outcome = new(id + "-outcome", [new("error", "exception", $"Resource: Patient/{resourceId} not found")]);
        CompatibilityHeaderExpectation[] absentHeaders = [new("ETag", null), new("Last-Modified", null), new("Location", null)];
        return new CompatibilityStep(id, new CompatibilityRequest(tenant.Name, "GET", $"Patient/{resourceId}") { Format = format },
            new CompatibilityResponseExpectation(HttpStatusCode.NotFound, false, outcome, new EmptyBodyExpectation(), outcome)
            {
                HttpHeaders = absentHeaders,
                DirectHeaders = absentHeaders,
            });
    }

    private static string PatientContent(CompatibilityFormat format, string? id, string language, string? family = null)
    {
        if (format == CompatibilityFormat.Json)
        {
            string identity = id is null ? string.Empty : $"\"id\":\"{id}\",";
            string name = family is null ? string.Empty : $$$""","name":[{"family":"{{{family}}}"}]""";
            return $$$"""{"resourceType":"Patient",{{{identity}}}"language":"{{{language}}}"{{{name}}}}""";
        }

        string xmlId = id is null ? string.Empty : $"""<id value="{id}"/>""";
        string xmlName = family is null ? string.Empty : $"""<name><family value="{family}"/></name>""";
        return $"""<Patient xmlns="http://hl7.org/fhir">{xmlId}<language value="{language}"/>{xmlName}</Patient>""";
    }

    private static string ObservationContent(CompatibilityFormat format, string id, string reference) => format == CompatibilityFormat.Json
        ? $$$"""{"resourceType":"Observation","id":"{{{id}}}","status":"final","code":{"text":"Synthetic replay observation"},"subject":{"reference":"{{{reference}}}"}}"""
        : $"""<Observation xmlns="http://hl7.org/fhir"><id value="{id}"/><status value="final"/><code><text value="Synthetic replay observation"/></code><subject><reference value="{reference}"/></subject></Observation>""";
}

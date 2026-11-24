// <copyright file="Sdk6ContractTests.Operations.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using fhir.candle.Tests.Controllers;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;

namespace fhir.candle.Tests;

public partial class Sdk6ContractTests
{
    private const string ValidationEvidence =
        "TestValidateOperation (H1/H2/M1/L4); ValidateRoutingTests; docs/user/operations/validate.md; " +
        "OpValidate target precedence and CodedValidationException mapping. Synthetic targets, not SDK snapshots.";

    private const string DiscoveryEvidence =
        "docs/technical/operations-and-hooks.md; IFhirOperation declarations and GetDefinition; " +
        "VersionedFhirStore.CheckLoadedOperations/DiscoverInteractionHooks/LoadPackage; R4 CDexTaskProcess.";

    private const string ValidateGetDiagnostic = "$validate only supports POST; GET is not allowed.";

    private const string ValidateMethodEvidence =
        "docs/user/operations/validate.md; approved validate-only-405 repair of a pre-existing defect, not an SDK6 regression; " +
        "existing dispatcher guards and FhirController authorization/Prefer boundaries.";

    private static readonly string[] _validationKinds =
    [
        "valid", "valid-wrapped", "missing-required", "wrapped-missing-required",
        "primitive", "wrapped-primitive", "narrative", "wrapped-narrative",
        "ignored-valid", "ignored-invalid", "no-body", "no-target", "malformed",
    ];

    /// <summary>Each target/route/version/format/boundary has its own executable row.</summary>
    public static IEnumerable<object[]> OperationValidationCases
    {
        get
        {
            foreach (object[] row in OperationRows("validate", _validationKinds))
            {
                foreach (string route in new[] { "system", "type", "instance" })
                {
                    yield return [$"{row[0]} / {route}", row[1], row[2], row[3], row[4], route];
                }
            }
        }
    }

    /// <summary>Store observation must use the same store as the declared request boundary.</summary>
    public static IEnumerable<object[]> OperationBoundaryCases => OperationRows("owned-store", ["identity"]);

    /// <summary>Package absence and the real R4-only PAS operation gate are separate scenarios.</summary>
    public static IEnumerable<object[]> OperationDiscoveryCases => OperationRows("operation-discovery", ["absent", "pas-package"]);

    /// <summary>The CDex interaction hook has separate eligibility, package, and version controls.</summary>
    public static IEnumerable<object[]> InteractionHookCases => OperationRows("cdex-hook",
        ["package-absent", "eligible", "eligible-post", "wrong-status", "wrong-intent", "missing-query"]);

    /// <summary>Supported operation levels and negative boundaries cannot mask one another.</summary>
    public static IEnumerable<object[]> OperationRouteCases => OperationRows("operation-route",
        ["convert-system", "convert-type", "convert-instance", "status-system", "status-type-get", "status-type-post",
         "status-wrong-type", "events-system", "events-type", "events-missing-instance",
         "hook-system-invalid", "hook-type", "hook-instance", "unknown", "validate-missing-instance", "unsupported-verb",
         "validate-get-not-allowed"]);

    /// <summary>All levels reject GET; raw and non-FHIR object inputs exercise separate direct-only branches.</summary>
    public static IEnumerable<object[]> ValidateGetCases
    {
        get
        {
            foreach (object[] row in OperationRows("validate-method", ["no-body", "raw-body", "malformed-body", "non-fhir-object"]))
            {
                if ((CompatibilityTransport)row[2] == CompatibilityTransport.Http && (string)row[4] != "no-body")
                {
                    continue;
                }
                foreach (string level in new[] { "system", "type", "instance" })
                {
                    yield return [$"{row[0]} / {level}", row[1], row[2], row[3], row[4], level];
                }
            }
        }
    }

    /// <summary>Earlier guards retain boundary-specific representations, including HTTP-only tenant/auth checks.</summary>
    public static IEnumerable<object[]> ValidateMethodGuardCases => OperationRows("validate-earlier-guard",
        ["missing-instance", "missing-instance-with-body", "missing-instance-post-body",
         "parameters-type", "parameters-instance", "unknown-type", "unknown-type-instance",
         "unknown-operation-system", "unknown-operation-type", "unknown-operation-instance",
         "missing-tenant-system", "missing-tenant-type", "missing-tenant-instance",
         "unauthorized-system", "unauthorized-type", "unauthorized-instance"])
        .Where(row => (CompatibilityTransport)row[2] == CompatibilityTransport.Http ||
            (!((string)row[4]).StartsWith("missing-tenant-", StringComparison.Ordinal) &&
             !((string)row[4]).StartsWith("unauthorized-", StringComparison.Ordinal)));

    /// <summary>Each HTTP preference retains its own body contract and an exact Allow header at every level.</summary>
    public static IEnumerable<object[]> ValidateGetPreferenceCases
    {
        get
        {
            foreach (object[] row in OperationRows("validate-get-prefer", ["default", "representation", "OperationOutcome", "minimal"])
                .Where(row => (CompatibilityTransport)row[2] == CompatibilityTransport.Http))
            {
                foreach (string level in new[] { "system", "type", "instance" })
                {
                    yield return [$"{row[0]} / {level}", row[1], row[2], row[3], row[4], level];
                }
            }
        }
    }

    /// <summary>System and type-level controls independently reject an accidental generic method policy.</summary>
    public static IEnumerable<object[]> OtherOperationVerbCases => OperationRows("other-operation-verb",
        ["subscription-hook-get", "status-get", "status-post", "pas-submit-get", "pas-inquire-get"])
        .Where(row => !((string)row[4]).StartsWith("pas-", StringComparison.Ordinal) ||
            (FhirReleases.FhirSequenceCodes)row[1] == FhirReleases.FhirSequenceCodes.R4);

    /// <summary>R4 package contents and the executable package-ID gate are independent authorities.</summary>
    public static IEnumerable<object[]> PasDefinitionAuthorityCases => OperationRows("pas-definition-authority",
        ["manifest-only", "with-definitions"])
        .Where(row => (FhirReleases.FhirSequenceCodes)row[1] == FhirReleases.FhirSequenceCodes.R4);

    /// <summary>Manifest-only registration refreshes metadata without changing a sibling or a warmed no-op cache.</summary>
    public static IEnumerable<object[]> OperationCapabilityRefreshCases => OperationRows("operation-capability-refresh", ["manifest-only"]);

    /// <summary>Invalid parseable targets reach the structural validator, not the ordinary parser's rejection.</summary>
    [Theory]
    [MemberData(nameof(OperationValidationCases))]
    public async Task ValidatePreservesTargetAndIssueContract(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind,
        string route)
    {
        Assert.NotEmpty(label);
        CompatibilityTenant tenant = Tenant(version, $"validate-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, ValidationEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();

        string focus = PatientContent(format, "focus", "en", "Stored focus is unchanged");
        GeneratedResourceExpectation focusBinding = new("focus-v1", "Patient", "1", "focus");
        if (route == "instance")
        {
            await replay.StepAsync(new("seed-focus",
                new(tenant.Name, "PUT", "Patient/focus") { Format = format, Content = focus },
                ResourceResponse("seed-focus", focus, focusBinding, HttpStatusCode.Created)));
        }

        bool missingCode = kind.Contains("missing-required", StringComparison.Ordinal) || kind == "ignored-invalid";
        string resourceType = missingCode ? "Observation" : "Patient";
        string path = route switch
        {
            "system" => "$validate",
            "type" => $"{resourceType}/$validate",
            "instance" => "Patient/focus/$validate",
            _ => throw new ArgumentOutOfRangeException(nameof(route)),
        };
        string? content = ValidationContent(version, format, kind);
        CompatibilityResponse response = await replay.SendAsync(new(tenant.Name, "POST", path)
        {
            Format = format,
            Content = content,
        });
        string context = $"{label}: POST {tenant.BaseUrl}/{path}\nRequest: {content ?? "(no body)"}\n" +
            $"Status: {(int)response.Status}\nResource: {ExchangeResource(response)}\nOutcome: {response.Direct?.SerializedOutcome}";

        if (kind == "malformed")
        {
            Assert.True(response.Status == HttpStatusCode.UnsupportedMediaType, context);
            AssertOperationOutcome(replay, tenant, format, "malformed", response,
                HttpStatusCode.UnsupportedMediaType, false, false, [new("error", "invalid")]);
            string outcome = OperationOutcomeBody(response);
            Assert.Contains("does not consume non-FHIR content.", outcome, StringComparison.Ordinal);
            Assert.DoesNotContain("All OK", outcome, StringComparison.Ordinal);
        }
        else if ((kind is "no-body" or "no-target") && route != "instance")
        {
            Assert.True(response.Status == HttpStatusCode.UnprocessableEntity, context);
            AssertOperationOutcome(replay, tenant, format, "no-target", response,
                HttpStatusCode.UnprocessableEntity, false, true,
                [new("error", "invalid", "$validate requires a target resource: either an instance focus (URL), a 'resource' parameter inside a Parameters body, or a resource as the request body.")]);
        }
        else
        {
            Assert.True(response.Status == HttpStatusCode.OK, context);
            bool primitive = kind.Contains("primitive", StringComparison.Ordinal);
            bool narrative = kind.Contains("narrative", StringComparison.Ordinal);
            List<OutcomeIssueExpectation> issues = missingCode
                ? [new("error", "required")]
                : primitive || narrative
                    ? [new("error", "value")]
                    : [new("information", "informational", "All OK")];
            if (kind.StartsWith("ignored-", StringComparison.Ordinal))
            {
                issues.Add(new("information", "informational", "Parameter 'mode' is currently ignored by this $validate implementation."));
                issues.Add(new("information", "informational", "Parameter 'profile' is currently ignored by this $validate implementation."));
            }
            AssertOperationOutcome(replay, tenant, format, "validation", response, HttpStatusCode.OK, true, true, issues);
            if (missingCode || primitive || narrative)
            {
                string issue = ExchangeRepeated(ExchangeResource(response), format, "issue")[0];
                string element = missingCode ? "code" : primitive ? "active" : "div";
                string[] expressions = OperationPrimitiveArray(issue, format, "expression");
                Assert.NotEmpty(expressions);
                Assert.Contains(expressions, expression => expression.Contains(element, StringComparison.Ordinal));
                Assert.DoesNotContain(expressions, expression => expression.Contains("Parameters", StringComparison.Ordinal));
                Assert.DoesNotContain("All OK", ExchangeResource(response), StringComparison.Ordinal);
            }
        }

        // Validation is stateless, including body-over-focus and invalid-body paths.
        if (route == "instance")
        {
            await replay.StepAsync(BoundRead("focus-survives", tenant, format, focus, focusBinding));
        }
        else
        {
            foreach (string type in new[] { "Patient", "Observation" })
            {
                CompatibilityResponse search = await replay.SendAsync(new(tenant.Name, "GET", type) { Format = format });
                AssertExchangeContainer(replay, tenant, format, $"after-validation-{type}", search, "Bundle", string.Empty);
                Assert.Equal(0, SearchTotal(ExchangeResource(search), format));
                Assert.False(ExchangeHasMember(ExchangeResource(search), format, "entry"));
            }
        }
    }

    /// <summary>The accessor validates ownership/lifetime and observes HTTP mutations, not replacement stores.</summary>
    [Theory]
    [MemberData(nameof(OperationBoundaryCases))]
    public async Task OperationStoreAccessorPreservesOwnership(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        Assert.Equal("identity", kind);
        CompatibilityTenant oak = Tenant(version, $"operation-oak-{VersionName(version)}");
        CompatibilityTenant elm = Tenant(version, $"operation-elm-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, ValidationEvidence, [oak, elm], [MetadataStep(oak, format)]);
        CompatibilityReplay replay = new(scenario, transport);
        await using (replay)
        {
            await replay.RunAsync();
            IFhirStore oakStore = replay.GetOwnedStore(oak.Name);
            IFhirStore elmStore = replay.GetOwnedStore(elm.Name);
            Assert.Same(oakStore, replay.GetOwnedStore(oak.Name));
            Assert.NotSame(oakStore, elmStore);
            Assert.Throws<ArgumentException>(() => replay.GetOwnedStore("unknown"));
            Assert.Throws<ArgumentException>(() => replay.GetOwnedStore(oak.Name.ToUpperInvariant()));
            string patient = PatientContent(format, "owned", "en");
            await replay.StepAsync(new("owned-write", new(oak.Name, "PUT", "Patient/owned") { Format = format, Content = patient },
                ResourceResponse("owned-write", patient, new("owned-v1", "Patient", "1", "owned"), HttpStatusCode.Created)));
            Assert.True(oakStore["Patient"].ContainsKey("owned"));
            Assert.False(elmStore["Patient"].ContainsKey("owned"));
        }
        Assert.Throws<ObjectDisposedException>(() => replay.GetOwnedStore(oak.Name));
        Assert.Throws<ObjectDisposedException>(() => replay.GetOwnedStore("unknown"));
    }

    /// <summary>Definitions and capability advertisements retain native version and package identities.</summary>
    [Theory]
    [MemberData(nameof(OperationDiscoveryCases))]
    public async Task OperationDiscoveryIsVersionAndPackageCorrect(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        CompatibilityTenant tenant = Tenant(version, $"discovery-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, DiscoveryEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        IFhirStore store = replay.GetOwnedStore(tenant.Name);
        Assert.Empty(store.LoadedPackageIds);

        using ExpandedOperationPackage package = new("hl7.fhir.us.davinci-pas", version);
        if (kind == "pas-package")
        {
            package.Load(store);
            Assert.Contains(package.Directive, store.LoadedPackageDirectives);
            Assert.Contains("hl7.fhir.us.davinci-pas", store.LoadedPackageIds);
        }
        bool pas = kind == "pas-package" && version == FhirReleases.FhirSequenceCodes.R4;
        OperationContract[] expected = OperationContracts(version, pas);
        CompatibilityResponse search = await replay.SendAsync(new(tenant.Name, "GET", "OperationDefinition") { Format = format });
        AssertExchangeContainer(replay, tenant, format, "definitions", search, "Bundle", string.Empty);
        string[] definitions = ExchangeRepeated(ExchangeResource(search), format, "entry")
            .Select(entry => ExchangeNestedResource(entry, format, "resource")).ToArray();
        Assert.Equal(expected.Select(op => op.Name[1..]).Order(StringComparer.Ordinal),
            definitions.Select(definition => ExchangePrimitive(definition, format, "code")).Order(StringComparer.Ordinal));
        foreach (OperationContract op in expected)
        {
            string definition = Assert.Single(definitions, definition => ExchangePrimitive(definition, format, "code") == op.Name[1..]);
            Assert.Equal(op.Name, ExchangePrimitive(definition, format, "name"));
            Assert.Equal(op.Canonical, ExchangePrimitive(definition, format, "url"));
            Assert.Equal("operation", ExchangePrimitive(definition, format, "kind"));
            Assert.Equal("draft", ExchangePrimitive(definition, format, "status"));
            Assert.Equal(op.System, OperationBoolean(definition, format, "system"));
            Assert.Equal(op.Type, OperationBoolean(definition, format, "type"));
            Assert.Equal(op.Instance, OperationBoolean(definition, format, "instance"));
            Assert.Equal(op.AffectsState, OperationBoolean(definition, format, "affectsState"));
            Assert.Equal(op.Resources, OperationPrimitiveArray(definition, format, "resource"));
            string id = op.SuppliedId ?? op.Name[1..] + "-" + op.Version.Replace('.', '-');
            GeneratedResourceExpectation binding = new("definition-" + id, "OperationDefinition", "1", id);
            WireContractAssertions.BindResource(binding, definition, format, tenant.BaseUrl, replay.Bindings);
            if (op.SuppliedId is not null)
            {
                WireContractAssertions.Body(new ResourceBodyExpectation(PasDefinitionContent(format, op.Name),
                    binding with { Mode = CompatibilityBindingMode.Reuse }), definition, format, tenant.BaseUrl, replay.Bindings);
            }
            CompatibilityResponse read = await replay.SendAsync(new(tenant.Name, "GET", "OperationDefinition/" + id) { Format = format });
            WireContractAssertions.Status(HttpStatusCode.OK, read.Status);
            WireContractAssertions.Content(definition, ExchangeResource(read), format);

            Type implementation = Assert.Single(store.GetType().Assembly.GetTypes(), type =>
                type.GetInterfaces().Any(contract => contract.FullName == "FhirCandle.Operations.IFhirOperation") &&
                ReadOperationProperty<string>(Activator.CreateInstance(type)!, "OperationName") == op.Name);
            Assert.StartsWith("Op", implementation.Name, StringComparison.Ordinal);
            object operation = Activator.CreateInstance(implementation)!;
            Assert.Equal(op.Version, ReadOperationProperty<string>(operation, "OperationVersion"));
            Assert.Equal(op.Get, ReadOperationProperty<bool>(operation, "AllowGet"));
            Assert.True(ReadOperationProperty<bool>(operation, "AllowPost"));
            Assert.Equal(op.Package, ReadOperationProperty<string>(operation, "RequiresPackage"));
            if (op.Name == "$validate")
            {
                Assert.Equal(new[] { "resource", "mode", "profile", "return" },
                    ExchangeRepeated(definition, format, "parameter").Select(parameter => ExchangePrimitive(parameter, format, "name")));
            }
        }

        foreach (string operation in new[] { "submit", "inquire" })
        {
            CompatibilityResponse response = await replay.SendAsync(new(tenant.Name, "POST", $"Claim/${operation}") { Format = format });
            AssertOperationOutcome(replay, tenant, format, "pas-route-" + operation, response,
                pas ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.NotFound, false, false,
                pas
                    ? [new("fatal", "structure", operation == "submit"
                        ? "PAS Claim Submit requires a PASRequestBundle as input."
                        : "PAS Claim Inquiry requires a PASClaimInquiryBundle as input.")]
                    : [new("error", "not-found", $"Operation ${operation} does not have an executable implementation on this server.")]);
        }

        CompatibilityResponse metadata = await replay.SendAsync(new(tenant.Name, "GET", "metadata") { Format = format });
        WireContractAssertions.Status(HttpStatusCode.OK, metadata.Status);
        string rest = Assert.Single(ExchangeRepeated(ExchangeResource(metadata), format, "rest"));
        AssertOperationAdvertisements(expected.Where(op => op.System), rest, format);
        foreach (string resourceType in new[] { "Patient", "Subscription", "Claim" })
        {
            string resource = Assert.Single(ExchangeRepeated(rest, format, "resource"),
                resource => ExchangePrimitive(resource, format, "type") == resourceType);
            AssertOperationAdvertisements(expected.Where(op => (op.Type || op.Instance) &&
                (op.Resources.Length == 0 || op.Resources.Contains(resourceType, StringComparer.Ordinal))), resource, format);
        }
    }

    /// <summary>New executable registrations invalidate cached metadata, while duplicate and ineligible registrations do not.</summary>
    [Theory]
    [MemberData(nameof(OperationCapabilityRefreshCases))]
    public async Task PackageOperationRegistrationRefreshesCachedMetadata(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        Assert.Equal("manifest-only", kind);
        CompatibilityTenant target = Tenant(version, $"capability-target-{VersionName(version)}");
        CompatibilityTenant sibling = Tenant(version, $"capability-sibling-{VersionName(version)}");
        CompatibilityStep targetMetadata = MetadataStep(target, format);
        CompatibilityStep siblingMetadata = MetadataStep(sibling, format);
        CompatibilityScenario scenario = new(label, DiscoveryEvidence, [target, sibling], [targetMetadata, siblingMetadata]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();

        OperationContract[] builtIns = OperationContracts(version, false);
        string targetBefore = AssertOperationCapabilities(target, format, builtIns,
            Assert.Single(replay.Responses, response => response.Key == targetMetadata.Id).Value);
        string siblingBefore = AssertOperationCapabilities(sibling, format, builtIns,
            Assert.Single(replay.Responses, response => response.Key == siblingMetadata.Id).Value);
        Assert.Empty(replay.GetOwnedStore(target.Name).LoadedPackageIds);
        Assert.Empty(replay.GetOwnedStore(sibling.Name).LoadedPackageIds);

        using ExpandedOperationPackage package = new("hl7.fhir.us.davinci-pas", version, includePasDefinitions: false);
        package.Load(replay.GetOwnedStore(target.Name));
        package.Load(replay.GetOwnedStore(target.Name));
        Assert.Contains(package.Directive, replay.GetOwnedStore(target.Name).LoadedPackageDirectives);
        Assert.Contains("hl7.fhir.us.davinci-pas", replay.GetOwnedStore(target.Name).LoadedPackageIds);

        OperationContract[] expected = OperationContracts(version, version == FhirReleases.FhirSequenceCodes.R4);
        string refreshed = AssertOperationCapabilities(target, format, expected,
            await replay.SendAsync(new(target.Name, "GET", "metadata") { Format = format }));
        if (version != FhirReleases.FhirSequenceCodes.R4)
        {
            WireContractAssertions.Content(targetBefore, refreshed, format);
        }
        string siblingAfter = AssertOperationCapabilities(sibling, format, builtIns,
            await replay.SendAsync(new(sibling.Name, "GET", "metadata") { Format = format }));
        WireContractAssertions.Content(siblingBefore, siblingAfter, format);

        // The first regenerated response need not carry the persisted metadata of the next cached read.
        string warmed = AssertOperationCapabilities(target, format, expected,
            await replay.SendAsync(new(target.Name, "GET", "metadata") { Format = format }));
        string repeated = AssertOperationCapabilities(target, format, expected,
            await replay.SendAsync(new(target.Name, "GET", "metadata") { Format = format }));
        WireContractAssertions.Content(warmed, repeated, format);

        package.Load(replay.GetOwnedStore(target.Name));
        string afterDuplicate = AssertOperationCapabilities(target, format, expected,
            await replay.SendAsync(new(target.Name, "GET", "metadata") { Format = format }));
        WireContractAssertions.Content(warmed, afterDuplicate, format);
        string siblingAfterDuplicate = AssertOperationCapabilities(sibling, format, builtIns,
            await replay.SendAsync(new(sibling.Name, "GET", "metadata") { Format = format }));
        WireContractAssertions.Content(siblingBefore, siblingAfterDuplicate, format);
        Assert.Empty(replay.GetOwnedStore(sibling.Name).LoadedPackageIds);

        foreach (CompatibilityTenant tenant in new[] { target, sibling })
        {
            CompatibilityResponse search = await replay.SendAsync(new(tenant.Name, "GET", "OperationDefinition") { Format = format });
            AssertExchangeContainer(replay, tenant, format, tenant.Name + "-capability-definitions", search, "Bundle", string.Empty);
            string[] definitions = ExchangeRepeated(ExchangeResource(search), format, "entry")
                .Select(entry => ExchangeNestedResource(entry, format, "resource")).ToArray();
            Assert.Equal(builtIns.Select(op => op.Name[1..]).Order(StringComparer.Ordinal),
                definitions.Select(definition => ExchangePrimitive(definition, format, "code")).Order(StringComparer.Ordinal));
            foreach (OperationContract op in OperationContracts(version, true).Where(op => op.SuppliedId is not null))
            {
                Assert.DoesNotContain(definitions, definition => ExchangePrimitive(definition, format, "url") == op.Canonical);
                CompatibilityResponse missing = await replay.SendAsync(new(tenant.Name, "GET", "OperationDefinition/" + op.SuppliedId)
                {
                    Format = format,
                });
                AssertOperationOutcome(replay, tenant, format, tenant.Name + "-absent-" + op.SuppliedId, missing,
                    HttpStatusCode.NotFound, false, false,
                    [new("error", "exception", $"Resource: OperationDefinition/{op.SuppliedId} not found")]);
            }
        }
    }

    /// <summary>HTTP routes and PerformInteraction retain their own supported/unsupported boundaries.</summary>
    [Theory]
    [MemberData(nameof(OperationRouteCases))]
    public async Task OperationRoutesPreserveSupportedBoundaries(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        CompatibilityTenant tenant = Tenant(version, $"routes-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, DiscoveryEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        string patient = PatientContent(format, "route-focus", "en");
        await replay.StepAsync(new("route-focus", new(tenant.Name, "PUT", "Patient/route-focus") { Format = format, Content = patient },
            ResourceResponse("route-focus", patient, new("route-focus-v1", "Patient", "1", "route-focus"), HttpStatusCode.Created)));
        (string method, string path, HttpStatusCode status, string code) = kind switch
        {
            "convert-system" => ("POST", "$convert", HttpStatusCode.OK, string.Empty),
            "convert-type" => ("POST", "Patient/$convert", HttpStatusCode.UnprocessableEntity, "not-supported"),
            "convert-instance" => ("POST", "Patient/route-focus/$convert", HttpStatusCode.UnprocessableEntity, "not-supported"),
            "status-system" => ("GET", "$status", HttpStatusCode.UnprocessableEntity, "not-supported"),
            "status-type-get" => ("GET", "Subscription/$status", HttpStatusCode.OK, string.Empty),
            "status-type-post" => ("POST", "Subscription/$status", HttpStatusCode.OK, string.Empty),
            "status-wrong-type" => ("GET", "Patient/$status", HttpStatusCode.UnprocessableEntity, "not-supported"),
            "events-system" => ("GET", "$events", HttpStatusCode.UnprocessableEntity, "not-supported"),
            "events-type" => ("GET", "Subscription/$events", HttpStatusCode.UnprocessableEntity, "not-supported"),
            "events-missing-instance" => ("GET", "Subscription/missing/$events", HttpStatusCode.NotFound, "not-found"),
            "hook-system-invalid" => ("POST", "$subscription-hook", HttpStatusCode.UnprocessableEntity, "exception"),
            "hook-type" => ("POST", "Patient/$subscription-hook", HttpStatusCode.UnprocessableEntity, "not-supported"),
            "hook-instance" => ("POST", "Patient/route-focus/$subscription-hook", HttpStatusCode.UnprocessableEntity, "not-supported"),
            "unknown" => ("POST", "$not-an-operation", HttpStatusCode.NotFound, "not-found"),
            "validate-missing-instance" => ("POST", "Patient/missing/$validate", HttpStatusCode.NotFound, "not-found"),
            "validate-get-not-allowed" => ("GET", "Patient/route-focus/$validate", HttpStatusCode.MethodNotAllowed, string.Empty),
            "unsupported-verb" => ("PATCH", "Patient/route-focus/$validate",
                transport == CompatibilityTransport.Http ? HttpStatusCode.MethodNotAllowed : HttpStatusCode.NotImplemented, "not-supported"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        CompatibilityResponse response = await replay.SendAsync(new(tenant.Name, method, path)
        {
            Format = format,
            Content = kind == "convert-system" ? patient : null,
        });
        Assert.True(response.Status == status, $"{label}: {method} {path}, expected {status}, actual {response.Status}; {OperationOutcomeBody(response)}");
        if (kind == "validate-get-not-allowed")
        {
            AssertValidateGetRejection(replay, tenant, format, kind, response);
            await replay.StepAsync(BoundRead("route-focus-survives-get", tenant, format, patient,
                new("route-focus-v1", "Patient", "1", "route-focus")));
            return;
        }
        if (kind == "unsupported-verb")
        {
            if (transport == CompatibilityTransport.Http)
            {
                Assert.Empty(response.HttpBody!);
                Assert.Contains("GET", string.Join(",", response.HttpHeaders["Allow"]), StringComparison.Ordinal);
                Assert.Contains("POST", string.Join(",", response.HttpHeaders["Allow"]), StringComparison.Ordinal);
            }
            else
            {
                // Unsupported generic dispatch does not run the operation serialization wrapper.
                Assert.False(response.Direct!.Success);
                Assert.Empty(response.Direct.SerializedResource);
            }
            return;
        }
        if (status != HttpStatusCode.OK)
        {
            AssertOperationOutcome(replay, tenant, format, kind, response, status, false, false, [new("error", code)]);
        }
        else if (kind == "convert-system")
        {
            AssertExchangeContainer(replay, tenant, format, kind, response, "Patient", "route-focus",
                new(kind + "-outcome", [new("success", "success", "Successful conversion")]));
            WireContractAssertions.Content(patient, ExchangeResource(response), format);
        }
        else
        {
            AssertExchangeContainer(replay, tenant, format, kind, response, "Bundle", string.Empty);
            Assert.Equal("searchset", ExchangePrimitive(ExchangeResource(response), format, "type"));
            Assert.False(ExchangeHasMember(ExchangeResource(response), format, "entry"));
        }
    }

    /// <summary>The validate-only method guard precedes both parsing branches and leaves stored content untouched.</summary>
    [Theory]
    [MemberData(nameof(ValidateGetCases))]
    public async Task ValidateRejectsGetAtEveryOperationLevel(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind,
        string level)
    {
        CompatibilityTenant tenant = Tenant(version, $"validate-get-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, ValidateMethodEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        string focus = PatientContent(format, "focus", "en", "GET must not validate or mutate this focus");
        GeneratedResourceExpectation binding = new("get-focus-v1", "Patient", "1", "focus");
        await replay.StepAsync(new("get-focus", new(tenant.Name, "PUT", "Patient/focus") { Format = format, Content = focus },
            ResourceResponse("get-focus", focus, binding, HttpStatusCode.Created)));
        string path = ValidateOperationPath(level);
        CompatibilityResponse response;
        if (kind == "non-fhir-object")
        {
            Assert.Equal(CompatibilityTransport.Direct, transport);
            IFhirStore store = replay.GetOwnedStore(tenant.Name);
            FhirRequestContext context = new()
            {
                TenantName = tenant.Name,
                Store = store,
                HttpMethod = "GET",
                Url = tenant.BaseUrl + "/" + path,
                Authorization = null,
                Interaction = OperationInteraction(level),
                ResourceType = level == "system" ? string.Empty : "Patient",
                Id = level == "instance" ? "focus" : string.Empty,
                OperationName = "$validate",
                SourceObject = new object(),
                SourceFormat = WireContractAssertions.MediaType(format),
                DestinationFormat = WireContractAssertions.MediaType(format),
            };
            bool success = InvokeOperationWrapper(store, level, context, out FhirResponseContext direct);
            Assert.False(success);
            Assert.Null(direct.Resource);
            Assert.Null(direct.NonFhirData);
            Assert.NotNull(direct.Outcome);
            response = DirectOperationResponse(success, direct);
        }
        else
        {
            string? content = kind switch
            {
                "no-body" => null,
                "raw-body" => ValidationContent(version, format, "valid"),
                "malformed-body" => ValidationContent(version, format, "malformed"),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
            response = await replay.SendAsync(new(tenant.Name, "GET", path) { Format = format, Content = content });
        }
        AssertValidateGetRejection(replay, tenant, format, "get-rejected", response);
        await replay.StepAsync(BoundRead("focus-survives-rejected-get", tenant, format, focus, binding));
        Assert.False(replay.GetOwnedStore(tenant.Name)["Patient"].ContainsKey("validation-candidate"));
    }

    /// <summary>Not-found and authorization guards keep their existing order and their real boundary representations.</summary>
    [Theory]
    [MemberData(nameof(ValidateMethodGuardCases))]
    public async Task ValidateMethodRejectionPreservesEarlierGuards(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        Assert.NotEmpty(label);
        CompatibilityTenant tenant = Tenant(version, $"validate-guards-{VersionName(version)}");
        if (kind.StartsWith("missing-tenant-", StringComparison.Ordinal) || kind.StartsWith("unauthorized-", StringComparison.Ordinal))
        {
            Assert.Equal(CompatibilityTransport.Http, transport);
            await AssertValidateHttpEarlierGuard(tenant, format, kind);
            return;
        }

        CompatibilityScenario scenario = new(label, ValidateMethodEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        string focus = PatientContent(format, "focus", "en");
        GeneratedResourceExpectation binding = new("guard-focus-v1", "Patient", "1", "focus");
        await replay.StepAsync(new("guard-focus", new(tenant.Name, "PUT", "Patient/focus") { Format = format, Content = focus },
            ResourceResponse("guard-focus", focus, binding, HttpStatusCode.Created)));
        (string method, string path, bool unsupportedType) = kind switch
        {
            "missing-instance" or "missing-instance-with-body" => ("GET", "Patient/missing/$validate", false),
            "missing-instance-post-body" => ("POST", "Patient/missing/$validate", false),
            "parameters-type" => ("GET", "Parameters/$validate", true),
            "parameters-instance" => ("GET", "Parameters/focus/$validate", true),
            "unknown-type" => ("GET", "NotAResource/$validate", true),
            "unknown-type-instance" => ("GET", "NotAResource/focus/$validate", true),
            "unknown-operation-system" => ("GET", "$not-an-operation", false),
            "unknown-operation-type" => ("GET", "Patient/$not-an-operation", false),
            "unknown-operation-instance" => ("GET", "Patient/focus/$not-an-operation", false),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        string? content = kind is "missing-instance-with-body" or "missing-instance-post-body"
            ? ValidationContent(version, format, "valid")
            : kind.StartsWith("unknown-operation-", StringComparison.Ordinal)
                ? ValidationContent(version, format, "malformed")
                : null;
        CompatibilityResponse response = await replay.SendAsync(new(tenant.Name, method, path) { Format = format, Content = content });
        if (unsupportedType)
        {
            string[] parts = path.Split('/');
            string resourceType = parts[0];
            string level = parts.Length == 3 ? "instance" : "type";
            if (transport == CompatibilityTransport.Http)
            {
                WireContractAssertions.Status(HttpStatusCode.NotFound, response.Status);
                Assert.Equal("text/plain", response.MediaType);
                string handler = level == "instance" ? "GetInstanceOperation" : "GetTypeOperation";
                Assert.Equal($"{handler} <<< tenant {tenant.Name} does not support resource {resourceType}!", response.HttpBody);
            }
            else
            {
                // An unsupported wire type is not a generic-dispatch route. The explicit wrappers still own a 404 guard.
                WireContractAssertions.Status(HttpStatusCode.NotImplemented, response.Status);
                Assert.False(response.Direct!.Success);
                Assert.Empty(response.Direct.SerializedResource);
                Assert.Empty(response.Direct.SerializedOutcome);
                Assert.Empty(response.Direct.ResourceType);
                Assert.Empty(response.Direct.Id);
                Assert.Empty(response.Direct.ETag);
                Assert.Empty(response.Direct.LastModified);
                Assert.Empty(response.Direct.Location);
                IFhirStore store = replay.GetOwnedStore(tenant.Name);
                FhirRequestContext context = new()
                {
                    TenantName = tenant.Name,
                    Store = store,
                    HttpMethod = "GET",
                    Url = tenant.BaseUrl + "/" + path,
                    Authorization = null,
                    Interaction = OperationInteraction(level),
                    ResourceType = resourceType,
                    Id = level == "instance" ? "focus" : string.Empty,
                    OperationName = "$validate",
                    SourceObject = new object(),
                    SourceFormat = WireContractAssertions.MediaType(format),
                    DestinationFormat = WireContractAssertions.MediaType(format),
                };
                bool success = InvokeOperationWrapper(store, level, context, out FhirResponseContext direct);
                Assert.Null(direct.Resource);
                Assert.NotNull(direct.Outcome);
                AssertOperationOutcome(replay, tenant, format, "unsupported-type-wrapper", DirectOperationResponse(success, direct),
                    HttpStatusCode.NotFound, false, false,
                    [new("error", "not-supported", $"Resource type {resourceType} does not exist on this server.")]);
            }
        }
        else
        {
            string diagnostic = kind.StartsWith("unknown-operation-", StringComparison.Ordinal)
                ? "Operation $not-an-operation does not have an executable implementation on this server."
                : "Instance Patient/missing does not exist on this server.";
            AssertOperationOutcome(replay, tenant, format, kind, response, HttpStatusCode.NotFound, false, false,
                [new("error", "not-found", diagnostic)]);
        }
        Assert.False(response.HttpHeaders.ContainsKey("Allow"));
        await replay.StepAsync(BoundRead("focus-survives-earlier-guard", tenant, format, focus, binding));
    }

    /// <summary>HTTP error bodies honor ordinary Prefer handling without losing Allow or changing stored metadata.</summary>
    [Theory]
    [MemberData(nameof(ValidateGetPreferenceCases))]
    public async Task ValidateGetPreferencesPreserveState(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind,
        string level)
    {
        Assert.Equal(CompatibilityTransport.Http, transport);
        CompatibilityTenant tenant = Tenant(version, $"validate-prefer-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, ValidateMethodEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        string focus = PatientContent(format, "focus", "en", "Prefer must not change content or metadata");
        GeneratedResourceExpectation binding = new("prefer-focus-v1", "Patient", "1", "focus");
        await replay.StepAsync(new("prefer-focus", new(tenant.Name, "PUT", "Patient/focus") { Format = format, Content = focus },
            ResourceResponse("prefer-focus", focus, binding, HttpStatusCode.Created)));
        Dictionary<string, string> headers = [];
        if (kind != "default")
        {
            headers.Add("Prefer", "return=" + kind);
        }
        CompatibilityResponse response = await replay.SendAsync(new(tenant.Name, "GET", ValidateOperationPath(level))
        {
            Format = format,
            Headers = headers,
        });
        AssertValidateGetRejection(replay, tenant, format, "prefer-rejection", response, minimal: kind == "minimal");
        await replay.StepAsync(BoundRead("focus-survives-prefer", tenant, format, focus, binding));
    }

    /// <summary>Other operations retain their pre-existing GET/POST behavior, independently of discovery assertions.</summary>
    [Theory]
    [MemberData(nameof(OtherOperationVerbCases))]
    public async Task OtherOperationVerbBehaviorIsUnchanged(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        CompatibilityTenant tenant = Tenant(version, $"other-verbs-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, DiscoveryEvidence, [tenant], []);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.AssertNoClinicalResourcesAsync(format);
        IFhirStore store = replay.GetOwnedStore(tenant.Name);
        CompatibilityResponse response;
        if (kind.StartsWith("pas-", StringComparison.Ordinal))
        {
            Assert.Equal(FhirReleases.FhirSequenceCodes.R4, version);
            using ExpandedOperationPackage package = new("hl7.fhir.us.davinci-pas", version);
            package.Load(store);
            Assert.Contains(package.Directive, store.LoadedPackageDirectives);
            string operation = kind == "pas-submit-get" ? "submit" : "inquire";
            response = await replay.SendAsync(new(tenant.Name, "GET", $"Claim/${operation}") { Format = format });
            AssertOperationOutcome(replay, tenant, format, kind, response, HttpStatusCode.UnprocessableEntity, false, false,
                [new("fatal", "structure", operation == "submit"
                    ? "PAS Claim Submit requires a PASRequestBundle as input."
                    : "PAS Claim Inquiry requires a PASClaimInquiryBundle as input.")]);
        }
        else if (kind == "subscription-hook-get")
        {
            Assert.Empty(store.ReceivedNotifications);
            response = await replay.SendAsync(new(tenant.Name, "GET", "$subscription-hook") { Format = format });
            AssertOperationOutcome(replay, tenant, format, kind, response, HttpStatusCode.UnprocessableEntity, false, false,
                [new("error", "exception", "Posted content is not a valid Subscription notification bundle")]);
            Assert.Empty(store.ReceivedNotifications);
            CompatibilityResponse bundles = await replay.SendAsync(new(tenant.Name, "GET", "Bundle") { Format = format });
            AssertExchangeContainer(replay, tenant, format, "no-notification-bundle", bundles, "Bundle", string.Empty);
            Assert.Equal(0, SearchTotal(ExchangeResource(bundles), format));
            Assert.False(ExchangeHasMember(ExchangeResource(bundles), format, "entry"));
        }
        else
        {
            Assert.Contains(kind, new[] { "status-get", "status-post" });
            response = await replay.SendAsync(new(tenant.Name, kind == "status-get" ? "GET" : "POST", "Subscription/$status")
            {
                Format = format,
            });
            AssertExchangeContainer(replay, tenant, format, kind, response, "Bundle", string.Empty);
            Assert.Equal("searchset", ExchangePrimitive(ExchangeResource(response), format, "type"));
            Assert.False(ExchangeHasMember(ExchangeResource(response), format, "entry"));
        }
        Assert.False(response.HttpHeaders.ContainsKey("Allow"));
    }

    /// <summary>PAS execution needs the package gate, while literal definitions come only from package resources.</summary>
    [Theory]
    [MemberData(nameof(PasDefinitionAuthorityCases))]
    public async Task PasDefinitionsComeFromPackageContent(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        Assert.Equal(FhirReleases.FhirSequenceCodes.R4, version);
        CompatibilityTenant tenant = Tenant(version, $"pas-authority-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, DiscoveryEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        IFhirStore store = replay.GetOwnedStore(tenant.Name);
        Assert.Empty(store.LoadedPackageIds);
        bool includesDefinitions = kind == "with-definitions";
        using ExpandedOperationPackage package = new("hl7.fhir.us.davinci-pas", version, includePasDefinitions: includesDefinitions);
        package.Load(store);
        Assert.Equal("hl7.fhir.us.davinci-pas#0.0.0-sdk6-test", package.Directive);
        Assert.Contains(package.Directive, store.LoadedPackageDirectives);
        Assert.Contains("hl7.fhir.us.davinci-pas", store.LoadedPackageIds);

        // Definition absence must not be mistaken for disabled executable operations.
        foreach (string operation in new[] { "submit", "inquire" })
        {
            CompatibilityResponse dispatch = await replay.SendAsync(new(tenant.Name, "POST", $"Claim/${operation}") { Format = format });
            AssertOperationOutcome(replay, tenant, format, "authority-route-" + operation, dispatch,
                HttpStatusCode.UnprocessableEntity, false, false,
                [new("fatal", "structure", operation == "submit"
                    ? "PAS Claim Submit requires a PASRequestBundle as input."
                    : "PAS Claim Inquiry requires a PASClaimInquiryBundle as input.")]);
            Assert.False(dispatch.HttpHeaders.ContainsKey("Allow"));
        }

        CompatibilityResponse search = await replay.SendAsync(new(tenant.Name, "GET", "OperationDefinition") { Format = format });
        AssertExchangeContainer(replay, tenant, format, "authority-definitions", search, "Bundle", string.Empty);
        string[] definitions = ExchangeRepeated(ExchangeResource(search), format, "entry")
            .Select(entry => ExchangeNestedResource(entry, format, "resource")).ToArray();
        Assert.Equal(OperationContracts(version, includesDefinitions).Select(op => op.Name[1..]).Order(StringComparer.Ordinal),
            definitions.Select(definition => ExchangePrimitive(definition, format, "code")).Order(StringComparer.Ordinal));
        foreach (OperationContract op in OperationContracts(version, true).Where(op => op.SuppliedId is not null))
        {
            if (!includesDefinitions)
            {
                Assert.DoesNotContain(definitions, definition => ExchangePrimitive(definition, format, "url") == op.Canonical);
                CompatibilityResponse missing = await replay.SendAsync(new(tenant.Name, "GET", "OperationDefinition/" + op.SuppliedId)
                {
                    Format = format,
                });
                AssertOperationOutcome(replay, tenant, format, "absent-" + op.SuppliedId, missing,
                    HttpStatusCode.NotFound, false, false,
                    [new("error", "exception", $"Resource: OperationDefinition/{op.SuppliedId} not found")]);
                continue;
            }
            string definition = Assert.Single(definitions, definition => ExchangePrimitive(definition, format, "url") == op.Canonical);
            string expected = PasDefinitionContent(format, op.Name);
            GeneratedResourceExpectation binding = new("supplied-" + op.SuppliedId, "OperationDefinition", "1", op.SuppliedId);
            WireContractAssertions.Body(new ResourceBodyExpectation(expected, binding), definition, format, tenant.BaseUrl, replay.Bindings);
            await replay.StepAsync(BoundRead("read-" + op.SuppliedId, tenant, format, expected, binding));
        }
    }

    /// <summary>Only the package-enabled R4 interaction hook may execute an eligible local data-query Task.</summary>
    [Theory]
    [MemberData(nameof(InteractionHookCases))]
    public async Task InteractionHookRequiresPackageAndEligibleTask(
        string label,
        FhirReleases.FhirSequenceCodes version,
        CompatibilityTransport transport,
        CompatibilityFormat format,
        string kind)
    {
        CompatibilityTenant tenant = Tenant(version, $"cdex-{VersionName(version)}");
        CompatibilityScenario scenario = new(label, DiscoveryEvidence, [tenant], [MetadataStep(tenant, format)]);
        await using CompatibilityReplay replay = new(scenario, transport);
        await replay.RunAsync();
        IFhirStore store = replay.GetOwnedStore(tenant.Name);
        const string hookId = "036a8204-4d4f-46fc-a715-900bc2790a16";
        Dictionary<string, string> hooks = Assert.IsType<Dictionary<string, string>>(
            store.GetType().GetField("_hookNamesById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store));
        Assert.DoesNotContain(hookId, hooks.Keys);
        using ExpandedOperationPackage package = new("hl7.fhir.us.davinci-cdex", version);
        if (kind != "package-absent")
        {
            package.Load(store);
            Assert.Contains(package.Directive, store.LoadedPackageDirectives);
            Assert.Contains("hl7.fhir.us.davinci-cdex", store.LoadedPackageIds);
        }
        bool enabled = version == FhirReleases.FhirSequenceCodes.R4 && kind != "package-absent";
        Assert.Equal(enabled, hooks.ContainsKey(hookId));
        Type? hookType = store.GetType().Assembly.GetType("FhirCandle.R4.InteractionHooks.CDexTaskProcess");
        if (version == FhirReleases.FhirSequenceCodes.R4)
        {
            Assert.NotNull(hookType);
            Assert.Contains(hookType.GetInterfaces(), contract => contract.FullName == "FhirCandle.Interactions.IFhirInteractionHook");
            Assert.DoesNotContain(hookType.GetInterfaces(), contract => contract.Name == "IFhirOperation");
            object hook = Activator.CreateInstance(hookType)!;
            Assert.Equal("hl7.fhir.us.davinci-cdex", ReadOperationProperty<string>(hook, "RequiresPackage"));
            Assert.Equal(new[] { FhirReleases.FhirSequenceCodes.R4 },
                ReadOperationProperty<HashSet<FhirReleases.FhirSequenceCodes>>(hook, "SupportedFhirVersions"));
        }
        else
        {
            Assert.Null(hookType);
        }

        string patient = CdexContent(format, "patient");
        GeneratedResourceExpectation patientBinding = new("cdex-patient-v1", "Patient", "1", "cdex-patient");
        await replay.StepAsync(new("cdex-patient", new(tenant.Name, "PUT", "Patient/cdex-patient") { Format = format, Content = patient },
            ResourceResponse("cdex-patient", patient, patientBinding, HttpStatusCode.Created)));
        string task = CdexContent(format, kind is "package-absent" or "eligible-post" ? "eligible" : kind);
        bool post = kind == "eligible-post";
        if (post)
        {
            task = OperationWithoutId(task, format);
        }
        bool completes = enabled && kind is "eligible" or "eligible-post";
        CompatibilityResponse write = await replay.SendAsync(new(tenant.Name, post ? "POST" : "PUT", post ? "Task" : "Task/cdex-task")
        {
            Format = format,
            Content = task,
        });
        Assert.True(write.Status == (completes ? HttpStatusCode.OK : HttpStatusCode.Created),
            $"{label}: {write.Status}; {ExchangeResource(write)}; {write.Direct?.SerializedOutcome}");
        string written = ExchangeResource(write);
        GeneratedResourceExpectation taskBinding = new("cdex-task-result", "Task", completes ? "2" : "1", post ? null : "cdex-task");
        GeneratedResourceBinding taskIdentity = WireContractAssertions.BindResource(taskBinding, written, format, tenant.BaseUrl, replay.Bindings);
        if (!completes)
        {
            WireContractAssertions.Body(new ResourceBodyExpectation(task, taskBinding with { Mode = CompatibilityBindingMode.Reuse }),
                written, format, tenant.BaseUrl, replay.Bindings);
            Assert.False(ExchangeHasMember(written, format, "contained"));
            Assert.False(ExchangeHasMember(written, format, "output"));
        }
        else
        {
            Assert.Equal("completed", ExchangePrimitive(written, format, "status"));
            Assert.Equal("order", ExchangePrimitive(written, format, "intent"));
            AssertExchangeMemberContent(task, written, format, "input");
            string contained = Assert.Single(ExchangeRepeated(written, format, "contained"));
            string bundle = format == CompatibilityFormat.Json ? contained : Assert.Single(ExchangeXml(contained).Elements()).ToString(SaveOptions.DisableFormatting);
            string bundleId = ExchangePrimitive(bundle, format, "id");
            Assert.True(Guid.TryParse(bundleId, out _));
            replay.Bindings.Values.Add("cdex-contained-bundle", new("cdex-contained-bundle", bundleId));
            Assert.Equal("Bundle", ExchangeResourceType(bundle, format));
            Assert.Equal("searchset", ExchangePrimitive(bundle, format, "type"));
            Assert.Equal(1, SearchTotal(bundle, format));
            string entry = Assert.Single(ExchangeRepeated(bundle, format, "entry"));
            Assert.Equal(tenant.BaseUrl + "/Patient/cdex-patient", ExchangePrimitive(entry, format, "fullUrl"));
            Assert.Equal("match", ExchangePrimitive(ExchangeChild(entry, format, "search"), format, "mode"));
            WireContractAssertions.Body(new ResourceBodyExpectation(patient, patientBinding with { Mode = CompatibilityBindingMode.Reuse }),
                ExchangeNestedResource(entry, format, "resource"), format, tenant.BaseUrl, replay.Bindings);
            string output = Assert.Single(ExchangeRepeated(written, format, "output"));
            Assert.Equal("#" + bundleId, ExchangePrimitive(ExchangeChild(output, format, "valueReference"), format, "reference"));
            string coding = Assert.Single(ExchangeRepeated(ExchangeChild(output, format, "type"), format, "coding"));
            Assert.Equal("http://hl7.org/fhir/us/davinci-hrex/CodeSystem/hrex-temp", ExchangePrimitive(coding, format, "system"));
            Assert.Equal("data-query", ExchangePrimitive(coding, format, "code"));
            if (write.Direct is CompatibilityDirectResponse direct)
            {
                Assert.True(direct.Success);
                string issue = Assert.Single(ExchangeRepeated(direct.SerializedOutcome, format, "issue"));
                Assert.Equal("information", ExchangePrimitive(issue, format, "severity"));
                Assert.Equal("informational", ExchangePrimitive(issue, format, "code"));
                Assert.Equal($"Task/{taskIdentity.Id} updated.", ExchangePrimitive(issue, format, "diagnostics"));
            }
        }
        CompatibilityResponse read = await replay.SendAsync(new(tenant.Name, "GET", $"Task/{taskIdentity.Id}") { Format = format });
        WireContractAssertions.Status(HttpStatusCode.OK, read.Status);
        WireContractAssertions.BindResource(taskBinding with { Mode = CompatibilityBindingMode.Reuse }, ExchangeResource(read), format, tenant.BaseUrl, replay.Bindings);
        WireContractAssertions.Content(written, ExchangeResource(read), format);
        await replay.StepAsync(BoundRead("patient-survives-hook", tenant, format, patient, patientBinding));
    }

    /// <summary>No domain matrix may become empty or silently drop a version/boundary.</summary>
    [Fact]
    public void OperationTheoryDataIsNonEmptyAndLabeled()
    {
        Assert.Equal(468, OperationValidationCases.Count());
        Assert.Equal(12, OperationBoundaryCases.Count());
        Assert.Equal(24, OperationDiscoveryCases.Count());
        Assert.Equal(204, OperationRouteCases.Count());
        Assert.Equal(72, InteractionHookCases.Count());
        Assert.Equal(12, OperationRouteCases.Count(row => (string)row[4] == "validate-get-not-allowed"));
        Assert.Equal(90, ValidateGetCases.Count());
        Assert.Equal(18, ValidateGetCases.Count(row => (string)row[4] == "non-fhir-object"));
        Assert.Equal(156, ValidateMethodGuardCases.Count());
        Assert.Equal(72, ValidateGetPreferenceCases.Count());
        Assert.Equal(44, OtherOperationVerbCases.Count());
        Assert.Equal(8, PasDefinitionAuthorityCases.Count());
        Assert.Equal(12, OperationCapabilityRefreshCases.Count());
        foreach (IEnumerable<object[]> matrix in new[]
        {
            OperationValidationCases, OperationBoundaryCases, OperationDiscoveryCases, OperationRouteCases, InteractionHookCases,
            ValidateGetCases, ValidateMethodGuardCases, ValidateGetPreferenceCases, OtherOperationVerbCases, PasDefinitionAuthorityCases,
            OperationCapabilityRefreshCases,
        })
        {
            object[][] rows = matrix.ToArray();
            Assert.NotEmpty(rows);
            Assert.Equal(rows.Length, rows.Select(row => (string)row[0]).Distinct(StringComparer.Ordinal).Count());
            Assert.All(rows, row =>
            {
                Assert.Contains(row[1].ToString()!, (string)row[0], StringComparison.Ordinal);
                Assert.Contains(row[2].ToString()!, (string)row[0], StringComparison.Ordinal);
                Assert.Contains(row[3].ToString()!, (string)row[0], StringComparison.Ordinal);
            });
        }
    }

    private sealed record OperationContract(
        string Name, string Canonical, bool System, bool Type, bool Instance, bool Get, bool AffectsState,
        string[] Resources, string Package = "", string Version = "0.0.1", string? SuppliedId = null);

    private static OperationContract[] OperationContracts(FhirReleases.FhirSequenceCodes version, bool pas)
    {
        string subscriptionRoot = version == FhirReleases.FhirSequenceCodes.R5
            ? "http://hl7.org/fhir/OperationDefinition/Subscription-"
            : "http://hl7.org/fhir/uv/subscriptions-backport/OperationDefinition/backport-subscription-";
        List<OperationContract> contracts =
        [
            new("$validate", "http://hl7.org/fhir/OperationDefinition/Resource-validate", true, true, true, false, false, []),
            new("$convert", "http://hl7.org/fhir/OperationDefinition/Resource-convert", true, false, false, false, false, []),
            new("$events", subscriptionRoot + "events", false, false, true, true, false, ["Subscription"]),
            new("$status", subscriptionRoot + "status", false, true, true, true, false, ["Subscription"]),
            new("$subscription-hook", "http://argo.run/fhir/OperationDefinition/subscription-hook", true, false, false, false, true, []),
            new("$feature-query", "http://www.hl7.org/fhir/uv/capstmt/OperationDefinition/feature-query", true, false, false, true, false, []),
            new("$test-if-fhir", "http://ginoc.io/fhir/OperationDefinition/test-if-fhir", true, false, false, false, false, []),
            new("$reset-store", "http://ginoc.io/fhir/OperationDefinition/reset-store", true, false, false, false, true, []),
        ];
        if (pas)
        {
            contracts.Add(new("$submit", "http://hl7.org/fhir/us/davinci-pas/OperationDefinition/Claim-submit",
                false, true, false, false, true, ["Claim"], "hl7.fhir.us.davinci-pas", "1.2.0", "sdk6-pas-claim-submit"));
            contracts.Add(new("$inquire", "http://hl7.org/fhir/us/davinci-pas/OperationDefinition/Claim-inquiry",
                false, true, false, false, false, ["Claim"], "hl7.fhir.us.davinci-pas", "1.2.0", "sdk6-pas-claim-inquiry"));
        }
        return contracts.ToArray();
    }

    private static T ReadOperationProperty<T>(object instance, string name) =>
        (T)instance.GetType().GetProperty(name)!.GetValue(instance)!;

    private static bool OperationBoolean(string content, CompatibilityFormat format, string name)
    {
        if (format == CompatibilityFormat.Xml)
        {
            return bool.Parse(ExchangePrimitive(content, format, name));
        }
        using JsonDocument document = JsonDocument.Parse(content);
        return document.RootElement.GetProperty(name).GetBoolean();
    }

    private static void AssertOperationAdvertisements(IEnumerable<OperationContract> expected, string component, CompatibilityFormat format)
    {
        string[] operations = ExchangeRepeated(component, format, "operation");
        Assert.Equal(expected.Select(op => op.Name).Order(StringComparer.Ordinal),
            operations.Select(operation => ExchangePrimitive(operation, format, "name")).Order(StringComparer.Ordinal));
        foreach (OperationContract op in expected)
        {
            string operation = Assert.Single(operations, operation => ExchangePrimitive(operation, format, "name") == op.Name);
            Assert.Equal(op.Canonical, ExchangePrimitive(operation, format, "definition"));
        }
    }

    private static string AssertOperationCapabilities(
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        OperationContract[] expected,
        CompatibilityResponse response)
    {
        WireContractAssertions.Status(HttpStatusCode.OK, response.Status);
        Assert.Equal(WireContractAssertions.MediaType(format), response.MediaType);
        if (response.Direct is CompatibilityDirectResponse direct)
        {
            Assert.True(direct.Success);
            Assert.Equal("CapabilityStatement", direct.ResourceType);
            Assert.Equal("metadata", direct.Id);
        }
        string content = ExchangeResource(response);
        Assert.Equal("CapabilityStatement", ExchangeResourceType(content, format));
        Assert.Equal("metadata", ExchangePrimitive(content, format, "id"));
        Assert.Equal(ExpectedFhirVersion(tenant.Version), ExchangePrimitive(content, format, "fhirVersion"));
        Assert.Equal(tenant.BaseUrl + "/CapabilityStatement/metadata", ExchangePrimitive(content, format, "url"));
        string rest = Assert.Single(ExchangeRepeated(content, format, "rest"));
        AssertOperationAdvertisements(expected.Where(op => op.System), rest, format);
        foreach (string resourceType in new[] { "Patient", "Subscription", "Claim" })
        {
            string resource = Assert.Single(ExchangeRepeated(rest, format, "resource"),
                resource => ExchangePrimitive(resource, format, "type") == resourceType);
            AssertOperationAdvertisements(expected.Where(op => (op.Type || op.Instance) &&
                (op.Resources.Length == 0 || op.Resources.Contains(resourceType, StringComparer.Ordinal))), resource, format);
        }
        return content;
    }

    private static string ValidateOperationPath(string level, string id = "focus") => level switch
    {
        "system" => "$validate",
        "type" => "Patient/$validate",
        "instance" => $"Patient/{id}/$validate",
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    private static Common.StoreInteractionCodes OperationInteraction(string level) => level switch
    {
        "system" => Common.StoreInteractionCodes.SystemOperation,
        "type" => Common.StoreInteractionCodes.TypeOperation,
        "instance" => Common.StoreInteractionCodes.InstanceOperation,
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    private static bool InvokeOperationWrapper(IFhirStore store, string level, FhirRequestContext context, out FhirResponseContext response) =>
        level switch
        {
            "system" => store.SystemOperation(context, out response),
            "type" => store.TypeOperation(context, out response),
            "instance" => store.InstanceOperation(context, out response),
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };

    private static CompatibilityResponse DirectOperationResponse(bool success, FhirResponseContext response) =>
        new(CompatibilityTransport.Direct, response.StatusCode ?? throw new InvalidOperationException("Missing operation status."), response.MimeType)
        {
            Direct = new(success, response.SerializedResource, response.SerializedOutcome, response.ResourceType, response.Id,
                response.ETag, response.LastModified, response.Location),
        };

    private static void AssertValidateGetRejection(
        CompatibilityReplay replay,
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        string name,
        CompatibilityResponse response,
        bool minimal = false)
    {
        WireContractAssertions.Status(HttpStatusCode.MethodNotAllowed, response.Status);
        Assert.Equal(WireContractAssertions.MediaType(format), response.MediaType);
        if (response.Transport == CompatibilityTransport.Http)
        {
            Assert.Null(response.Direct);
            Assert.Equal(new[] { "POST" }, response.HttpHeaders["Allow"]);
            foreach (CompatibilityHeaderExpectation header in NoExchangeHeaders())
            {
                Assert.False(response.HttpHeaders.ContainsKey(header.Name));
            }
        }
        else
        {
            Assert.False(minimal);
            Assert.Empty(response.HttpHeaders);
        }
        if (minimal)
        {
            Assert.Equal(CompatibilityTransport.Http, response.Transport);
            Assert.Empty(response.HttpBody!);
            return;
        }
        AssertOperationOutcome(replay, tenant, format, name, response, HttpStatusCode.MethodNotAllowed, false, false,
            [new("error", "not-supported", ValidateGetDiagnostic)]);
    }

    private static async Task AssertValidateHttpEarlierGuard(CompatibilityTenant tenant, CompatibilityFormat format, string kind)
    {
        bool requiresAuth = kind.StartsWith("unauthorized-", StringComparison.Ordinal);
        string level = kind[(kind.LastIndexOf('-') + 1)..];
        await using CandleWebApplicationFactory factory = new(
        [
            new TenantConfiguration
            {
                FhirVersion = tenant.Version,
                ControllerName = tenant.Name,
                BaseUrl = tenant.BaseUrl,
                SmartRequired = requiresAuth,
                SmartAllowed = false,
            },
        ]);
        const string MissingTenant = "missing-operation-tenant";
        string baseUrl = requiresAuth ? tenant.BaseUrl : "http://localhost/fhir/" + MissingTenant;
        // The missing instance also proves authorization precedes the store's existence check.
        using HttpRequestMessage request = new(HttpMethod.Get, baseUrl + "/" + ValidateOperationPath(level, "missing"));
        request.Headers.Accept.ParseAdd(WireContractAssertions.MediaType(format));
        using HttpResponseMessage response = await factory.Client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        Dictionary<string, string[]> headers = response.Headers.Concat(response.Content.Headers)
            .ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(requiresAuth ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(headers.ContainsKey("Allow"));
        foreach (CompatibilityHeaderExpectation header in NoExchangeHeaders())
        {
            Assert.False(headers.ContainsKey(header.Name));
        }
        if (requiresAuth)
        {
            Assert.Empty(body);
        }
        else
        {
            Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
            string handler = level switch
            {
                "system" => "GetSystemOperation",
                "type" => "GetTypeOperation",
                "instance" => "GetInstanceOperation",
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
            Assert.Equal($"{handler} <<< no tenant at {MissingTenant}!", body);
        }
    }

    private static string PasDefinitionContent(CompatibilityFormat format, string operation) => (format, operation) switch
    {
        // Independent expected literals: no expected-side SDK parsing, output capture, or version-derived IDs.
        (CompatibilityFormat.Json, "$submit") => """
            {"resourceType":"OperationDefinition","id":"sdk6-pas-claim-submit","url":"http://hl7.org/fhir/us/davinci-pas/OperationDefinition/Claim-submit","name":"$submit","status":"draft","kind":"operation","affectsState":true,"code":"submit","resource":["Claim"],"system":false,"type":true,"instance":false}
            """,
        (CompatibilityFormat.Json, "$inquire") => """
            {"resourceType":"OperationDefinition","id":"sdk6-pas-claim-inquiry","url":"http://hl7.org/fhir/us/davinci-pas/OperationDefinition/Claim-inquiry","name":"$inquire","status":"draft","kind":"operation","affectsState":false,"code":"inquire","resource":["Claim"],"system":false,"type":true,"instance":false}
            """,
        (CompatibilityFormat.Xml, "$submit") => """
            <OperationDefinition xmlns="http://hl7.org/fhir"><id value="sdk6-pas-claim-submit"/><url value="http://hl7.org/fhir/us/davinci-pas/OperationDefinition/Claim-submit"/><name value="$submit"/><status value="draft"/><kind value="operation"/><affectsState value="true"/><code value="submit"/><resource value="Claim"/><system value="false"/><type value="true"/><instance value="false"/></OperationDefinition>
            """,
        (CompatibilityFormat.Xml, "$inquire") => """
            <OperationDefinition xmlns="http://hl7.org/fhir"><id value="sdk6-pas-claim-inquiry"/><url value="http://hl7.org/fhir/us/davinci-pas/OperationDefinition/Claim-inquiry"/><name value="$inquire"/><status value="draft"/><kind value="operation"/><affectsState value="false"/><code value="inquire"/><resource value="Claim"/><system value="false"/><type value="true"/><instance value="false"/></OperationDefinition>
            """,
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    private static string CdexContent(CompatibilityFormat format, string kind)
    {
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument fixture = JsonDocument.Parse(CompatibilityReplay.LoadFixture("r4/cdex-task-resources.json"));
            return fixture.RootElement.GetProperty(kind).GetRawText();
        }
        if (kind == "patient")
        {
            return """<Patient xmlns="http://hl7.org/fhir"><id value="cdex-patient"/><active value="true"/><name><family value="Synthetic CDex"/><given value="Local"/></name><birthDate value="1980-02-03"/></Patient>""";
        }
        string status = kind == "wrong-status" ? "draft" : "requested";
        string intent = kind == "wrong-intent" ? "proposal" : "order";
        string input = kind == "missing-query" ? string.Empty
            : """<input><type><coding><system value="http://hl7.org/fhir/us/davinci-hrex/CodeSystem/hrex-temp"/><code value="data-query"/></coding></type><valueString value="Patient?_id=cdex-patient"/></input>""";
        return $"""<Task xmlns="http://hl7.org/fhir"><id value="cdex-task"/><status value="{status}"/><intent value="{intent}"/>{input}</Task>""";
    }

    private static string OperationWithoutId(string content, CompatibilityFormat format)
    {
        if (format == CompatibilityFormat.Json)
        {
            JsonObject resource = JsonNode.Parse(content)!.AsObject();
            Assert.True(resource.Remove("id"));
            return resource.ToJsonString();
        }
        XElement root = ExchangeXml(content);
        Assert.Single(root.Elements(_exchangeFhir + "id")).Remove();
        return root.ToString(SaveOptions.DisableFormatting);
    }

    private sealed class ExpandedOperationPackage : IDisposable
    {
        // Minimal synthetic package resources, not official PAS IG definitions or generated SDK output.
        private const string SubmitDefinition = """
            {
              "resourceType": "OperationDefinition",
              "id": "sdk6-pas-claim-submit",
              "url": "http://hl7.org/fhir/us/davinci-pas/OperationDefinition/Claim-submit",
              "name": "$submit",
              "status": "draft",
              "kind": "operation",
              "affectsState": true,
              "code": "submit",
              "resource": [ "Claim" ],
              "system": false,
              "type": true,
              "instance": false
            }
            """;

        private const string InquiryDefinition = """
            {
              "resourceType": "OperationDefinition",
              "id": "sdk6-pas-claim-inquiry",
              "url": "http://hl7.org/fhir/us/davinci-pas/OperationDefinition/Claim-inquiry",
              "name": "$inquire",
              "status": "draft",
              "kind": "operation",
              "affectsState": false,
              "code": "inquire",
              "resource": [ "Claim" ],
              "system": false,
              "type": true,
              "instance": false
            }
            """;

        internal ExpandedOperationPackage(string packageId, FhirReleases.FhirSequenceCodes version, bool includePasDefinitions = true)
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "fhir-candle-operation-package-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(DirectoryPath, "package"));
            try
            {
                string destination = Path.Combine(DirectoryPath, "package", "package.json");
                File.Copy(Path.Combine(AppContext.BaseDirectory, "data", "sdk6-compatibility", "r4", "cdex-package-manifest.json"), destination);
                if (packageId != "hl7.fhir.us.davinci-cdex")
                {
                    // The manifest establishes the package-ID gate independently of any resource definitions.
                    JsonObject manifest = JsonNode.Parse(File.ReadAllText(destination))!.AsObject();
                    manifest["name"] = packageId;
                    manifest["fhirVersions"] = new JsonArray(ExpectedFhirVersion(version));
                    manifest["description"] = "Synthetic expanded package for fhir-candle operation tests; not official IG content.";
                    File.WriteAllText(destination, manifest.ToJsonString());
                }
                if (packageId == "hl7.fhir.us.davinci-pas" && version == FhirReleases.FhirSequenceCodes.R4 && includePasDefinitions)
                {
                    File.WriteAllText(Path.Combine(DirectoryPath, "package", "OperationDefinition-sdk6-pas-claim-submit.json"), SubmitDefinition);
                    File.WriteAllText(Path.Combine(DirectoryPath, "package", "OperationDefinition-sdk6-pas-claim-inquiry.json"), InquiryDefinition);
                }
            }
            catch
            {
                Directory.Delete(DirectoryPath, recursive: true);
                throw;
            }
            Directive = packageId + "#0.0.0-sdk6-test";
        }

        internal string DirectoryPath { get; }
        internal string Directive { get; }

        internal void Load(IFhirStore store) => store.LoadPackage(Directive, DirectoryPath, string.Empty, includeExamples: false);

        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
    }

    private static IEnumerable<object[]> OperationRows(string group, string[] kinds)
    {
        foreach (FhirReleases.FhirSequenceCodes version in _versions)
        {
            foreach (CompatibilityTransport transport in Enum.GetValues<CompatibilityTransport>())
            {
                foreach (CompatibilityFormat format in Enum.GetValues<CompatibilityFormat>())
                {
                    foreach (string kind in kinds)
                    {
                        yield return [$"{group} / {kind} / {version} / {transport} / {format}", version, transport, format, kind];
                    }
                }
            }
        }
    }

    private static string? ValidationContent(FhirReleases.FhirSequenceCodes version, CompatibilityFormat format, string kind)
    {
        if (kind == "no-body")
        {
            return null;
        }
        if (kind == "malformed")
        {
            return format == CompatibilityFormat.Json ? """{"resourceType":"Patient","active":""" : """<Patient xmlns="http://hl7.org/fhir"><active""";
        }
        bool ignored = kind.StartsWith("ignored-", StringComparison.Ordinal);
        bool wrapped = kind.StartsWith("wrapped-", StringComparison.Ordinal) || ignored;
        string key = ignored ? (kind == "ignored-valid" ? "valid" : "missing-required") : kind.Replace("wrapped-", string.Empty, StringComparison.Ordinal);
        string body;
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument fixture = JsonDocument.Parse(CompatibilityReplay.LoadFixture(version, "validation-resources.json"));
            body = fixture.RootElement.GetProperty(key).GetRawText();
        }
        else
        {
            body = key switch
            {
                "valid" => """<Patient xmlns="http://hl7.org/fhir"><id value="validation-candidate"/><active value="true"/><gender value="female"/><birthDate value="1980-02-03"/></Patient>""",
                "valid-wrapped" => """<Parameters xmlns="http://hl7.org/fhir"><parameter><name value="resource"/><resource><Patient><id value="validation-candidate"/><active value="true"/><gender value="female"/><birthDate value="1980-02-03"/></Patient></resource></parameter></Parameters>""",
                "missing-required" => """<Observation xmlns="http://hl7.org/fhir"><id value="missing-code"/><status value="final"/></Observation>""",
                "primitive" => """<Patient xmlns="http://hl7.org/fhir"><id value="invalid-primitive"/><active value="not-a-boolean"/></Patient>""",
                "narrative" => """<Patient xmlns="http://hl7.org/fhir"><id value="invalid-narrative"/><text><status value="generated"/><div xmlns="http://www.w3.org/1999/xhtml"><not-fhir-xhtml>Invalid narrative element</not-fhir-xhtml></div></text></Patient>""",
                "no-target" => """<Parameters xmlns="http://hl7.org/fhir"/>""",
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
        }
        if (!wrapped)
        {
            return body;
        }
        if (format == CompatibilityFormat.Json)
        {
            string notes = ignored
                ? """,{"name":"mode","valueCode":"create"},{"name":"profile","valueCanonical":"http://localhost/StructureDefinition/synthetic-profile"}"""
                : string.Empty;
            return $$$"""{"resourceType":"Parameters","parameter":[{"name":"resource","resource":{{{body}}}}{{{notes}}}]}""";
        }
        string xmlNotes = ignored
            ? """<parameter><name value="mode"/><valueCode value="create"/></parameter><parameter><name value="profile"/><valueCanonical value="http://localhost/StructureDefinition/synthetic-profile"/></parameter>"""
            : string.Empty;
        return $"""<Parameters xmlns="http://hl7.org/fhir"><parameter><name value="resource"/><resource>{body}</resource></parameter>{xmlNotes}</Parameters>""";
    }

    private static string[] OperationPrimitiveArray(string content, CompatibilityFormat format, string name)
    {
        if (!ExchangeHasMember(content, format, name))
        {
            return [];
        }
        if (format == CompatibilityFormat.Json)
        {
            using JsonDocument document = JsonDocument.Parse(content);
            return document.RootElement.GetProperty(name).EnumerateArray().Select(value => value.GetString()!).ToArray();
        }
        return ExchangeXml(content).Elements(_exchangeFhir + name).Select(element => (string)element.Attribute("value")!).ToArray();
    }

    private static string OperationOutcomeBody(CompatibilityResponse response) => response.Transport == CompatibilityTransport.Http
        ? response.HttpBody!
        : response.Direct!.SerializedOutcome;

    private static void AssertOperationOutcome(
        CompatibilityReplay replay,
        CompatibilityTenant tenant,
        CompatibilityFormat format,
        string name,
        CompatibilityResponse response,
        HttpStatusCode status,
        bool success,
        bool resourceIsOutcome,
        IReadOnlyList<OutcomeIssueExpectation> issues)
    {
        OutcomeBodyExpectation outcome = new(name + "-outcome", issues);
        WireContractAssertions.Status(status, response.Status);
        Assert.Equal(WireContractAssertions.MediaType(format), response.MediaType);
        WireContractAssertions.Body(outcome, OperationOutcomeBody(response), format, tenant.BaseUrl, replay.Bindings);
        if (response.Direct is CompatibilityDirectResponse direct)
        {
            Assert.Null(response.HttpBody);
            Assert.Equal(success, direct.Success);
            Assert.Empty(direct.ETag);
            Assert.Empty(direct.LastModified);
            Assert.Empty(direct.Location);
            if (resourceIsOutcome)
            {
                Assert.Equal("OperationOutcome", direct.ResourceType);
                Assert.Equal(ExchangePrimitive(direct.SerializedOutcome, format, "id"), direct.Id);
                WireContractAssertions.Content(direct.SerializedOutcome, direct.SerializedResource, format);
            }
            else
            {
                Assert.Empty(direct.SerializedResource);
                Assert.Empty(direct.ResourceType);
                Assert.Empty(direct.Id);
            }
        }
        else
        {
            Assert.Equal(CompatibilityTransport.Http, response.Transport);
            foreach (CompatibilityHeaderExpectation header in NoExchangeHeaders())
            {
                Assert.False(response.HttpHeaders.ContainsKey(header.Name));
            }
        }
    }
}

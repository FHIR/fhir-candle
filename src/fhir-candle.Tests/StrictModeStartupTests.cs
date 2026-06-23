// <copyright file="StrictModeStartupTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Configuration;
using FhirCandle.Models;
using FhirCandle.Strict;
using FhirCandle.Utils;
using Shouldly;

namespace fhir.candle.Tests;

/// <summary>
/// Tests for the strict-mode startup composition: <see cref="TenantConfiguration.ResolveStrict"/>
/// and <see cref="Program.ComputeStrictWarnings"/>. These are pure / config-only and don't
/// stand up a store; the store-level composition is covered by <c>TestStrictModeCompositionWins</c>.
/// </summary>
public class StrictModeStartupTests
{
    private static TenantConfiguration NewTenant(bool strict, bool allowExistingId, bool allowCreateAsUpdate)
    {
        return new TenantConfiguration
        {
            FhirVersion = FhirReleases.FhirSequenceCodes.R4,
            ControllerName = "r4",
            BaseUrl = "http://localhost/fhir/r4",
            Strict = strict,
            AllowExistingId = allowExistingId,
            AllowCreateAsUpdate = allowCreateAsUpdate,
        };
    }

    [Fact]
    public void TenantConfigurationResolveStrict_FlipsAllowFlags()
    {
        TenantConfiguration cfg = NewTenant(strict: true, allowExistingId: true, allowCreateAsUpdate: true);

        cfg.ResolveStrict();

        cfg.AllowExistingId.ShouldBeFalse();
        cfg.AllowCreateAsUpdate.ShouldBeFalse();
        cfg.Strict.ShouldBeTrue();
    }

    [Fact]
    public void TenantConfigurationResolveStrict_IsIdempotent()
    {
        TenantConfiguration cfg = NewTenant(strict: true, allowExistingId: true, allowCreateAsUpdate: true);

        cfg.ResolveStrict();
        cfg.ResolveStrict();
        cfg.ResolveStrict();

        cfg.AllowExistingId.ShouldBeFalse();
        cfg.AllowCreateAsUpdate.ShouldBeFalse();
    }

    [Fact]
    public void TenantConfigurationResolveStrict_NoOpWhenNotStrict()
    {
        TenantConfiguration cfg = NewTenant(strict: false, allowExistingId: true, allowCreateAsUpdate: true);

        cfg.ResolveStrict();

        cfg.AllowExistingId.ShouldBeTrue();
        cfg.AllowCreateAsUpdate.ShouldBeTrue();
    }

    private static CandleConfig NewCandleConfig()
    {
        // Default construction populates options but does not populate the Raw* properties.
        // We set them via the public Strict / private-setter Raw* properties through
        // object initializers — but Raw* setters are private. Use the same trick as
        // production: parse an empty command line and then mutate the public surface.
        // For these unit tests we only need the warning logic, which reads
        // Strict + Raw*. Construct directly via parse-result-less ctor: not exposed.
        // Workaround: use reflection-free access through a small derived test helper
        // is overkill; instead we construct via the parameterless flow if available.
        return new CandleConfig();
    }

    /// <summary>
    /// Test-only helper that constructs a <see cref="CandleConfig"/> with the strict
    /// + raw values we care about. Uses reflection on the private setters of the
    /// Raw* properties because the production ctor only populates them via the CLI
    /// parser, which we don't want to drag into a unit test.
    /// </summary>
    private static CandleConfig ConfigWithStrict(bool strict, bool? rawAllowExistingId, bool? rawAllowCreateAsUpdate)
    {
        CandleConfig cfg = new();
        cfg.Strict = strict;
        typeof(CandleConfig).GetProperty(nameof(CandleConfig.RawAllowExistingId))!
            .SetValue(cfg, rawAllowExistingId);
        typeof(CandleConfig).GetProperty(nameof(CandleConfig.RawAllowCreateAsUpdate))!
            .SetValue(cfg, rawAllowCreateAsUpdate);
        return cfg;
    }

    [Fact]
    public void ComputeStrictWarnings_EmptyWhenStrictDisabled()
    {
        CandleConfig cfg = ConfigWithStrict(strict: false, rawAllowExistingId: true, rawAllowCreateAsUpdate: true);

        List<Program.StrictConflictWarning> warnings = Program.ComputeStrictWarnings(cfg);

        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void ComputeStrictWarnings_EmptyWhenNoExplicitConflict()
    {
        CandleConfig cfg = ConfigWithStrict(strict: true, rawAllowExistingId: null, rawAllowCreateAsUpdate: null);

        List<Program.StrictConflictWarning> warnings = Program.ComputeStrictWarnings(cfg);

        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void ComputeStrictWarnings_EmptyWhenExplicitValueAgreesWithStrict()
    {
        CandleConfig cfg = ConfigWithStrict(strict: true, rawAllowExistingId: false, rawAllowCreateAsUpdate: false);

        List<Program.StrictConflictWarning> warnings = Program.ComputeStrictWarnings(cfg);

        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void ComputeStrictWarnings_WarnsOnExplicitConflict()
    {
        CandleConfig cfg = ConfigWithStrict(strict: true, rawAllowExistingId: true, rawAllowCreateAsUpdate: true);

        List<Program.StrictConflictWarning> warnings = Program.ComputeStrictWarnings(cfg);

        warnings.Count.ShouldBe(2);

        Program.StrictConflictWarning existing = warnings.Single(w => w.Flag == "create-existing-id");
        existing.RawValue.ShouldBe(true);
        existing.StrictEnforced.ShouldBeFalse();
        existing.Rule.ShouldBe(StrictRuleCode.PostClientSuppliedId);

        Program.StrictConflictWarning createAsUpdate = warnings.Single(w => w.Flag == "create-as-update");
        createAsUpdate.RawValue.ShouldBe(true);
        createAsUpdate.StrictEnforced.ShouldBeFalse();
        createAsUpdate.Rule.ShouldBe(StrictRuleCode.PutCreateAsUpdateDisallowed);
    }

    [Fact]
    public void StrictRule_GetSpecUrl_ReturnsPerVersionUrls()
    {
        StrictRule.GetSpecUrl(StrictRuleCode.PutEmptyBodyId, FhirReleases.FhirSequenceCodes.R4)
            .ShouldBe("http://hl7.org/fhir/R4/http.html#update");
        StrictRule.GetSpecUrl(StrictRuleCode.PutEmptyBodyId, FhirReleases.FhirSequenceCodes.R4B)
            .ShouldBe("http://hl7.org/fhir/R4B/http.html#update");
        StrictRule.GetSpecUrl(StrictRuleCode.PutEmptyBodyId, FhirReleases.FhirSequenceCodes.R5)
            .ShouldBe("http://hl7.org/fhir/R5/http.html#update");

        StrictRule.GetSpecUrl(StrictRuleCode.PostClientSuppliedId, FhirReleases.FhirSequenceCodes.R4)
            .ShouldBe("http://hl7.org/fhir/R4/http.html#create");
        StrictRule.GetSpecUrl(StrictRuleCode.PutCreateAsUpdateDisallowed, FhirReleases.FhirSequenceCodes.R5)
            .ShouldBe("http://hl7.org/fhir/R5/http.html#upsert");
        StrictRule.GetSpecUrl(StrictRuleCode.ResourceIdRegex, FhirReleases.FhirSequenceCodes.R4B)
            .ShouldBe("http://hl7.org/fhir/R4B/datatypes.html#id");
        StrictRule.GetSpecUrl(StrictRuleCode.SearchUnknownParameter, FhirReleases.FhirSequenceCodes.R5)
            .ShouldBe("http://hl7.org/fhir/R5/search.html#errors");
    }

    [Fact]
    public void StrictRule_SpecUrls_CoversEveryRuleCode()
    {
        foreach (StrictRuleCode rule in Enum.GetValues<StrictRuleCode>())
        {
            StrictRule.SpecUrls.ContainsKey(rule).ShouldBeTrue();
            IReadOnlyDictionary<FhirReleases.FhirSequenceCodes, string> perVersion = StrictRule.SpecUrls[rule];
            perVersion.ContainsKey(FhirReleases.FhirSequenceCodes.R4).ShouldBeTrue();
            perVersion.ContainsKey(FhirReleases.FhirSequenceCodes.R4B).ShouldBeTrue();
            perVersion.ContainsKey(FhirReleases.FhirSequenceCodes.R5).ShouldBeTrue();
        }
    }
}

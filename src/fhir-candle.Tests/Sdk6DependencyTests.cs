// <copyright file="Sdk6DependencyTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;
extern alias coreR4;
extern alias coreR4B;
extern alias coreR5;

using System.Net;
using System.Text.Json;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirCandle.Utils;
using Hl7.Fhir.Introspection;
using Shouldly;

namespace fhir.candle.Tests;

/// <summary>Verifies the SDK model and store aliases for each supported FHIR version.</summary>
public class Sdk6DependencyTests
{
    /// <summary>Checks that each model alias and inspector describe the intended FHIR release.</summary>
    /// <param name="version">The FHIR release selected by the alias.</param>
    /// <param name="expectedAssemblyName">The expected SDK model assembly name.</param>
    /// <param name="expectedFhirVersion">The expected FHIR specification version.</param>
    [Theory]
    [InlineData(FhirReleases.FhirSequenceCodes.R4, "Hl7.Fhir.R4", "4.0.1")]
    [InlineData(FhirReleases.FhirSequenceCodes.R4B, "Hl7.Fhir.R4B", "4.3.0")]
    [InlineData(FhirReleases.FhirSequenceCodes.R5, "Hl7.Fhir.R5", "5.0.0")]
    public void AliasedModelsAndInspectorsMatchFhirVersion(
        FhirReleases.FhirSequenceCodes version,
        string expectedAssemblyName,
        string expectedFhirVersion)
    {
        (Type patientType, ModelInspector inspector, string modelVersion) = version switch
        {
            FhirReleases.FhirSequenceCodes.R4 => (
                typeof(coreR4::Hl7.Fhir.Model.Patient),
                coreR4::Hl7.Fhir.Model.ModelInfo.ModelInspector,
                coreR4::Hl7.Fhir.Model.ModelInfo.Version),
            FhirReleases.FhirSequenceCodes.R4B => (
                typeof(coreR4B::Hl7.Fhir.Model.Patient),
                coreR4B::Hl7.Fhir.Model.ModelInfo.ModelInspector,
                coreR4B::Hl7.Fhir.Model.ModelInfo.Version),
            FhirReleases.FhirSequenceCodes.R5 => (
                typeof(coreR5::Hl7.Fhir.Model.Patient),
                coreR5::Hl7.Fhir.Model.ModelInfo.ModelInspector,
                coreR5::Hl7.Fhir.Model.ModelInfo.Version),
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        };

        patientType.Assembly.GetName().Name.ShouldBe(expectedAssemblyName);
        modelVersion.ShouldBe(expectedFhirVersion);
        inspector.FhirVersion.ShouldBe(expectedFhirVersion);

        ClassMapping? patientMapping = inspector.FindClassMapping("Patient");
        patientMapping.ShouldNotBeNull();
        patientMapping.NativeType.ShouldBe(patientType);
        patientMapping.Inspector.ShouldBeSameAs(inspector);
        inspector.FindClassMapping(patientType).ShouldBeSameAs(patientMapping);
    }

    /// <summary>Checks serialized store metadata without parsing it through the SDK.</summary>
    /// <param name="version">The FHIR release selected by the store alias.</param>
    /// <param name="expectedFhirVersion">The expected FHIR specification version.</param>
    [Theory]
    [InlineData(FhirReleases.FhirSequenceCodes.R4, "4.0.1")]
    [InlineData(FhirReleases.FhirSequenceCodes.R4B, "4.3.0")]
    [InlineData(FhirReleases.FhirSequenceCodes.R5, "5.0.0")]
    public void AliasedStoresAdvertiseExpectedFhirVersion(
        FhirReleases.FhirSequenceCodes version,
        string expectedFhirVersion)
    {
        using (IFhirStore store = version switch
        {
            FhirReleases.FhirSequenceCodes.R4 => new candleR4::FhirCandle.Storage.VersionedFhirStore(),
            FhirReleases.FhirSequenceCodes.R4B => new candleR4B::FhirCandle.Storage.VersionedFhirStore(),
            FhirReleases.FhirSequenceCodes.R5 => new candleR5::FhirCandle.Storage.VersionedFhirStore(),
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        })
        {
            store.GetType().Assembly.GetName().Name.ShouldBe($"FhirCandle.{version}");

            string tenantName = $"sdk6-{version.ToString().ToLowerInvariant()}";
            TenantConfiguration config = new()
            {
                FhirVersion = version,
                ControllerName = tenantName,
                BaseUrl = $"http://localhost/fhir/{tenantName}",
            };
            store.Init(config);

            FhirRequestContext ctx = new()
            {
                TenantName = store.Config.ControllerName,
                Store = store,
                HttpMethod = "GET",
                Url = $"{store.Config.BaseUrl}/metadata",
                Authorization = null,
                SourceFormat = "application/fhir+json",
                DestinationFormat = "application/fhir+json",
            };

            bool success = store.GetMetadata(ctx, out FhirResponseContext response);

            success.ShouldBeTrue();
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.SerializedResource.ShouldNotBeNullOrEmpty();

            using (JsonDocument metadata = JsonDocument.Parse(response.SerializedResource))
            {
                metadata.RootElement.GetProperty("resourceType").GetString().ShouldBe("CapabilityStatement");
                metadata.RootElement.GetProperty("fhirVersion").GetString().ShouldBe(expectedFhirVersion);
            }
        }
    }
}

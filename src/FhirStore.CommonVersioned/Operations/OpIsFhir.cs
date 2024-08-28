// <copyright file="OpSubscriptionEvents.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Extensions;
using FhirCandle.Models;
using FhirCandle.Subscriptions;
using FhirCandle.Versioned;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using System.Net;
using static Hl7.Fhir.Model.OperationOutcome;

namespace FhirCandle.Operations;

/// <summary>Operation to determine if a payload is FHIR: $test-if-fhir.</summary>
public class OpTestIfFhir : IFhirOperation
{
    /// <summary>Gets the name of the operation.</summary>
    public string OperationName => "$test-if-fhir";

    /// <summary>Gets the operation version.</summary>
    public string OperationVersion => "0.0.1";

    /// <summary>Gets the canonical by FHIR version.</summary>
    public Dictionary<FhirCandle.Utils.FhirReleases.FhirSequenceCodes, string> CanonicalByFhirVersion => new()
    {
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R4, "http://ginoc.io/fhir/OperationDefinition/test-if-fhir" },
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R4B, "http://ginoc.io/fhir/OperationDefinition/test-if-fhir" },
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R5, "http://ginoc.io/fhir/OperationDefinition/test-if-fhir" },
    };

    /// <summary>Gets a value indicating whether this operation is a named query.</summary>
    public bool IsNamedQuery => false;

    /// <summary>Gets a value indicating whether we allow get.</summary>
    public bool AllowGet => false;

    /// <summary>Gets a value indicating whether we allow post.</summary>
    public bool AllowPost => true;

    /// <summary>Gets a value indicating whether we allow system level.</summary>
    public bool AllowSystemLevel => true;

    /// <summary>Gets a value indicating whether we allow resource level.</summary>
    public bool AllowResourceLevel => false;

    /// <summary>Gets a value indicating whether we allow instance level.</summary>
    public bool AllowInstanceLevel => false;

    /// <summary>Gets a value indicating whether the accepts non FHIR.</summary>
    public bool AcceptsNonFhir => true;

    /// <summary>Gets a value indicating whether the returns non FHIR.</summary>
    public bool ReturnsNonFhir => false;

    /// <summary>
    /// If this operation requires a specific FHIR package to be loaded, the package identifier.
    /// </summary>
    public string RequiresPackage => string.Empty;

    /// <summary>Gets the supported resources.</summary>
    public HashSet<string> SupportedResources => new();

    /// <summary>Executes the Subscription/$events operation.</summary>
    /// <param name="ctx">          The context.</param>
    /// <param name="store">        The store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="focusResource">The focus resource.</param>
    /// <param name="bodyResource"> The body resource.</param>
    /// <param name="opResponse">   [out] The response resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool DoOperation(
        FhirRequestContext ctx,
        Storage.VersionedFhirStore store,
        Storage.IVersionedResourceStore? resourceStore,
        Hl7.Fhir.Model.Resource? focusResource,
        Hl7.Fhir.Model.Resource? bodyResource,
        out FhirResponseContext opResponse)
    {
        if (string.IsNullOrEmpty(ctx.SourceContent))
        {
            opResponse = new()
            {
                StatusCode = HttpStatusCode.UnprocessableEntity,
                Outcome = new OperationOutcome()
                {
                    Id = Guid.NewGuid().ToString(),
                    Issue = new List<OperationOutcome.IssueComponent>()
                    {
                        new OperationOutcome.IssueComponent()
                        {
                            Severity = OperationOutcome.IssueSeverity.Fatal,
                            Code = OperationOutcome.IssueType.Structure,
                            Diagnostics = "Body is empty",
                        },
                    },
                }
            };

            return false;
        }

        if (bodyResource == null)
        {
            opResponse = new()
            {
                StatusCode = HttpStatusCode.UnprocessableEntity,
                Outcome = new OperationOutcome()
                {
                    Id = Guid.NewGuid().ToString(),
                    Issue = new List<OperationOutcome.IssueComponent>()
                    {
                        new OperationOutcome.IssueComponent()
                        {
                            Severity = OperationOutcome.IssueSeverity.Fatal,
                            Code = OperationOutcome.IssueType.Structure,
                            Diagnostics = "Content is not parseable as FHIR",
                        },
                    },
                }
            };

            return false;
        }

        opResponse = new()
        {
            StatusCode = HttpStatusCode.OK,
            Outcome = new OperationOutcome()
            {
                Id = Guid.NewGuid().ToString(),
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Success,
                        Code = OperationOutcome.IssueType.Success,
                        Diagnostics = "Content is a structurally-parseable FHIR resource",
                    },
                },
            }
        };

        return true;
    }


    /// <summary>Gets an OperationDefinition for this operation.</summary>
    /// <param name="fhirVersion">The FHIR version.</param>
    /// <returns>The definition.</returns>
    public Hl7.Fhir.Model.OperationDefinition? GetDefinition(
        FhirCandle.Utils.FhirReleases.FhirSequenceCodes fhirVersion)
    {
        Hl7.Fhir.Model.OperationDefinition def = new()
        {
            Id = OperationName.Substring(1) + "-" + OperationVersion.Replace('.', '-'),
            Name = OperationName,
            Url = CanonicalByFhirVersion[fhirVersion],
            Status = Hl7.Fhir.Model.PublicationStatus.Draft,
            Kind = IsNamedQuery ? Hl7.Fhir.Model.OperationDefinition.OperationKind.Query : Hl7.Fhir.Model.OperationDefinition.OperationKind.Operation,
            Code = OperationName.Substring(1),
            Resource = SupportedResources.CopyTargetsNullable(),
            System = AllowSystemLevel,
            Type = AllowResourceLevel,
            Instance = AllowInstanceLevel,
            Parameter = new(),
        };

        def.Parameter.Add(new()
        {
            Name = "return",
            Use = Hl7.Fhir.Model.OperationParameterUse.Out,
            Min = 1,
            Max = "1",
            Type = Hl7.Fhir.Model.FHIRAllTypes.OperationOutcome,
            Documentation = GetReturnDocValue(),
        });

        string GetReturnDocValue() => fhirVersion switch
        {
            Utils.FhirReleases.FhirSequenceCodes.R4 => "An OperationOutcome with information about the submitted data.",
            Utils.FhirReleases.FhirSequenceCodes.R4B => "An OperationOutcome with information about the submitted data.",
            Utils.FhirReleases.FhirSequenceCodes.R5 => "An OperationOutcome with information about the submitted data.",
            _ => string.Empty,
        };

        return def;
    }
}


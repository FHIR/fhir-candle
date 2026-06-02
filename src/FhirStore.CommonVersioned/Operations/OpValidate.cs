// <copyright file="OpValidate.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Extensions;
using FhirCandle.Models;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Validation;
using System.Net;

namespace FhirCandle.Operations;

/// <summary>
/// Structural <c>$validate</c> operation implementation.
/// <para>
/// Accepts either a direct resource payload or a Parameters wrapper containing a
/// <c>resource</c> parameter (and optional <c>mode</c> / <c>profile</c> parameters).
/// Runs Firely's built-in POCO attribute validator and returns an
/// <see cref="Hl7.Fhir.Model.OperationOutcome"/> as the response body.
/// </para>
/// <para>
/// This is a v1 structural validator: it covers required cardinality, primitive
/// types, regex constraints, and FhirXhtml narrative — i.e., everything attribute-
/// driven on the POCO. Profile / binding / slicing validation is out of scope and
/// is tracked as a follow-up in <c>scratch/0601-01/plan.md</c>.
/// </para>
/// </summary>
public class OpValidate : IFhirOperation
{
    /// <summary>Gets the name of the operation.</summary>
    public string OperationName => "$validate";

    /// <summary>Gets the operation version.</summary>
    public string OperationVersion => "0.0.1";

    /// <summary>Gets the canonical by FHIR version.</summary>
    public Dictionary<FhirCandle.Utils.FhirReleases.FhirSequenceCodes, string> CanonicalByFhirVersion => new()
    {
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R4, "http://hl7.org/fhir/OperationDefinition/Resource-validate" },
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R4B, "http://hl7.org/fhir/OperationDefinition/Resource-validate" },
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R5, "http://hl7.org/fhir/OperationDefinition/Resource-validate" },
    };

    /// <summary>Gets a value indicating whether this operation is a named query.</summary>
    public bool IsNamedQuery => false;

    /// <summary>Gets a value indicating whether this operation affects the state of the store.</summary>
    public bool AffectsState => false;

    /// <summary>Gets a value indicating whether we allow get.</summary>
    public bool AllowGet => false;

    /// <summary>Gets a value indicating whether we allow post.</summary>
    public bool AllowPost => true;

    /// <summary>Gets a value indicating whether we allow system level.</summary>
    public bool AllowSystemLevel => true;

    /// <summary>Gets a value indicating whether we allow resource level.</summary>
    public bool AllowResourceLevel => true;

    /// <summary>Gets a value indicating whether we allow instance level.</summary>
    public bool AllowInstanceLevel => true;

    /// <summary>Gets a value indicating whether the accepts non FHIR.</summary>
    public bool AcceptsNonFhir => false;

    /// <summary>Gets a value indicating whether the returns non FHIR.</summary>
    public bool ReturnsNonFhir => false;

    /// <summary>If this operation requires a specific FHIR package to be loaded, the package identifier.</summary>
    public string RequiresPackage => string.Empty;

    /// <summary>Gets the supported resources.</summary>
    public HashSet<string> SupportedResources => [];

    /// <summary>
    /// Cached <see cref="ModelInspector"/> for the version-specific Firely model assembly.
    /// <c>typeof(Patient).Assembly</c> resolves to the concrete version-specific Firely
    /// model DLL (Hl7.Fhir.R4 / R4B / R5) — not Hl7.Fhir.Base — so the inspector reports
    /// the correct FHIR version when validating.
    /// <para>
    /// Initialized via a <see cref="InitInspector"/> helper that swallows the (very
    /// unlikely) initialization failure, logs once, and leaves the field as <c>null</c>.
    /// The <see cref="DoOperation"/> path checks for <c>null</c> and returns a clear
    /// 500 + OperationOutcome rather than throwing a TypeInitializationException that
    /// would render the entire FHIR type unreachable.
    /// </para>
    /// </summary>
    private static readonly ModelInspector? _inspector = InitInspector();

    /// <summary>Wraps the ModelInspector init so a failure cannot bring down the type.</summary>
    private static ModelInspector? InitInspector()
    {
        try
        {
            return ModelInspector.ForAssembly(typeof(Patient).Assembly);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "OpValidate: failed to initialize ModelInspector — $validate will return an Error OperationOutcome. Inner: " + ex.Message);
            return null;
        }
    }

    /// <summary>Executes the $validate operation.</summary>
    /// <param name="ctx">          The context.</param>
    /// <param name="store">        The store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="focusResource">The focus resource (instance-level invocations).</param>
    /// <param name="bodyResource"> The body resource (Parameters wrapper or the resource directly).</param>
    /// <param name="opResponse">   [out] The response.</param>
    /// <returns>True if the operation dispatched cleanly; false on shape errors.</returns>
    public bool DoOperation(
        FhirRequestContext ctx,
        Storage.VersionedFhirStore store,
        Storage.IVersionedResourceStore? resourceStore,
        Resource? focusResource,
        Resource? bodyResource,
        out FhirResponseContext opResponse)
    {
        // Target resolution (H2): body wins for instance-level invocations.
        //   1. Parameters wrapper with a 'resource' parameter  -> that resource
        //   2. Direct (non-Parameters) Resource in the body    -> that resource
        //   3. Otherwise                                        -> the focus (URL instance)
        //   4. Nothing                                          -> shape-error 422
        Resource? target = null;

        if (bodyResource is Parameters p)
        {
            target = ExtractParametersResource(p);
        }
        else if (bodyResource is not null)
        {
            target = bodyResource;
        }

        if (target is null)
        {
            target = focusResource;
        }

        if (target is null)
        {
            OperationOutcome shapeOutcome = new()
            {
                Id = Guid.NewGuid().ToString(),
                Issue =
                [
                    new()
                    {
                        Severity = OperationOutcome.IssueSeverity.Error,
                        Code = OperationOutcome.IssueType.Invalid,
                        Diagnostics = "$validate requires a target resource: either an instance focus (URL), a 'resource' parameter inside a Parameters body, or a resource as the request body.",
                    },
                ],
            };

            opResponse = new()
            {
                StatusCode = HttpStatusCode.UnprocessableEntity,
                Resource = shapeOutcome,
                Outcome = shapeOutcome,
            };
            return false;
        }

        // M7 — guard against a ModelInspector init failure at type load. We choose to
        // surface a clear Error OperationOutcome rather than letting a
        // TypeInitializationException take down every $validate call.
        if (_inspector is null)
        {
            OperationOutcome initOutcome = new()
            {
                Id = Guid.NewGuid().ToString(),
                Issue =
                [
                    new()
                    {
                        Severity = OperationOutcome.IssueSeverity.Error,
                        Code = OperationOutcome.IssueType.Exception,
                        Diagnostics = "$validate is unavailable: model inspector failed to initialize at startup. See server logs.",
                    },
                ],
            };

            opResponse = new()
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Resource = initOutcome,
                Outcome = initOutcome,
            };
            return true;
        }

        // Run structural validation. Per FHIR convention, validation issues
        // are conveyed via OperationOutcome issues, NOT via 4xx status —
        // so we always return 200 once we have a parseable target.
        IReadOnlyCollection<CodedValidationException> issues = target.Validate(
            _inspector,
            NarrativeValidationKind.FhirXhtml,
            validator: null,
            validateRecursively: true);

        OperationOutcome outcome = new()
        {
            Id = Guid.NewGuid().ToString(),
            Issue = [],
        };

        if (issues.Count == 0)
        {
            outcome.Issue.Add(new()
            {
                Severity = OperationOutcome.IssueSeverity.Information,
                Code = OperationOutcome.IssueType.Informational,
                Diagnostics = "All OK",
            });
        }
        else
        {
            foreach (CodedValidationException vex in issues)
            {
                OperationOutcome.IssueComponent issue = new()
                {
                    Severity = vex.IssueSeverity,
                    Code = vex.IssueType,
                    Diagnostics = vex.BaseErrorMessage ?? vex.Message,
                };

                string? expression = !string.IsNullOrEmpty(vex.InstancePath)
                    ? vex.InstancePath
                    : (!string.IsNullOrEmpty(vex.MemberName) ? vex.MemberName : null);

                if (!string.IsNullOrEmpty(expression))
                {
                    issue.Expression = [expression];
                }

                outcome.Issue.Add(issue);
            }
        }

        // M1 — characterize 'mode' and 'profile' parameters from a Parameters wrapper.
        // Both are accepted by the operation definition but ignored by this
        // implementation; surface that as Information issues so callers can see it
        // without having to read the OperationDefinition.
        if (bodyResource is Parameters paramsBody && paramsBody.Parameter is not null)
        {
            foreach (Parameters.ParameterComponent pc in paramsBody.Parameter)
            {
                if (string.Equals(pc.Name, "mode", StringComparison.Ordinal) ||
                    string.Equals(pc.Name, "profile", StringComparison.Ordinal))
                {
                    outcome.Issue.Add(new()
                    {
                        Severity = OperationOutcome.IssueSeverity.Information,
                        Code = OperationOutcome.IssueType.Informational,
                        Diagnostics = $"Parameter '{pc.Name}' is currently ignored by this $validate implementation.",
                    });
                }
            }
        }

        opResponse = new()
        {
            StatusCode = HttpStatusCode.OK,
            Resource = outcome,
            Outcome = outcome,
            ResourceType = outcome.TypeName,
            Id = outcome.Id ?? string.Empty,
        };
        return true;
    }

    /// <summary>
    /// Extracts the embedded resource from the 'resource' parameter inside a Parameters wrapper.
    /// </summary>
    /// <param name="parameters">The Parameters instance.</param>
    /// <returns>The embedded resource, or null if not present.</returns>
    private static Resource? ExtractParametersResource(Parameters parameters)
    {
        if (parameters.Parameter is null)
        {
            return null;
        }

        foreach (Parameters.ParameterComponent pc in parameters.Parameter)
        {
            if (string.Equals(pc.Name, "resource", StringComparison.Ordinal) && pc.Resource is not null)
            {
                return pc.Resource;
            }
        }

        return null;
    }

    /// <summary>Gets an OperationDefinition for this operation.</summary>
    /// <param name="fhirVersion">The FHIR version.</param>
    /// <returns>The definition.</returns>
    public OperationDefinition? GetDefinition(
        FhirCandle.Utils.FhirReleases.FhirSequenceCodes fhirVersion)
    {
        OperationDefinition def = new()
        {
            Id = OperationName.Substring(1) + "-" + OperationVersion.Replace('.', '-'),
            Name = OperationName,
            Url = CanonicalByFhirVersion[fhirVersion],
            Status = PublicationStatus.Draft,
            Kind = IsNamedQuery ? OperationDefinition.OperationKind.Query : OperationDefinition.OperationKind.Operation,
            AffectsState = AffectsState,
            Code = OperationName.Substring(1),
            Resource = SupportedResources.CopyTargetsNullable(),
            System = AllowSystemLevel,
            Type = AllowResourceLevel,
            Instance = AllowInstanceLevel,
            Parameter = [],
        };

        def.Parameter.Add(new()
        {
            Name = "resource",
            Use = OperationParameterUse.In,
            Min = 0,
            Max = "1",
            Type = FHIRAllTypes.Resource,
            Documentation = "The resource to validate. May be omitted at instance level (the focus is used instead).",
        });

        def.Parameter.Add(new()
        {
            Name = "mode",
            Use = OperationParameterUse.In,
            Min = 0,
            Max = "1",
            Type = FHIRAllTypes.Code,
            Documentation = "Validation mode hint (create, update, delete, ...). Currently ignored by this implementation.",
        });

        def.Parameter.Add(new()
        {
            Name = "profile",
            Use = OperationParameterUse.In,
            Min = 0,
            Max = "*",
            Type = FHIRAllTypes.Canonical,
            Documentation = "Optional profile canonical URLs the resource should be validated against. Currently ignored.",
        });

        def.Parameter.Add(new()
        {
            Name = "return",
            Use = OperationParameterUse.Out,
            Min = 1,
            Max = "1",
            Type = FHIRAllTypes.OperationOutcome,
            Documentation = "The OperationOutcome describing validation issues. An issue with severity 'information' indicates a successful validation.",
        });

        return def;
    }
}

using FhirCandle.Extensions;
using FhirCandle.Models;
using FhirCandle.Operations;
using FhirCandle.Storage;
using Hl7.Fhir.Model;

namespace FhirCandle.R4B.Operations;

public class OpResetStore : IFhirOperation
{
        /// <summary>Gets the name of the operation.</summary>
    public string OperationName => "$reset-store";

    /// <summary>Gets the operation version.</summary>
    public string OperationVersion => "0.0.1";

    /// <summary>Gets the canonical by FHIR version.</summary>
    public Dictionary<FhirCandle.Utils.FhirReleases.FhirSequenceCodes, string> CanonicalByFhirVersion => new()
    {
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R4, "http://ginoc.io/fhir/OperationDefinition/reset-store" },
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R4B, "http://ginoc.io/fhir/OperationDefinition/reset-store" },
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R5, "http://ginoc.io/fhir/OperationDefinition/reset-store" },
    };

    /// <summary>Gets a value indicating whether this operation is a named query.</summary>
    public bool IsNamedQuery => false;

    /// <summary>Gets a value indicating whether this operation affects the state of the store.</summary>
    public bool AffectsState => true;

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
    public bool AcceptsNonFhir => false;

    /// <summary>Gets a value indicating whether the returns non FHIR.</summary>
    public bool ReturnsNonFhir => false;

    /// <summary>
    /// If this operation requires a specific FHIR package to be loaded, the package identifier.
    /// </summary>
    public string RequiresPackage => string.Empty;

    /// <summary>Gets the supported resources.</summary>
    public HashSet<string> SupportedResources => [];

    private readonly HashSet<string> _excludedParams = [];

    /// <summary>Executes the Reset Store operation, deleting all non-protected resources.</summary>
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
        // split the url query
        System.Collections.Specialized.NameValueCollection queryParams = System.Web.HttpUtility.ParseQueryString(ctx.UrlQuery);
        string[] stringValues = queryParams.GetValues("keep-conformance") ?? [];

        bool keepConformance = stringValues.Any(value => bool.TryParse(value, out bool result) && result);

        // check for feature request parameters
        if (bodyResource is Hl7.Fhir.Model.Parameters requestParameters)
        {
            foreach (Hl7.Fhir.Model.Parameters.ParameterComponent pc in requestParameters.Parameter)
            {
                if (pc.Name == "keep-conformance")
                {
                    keepConformance = pc.Value is Hl7.Fhir.Model.FhirBoolean booleanValue && (booleanValue.Value == true);
                }
            }
        }

        store.ResetStore(keepConformance);
        
        opResponse = new()
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Outcome = new OperationOutcome()
            {
                Id = Guid.NewGuid().ToString(),
                Issue =
                [
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Success,
                        Code = OperationOutcome.IssueType.Success,
                        Diagnostics = keepConformance
                            ? "All non-protected and non-conformance resources have been removed."
                            : "All non-protected resources have been removed.",
                    }
                ],
            }
        };

        return true;
    }

    public Hl7.Fhir.Model.OperationDefinition? GetDefinition(
        FhirCandle.Utils.FhirReleases.FhirSequenceCodes fhirVersion)
    {
        return new()
        {
            Id = OperationName.Substring(1) + "-" + OperationVersion.Replace('.', '-'),
            Name = OperationName,
            Url = CanonicalByFhirVersion[fhirVersion],
            Status = Hl7.Fhir.Model.PublicationStatus.Draft,
            Kind = IsNamedQuery ? Hl7.Fhir.Model.OperationDefinition.OperationKind.Query : Hl7.Fhir.Model.OperationDefinition.OperationKind.Operation,
            AffectsState = AffectsState,
            Code = OperationName.Substring(1),
            Resource = SupportedResources.CopyTargetsNullable(),
            System = AllowSystemLevel,
            Type = AllowResourceLevel,
            Instance = AllowInstanceLevel,
            Parameter =
            [
                new()
                {
                    Name = "keep-conformance",
                    Use = Hl7.Fhir.Model.OperationParameterUse.In,
                    Min = 0,
                    Max = "1",
                    Type = Hl7.Fhir.Model.FHIRAllTypes.Boolean,
                    Documentation = "True to keep conformance resources, false to delete them. Default is false (delete).",
                },
                new()
                {
                    Name = "return",
                    Use = Hl7.Fhir.Model.OperationParameterUse.Out,
                    Min = 1,
                    Max = "1",
                    Type = Hl7.Fhir.Model.FHIRAllTypes.OperationOutcome,
                    Documentation = "An OperationOutcome resource with the results of the request.",
                }
            ],
        };
    }
}

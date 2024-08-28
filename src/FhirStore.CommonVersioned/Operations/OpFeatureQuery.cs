using FhirCandle.Extensions;
using FhirCandle.Models;
using FhirCandle.Operations;
using FhirCandle.Storage;
using Hl7.Fhir.Model;

namespace FhirCandle.R4B.Operations;

public class OpFeatureQuery : IFhirOperation
{
        /// <summary>Gets the name of the operation.</summary>
    public string OperationName => "$feature-query";

    /// <summary>Gets the operation version.</summary>
    public string OperationVersion => "0.0.1";

    /// <summary>Gets the canonical by FHIR version.</summary>
    public Dictionary<FhirCandle.Utils.FhirReleases.FhirSequenceCodes, string> CanonicalByFhirVersion => new()
    {
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R4, "http://www.hl7.org/fhir/uv/capstmt/OperationDefinition/feature-query" },
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R4B, "http://www.hl7.org/fhir/uv/capstmt/OperationDefinition/feature-query" },
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R5, "http://www.hl7.org/fhir/uv/capstmt/OperationDefinition/feature-query" },
    };

    /// <summary>Gets a value indicating whether this operation is a named query.</summary>
    public bool IsNamedQuery => false;

    /// <summary>Gets a value indicating whether we allow get.</summary>
    public bool AllowGet => true;

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

    private readonly HashSet<string> _excludedParams =
    [
        "_format",
    ];

    /// <summary>Executes the Subscription/$events operation.</summary>
    /// <param name="ctx">          The context.</param>
    /// <param name="store">        The store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="focusResource">The focus resource.</param>
    /// <param name="bodyResource"> The body resource.</param>
    /// <param name="opResponse">   [out] The response resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    /// <remarks>
    /// Input format (HTTP Query Parameter):
    ///     $feature-query?param=feature[@context][(value)]
    /// Input format (Parameters)
    ///     ...
    /// General patterns:
    ///     feature alone: returns list of values on the server (can refuse - see processing-status)
    ///         note: we cannot represent an empty list here, so if there are no values clients must check
    ///         the processing-status to see if the feature is supported
    ///     feature + context: returns list of values in that context on the server
    ///     feature + value: returns answer of true/false if all contexts match the supplied value
    ///     feature + context + value: returns answer of true/false if the supplied context matches the supplied value
    /// Responses
    ///     feature: 'feature' literal (one repetition per request feature param)
    ///         name: name of the feature (uri)
    ///         context: present if provided, used to match responses to requests (uri)
    ///         processing-status: code from the server about processing the request (e.g., all-ok, not-supported, etc.)
    ///         value:
    ///             if provided in input: the value requested (datatype as defined by the feature) (even if processing fails)
    ///             if not provided: the value of the feature (can have multiple repetitions) (uses datatype of feature)
    ///         answer: (boolean)
    ///             only present if processing was successful (all-ok)
    ///             if a value is provided, does the supplied value match the server feature-supported value
    ///             if a value is not provided, NOT PRESENT
    /// </remarks>
    public bool DoOperation(
        FhirRequestContext ctx,
        Storage.VersionedFhirStore store,
        Storage.IVersionedResourceStore? resourceStore,
        Hl7.Fhir.Model.Resource? focusResource,
        Hl7.Fhir.Model.Resource? bodyResource,
        out FhirResponseContext opResponse)
    {
        Console.Write("");
        
        // split the url query
        System.Collections.Specialized.NameValueCollection queryParams = System.Web.HttpUtility.ParseQueryString(ctx.UrlQuery);
        string[] paramValues = queryParams.GetValues("param") ?? [];
        
        // check for feature requests as http parameters
        List<FeatureRequestRecord?> featureRequests = paramValues
            .Where(p => !_excludedParams.Contains(p))
            .Select(ParseFeatureRequestParam)
            .Where(r => r != null)
            .ToList() ?? [];
        
        // check for feature request parameters
        if (bodyResource is Hl7.Fhir.Model.Parameters requestParameters)
        {
            foreach (Hl7.Fhir.Model.Parameters.ParameterComponent pc in requestParameters.Parameter)
            {
                if (pc.Name == "feature")
                {
                    featureRequests.Add(new ()
                    {
                        Context = pc.Part.FirstOrDefault(p => p.Name == "context")?.Value?.ToString() ?? string.Empty,
                        Feature = pc.Part.FirstOrDefault(p => p.Name == "feature")?.Value?.ToString() ?? string.Empty,
                        RawValue = pc.Part.FirstOrDefault(p => p.Name == "value")?.Value?.ToString() ?? string.Empty,
                        Value = pc.Part.FirstOrDefault(p => p.Name == "value")?.Value,
                    });
                }
            }
        }

        Parameters testResultsParam = new();
        
        // ask the server to test the features
        foreach (FeatureRequestRecord? req in featureRequests)
        {
            if (req == null)
            {
                continue;
            }

            _ = store.TryQueryCapabilityFeature(req.Feature, req.Context, req.Value, req.RawValue, out VersionedFhirStore.FeatureQueryResponse fqr);

            List<Parameters.ParameterComponent> parts = [
                new Parameters.ParameterComponent()
                {
                    Name = "name",
                    Value = new FhirUri(req.Feature),
                },
            ];
            
            if (!string.IsNullOrEmpty(req.Context))
            {
                parts.Add(new Parameters.ParameterComponent()
                {
                    Name = "context",
                    Value = new FhirUri(req.Context),
                });
            }
            
            if (fqr.Value.Count != 0)
            {
                parts.AddRange(fqr.Value.Select(v => new Parameters.ParameterComponent()
                {
                    Name = "value",
                    Value = v,
                }));
            }
            else if (req.Value != null)
            {
                parts.Add(new Parameters.ParameterComponent()
                {
                    Name = "value",
                    Value = req.Value,
                });
            }
            else if (!string.IsNullOrEmpty(req.RawValue))
            {
                parts.Add(new Parameters.ParameterComponent()
                {
                    Name = "value",
                    Value = new FhirString(req.RawValue),
                });
            }
            
            if (fqr.Matches != null)
            {
                parts.Add(new Parameters.ParameterComponent()
                {
                    Name = "matches",
                    Value = new FhirBoolean(fqr.Matches),
                });
            }
            
            parts.Add(new Parameters.ParameterComponent()
            {
                Name = "processing-status",
                Value = new Code(fqr.ProcessingStatus)
            });
            
            testResultsParam.Parameter.Add(new Parameters.ParameterComponent()
            {
                Name = "feature",
                Part = parts,
            });
        }
        
        opResponse = new()
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Resource = testResultsParam,
            Outcome = new OperationOutcome()
            {
                Id = Guid.NewGuid().ToString(),
                Issue =
                [
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Success,
                        Code = OperationOutcome.IssueType.Success,
                        Diagnostics = "Feature request query has been processed.",
                    }
                ],
            }
        };

        return true;
    }

    private FeatureRequestRecord? ParseFeatureRequestParam(string? param)
    {
        if (string.IsNullOrEmpty(param))
        {
            return null;
        }

        string value;
        string buffer = param;
        
        // parse the string: ?param=feature[@context][(value)]. feature is required - context is optional, delimited by @ - value is optional, delimited by opening and closing parens
        if (param.EndsWith(')'))
        {
            int startLoc = param.LastIndexOf('(');
            value = param[(startLoc + 1)..^1];
            buffer = buffer[..(startLoc)];
        }
        else
        {
            value = string.Empty;
        }

        int contextSepLoc = param.IndexOf('@');
        string feature = contextSepLoc != -1 ? buffer[..contextSepLoc] : buffer;
        
        string context = contextSepLoc != -1 ? buffer[(contextSepLoc + 1)..] : string.Empty;
        
        return new FeatureRequestRecord
        {
            Feature = feature,
            Context = context,
            RawValue = value,
            Value = null,
        };
    }
    
    private record class FeatureRequestRecord
    {
        public required string Feature { get; init; }
        public required string Context { get; init; }
        public required DataType? Value { get; init; }
        public required string RawValue { get; init; }
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
            Code = OperationName.Substring(1),
            Resource = SupportedResources.CopyTargetsNullable(),
            System = AllowSystemLevel,
            Type = AllowResourceLevel,
            Instance = AllowInstanceLevel,
            Parameter =
            [
                new()
                {
                    Name = "param",
                    Use = Hl7.Fhir.Model.OperationParameterUse.In,
                    Min = 0,
                    Max = "*",
                    Type = Hl7.Fhir.Model.FHIRAllTypes.String,
                    Documentation = "A string in the format of '{feature}(@{context}):value'.",
                },

                new()
                {
                    Name = "feature",
                    Use = Hl7.Fhir.Model.OperationParameterUse.In,
                    Min = 0,
                    Max = "*",
                    Type = Hl7.Fhir.Model.FHIRAllTypes.String,
                    Documentation = "A complex parameter include parts for feature, context, and value.",
                },

                new()
                {
                    Name = "return",
                    Use = Hl7.Fhir.Model.OperationParameterUse.Out,
                    Min = 1,
                    Max = "1",
                    Type = Hl7.Fhir.Model.FHIRAllTypes.Parameters,
                    Documentation = "A parameters resource with details about support for the requested feature.",
                }


            ],
        };
    }
}

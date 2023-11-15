// <copyright file="OpPasClaimInquiry.cs" company="Microsoft Corporation">
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

/// <summary>This operation is used to make an inquiry for a previously-submitted Pre-Authorization.</summary>
public class OpPasClaimInquiry : IFhirOperation
{
    /// <summary>Gets the name of the operation.</summary>
    public string OperationName => "$inquire";

    /// <summary>Gets the operation version.</summary>
    public string OperationVersion => "1.2.0";

    /// <summary>Gets the canonical by FHIR version.</summary>
    public Dictionary<FhirCandle.Models.TenantConfiguration.SupportedFhirVersions, string> CanonicalByFhirVersion => new()
    {
        { FhirCandle.Models.TenantConfiguration.SupportedFhirVersions.R4, "http://hl7.org/fhir/us/davinci-pas/OperationDefinition/Claim-inquiry" },
    };

    /// <summary>Gets a value indicating whether this operation is a named query.</summary>
    public bool IsNamedQuery => false;

    /// <summary>Gets a value indicating whether we allow get.</summary>
    public bool AllowGet => false;

    /// <summary>Gets a value indicating whether we allow post.</summary>
    public bool AllowPost => true;

    /// <summary>Gets a value indicating whether we allow system level.</summary>
    public bool AllowSystemLevel => false;

    /// <summary>Gets a value indicating whether we allow resource level.</summary>
    public bool AllowResourceLevel => true;

    /// <summary>Gets a value indicating whether we allow instance level.</summary>
    public bool AllowInstanceLevel => false;

    /// <summary>Gets a value indicating whether the accepts non FHIR.</summary>
    public bool AcceptsNonFhir => true;

    /// <summary>Gets a value indicating whether the returns non FHIR.</summary>
    public bool ReturnsNonFhir => false;

    /// <summary>
    /// If this operation requires a specific FHIR package to be loaded, the package identifier.
    /// </summary>
    public string RequiresPackage => "hl7.fhir.us.davinci-pas";

    /// <summary>Gets the supported resources.</summary>
    public HashSet<string> SupportedResources => new()
    {
        "Claim"
    };

    /// <summary>Executes the Subscription/$events operation.</summary>
    /// <param name="ctx">             The authentication.</param>
    /// <param name="store">           The store.</param>
    /// <param name="resourceType">    Type of the resource.</param>
    /// <param name="resourceStore">   The resource store.</param>
    /// <param name="instanceId">      Identifier for the instance.</param>
    /// <param name="focusResource">   The focus resource.</param>
    /// <param name="queryString">     The query string.</param>
    /// <param name="bodyResource">    The body resource.</param>
    /// <param name="bodyContent">     The original body content.</param>
    /// <param name="contentType">     Type of the content.</param>
    /// <param name="responseResource">[out] The response resource.</param>
    /// <param name="responseOutcome"> [out] The response outcome.</param>
    /// <param name="contentLocation"> [out] The content location.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode DoOperation(
        FhirRequestContext ctx,
        Storage.VersionedFhirStore store,
        string resourceType,
        Storage.IVersionedResourceStore? resourceStore,
        string instanceId,
        Hl7.Fhir.Model.Resource? focusResource,
        string queryString,
        Hl7.Fhir.Model.Resource? bodyResource,
        string bodyContent,
        string contentType,
        out Hl7.Fhir.Model.Resource? responseResource,
        out Hl7.Fhir.Model.OperationOutcome? responseOutcome,
        out string contentLocation)
    {
        if ((bodyResource == null) ||
            (bodyResource is not Bundle b))
        {
            responseResource = null;
            responseOutcome = new OperationOutcome()
            {
                Id = Guid.NewGuid().ToString(),
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Fatal,
                        Code = OperationOutcome.IssueType.Structure,
                        Diagnostics = "PAS Claim Inquiry requires a PASClaimInquiryBundle as input.",
                    },
                },
            };
            contentLocation = string.Empty;

            return HttpStatusCode.UnprocessableEntity;
        }

        if (b.Type != Bundle.BundleType.Collection)
        {
            responseResource = null;
            responseOutcome = new OperationOutcome()
            {
                Id = Guid.NewGuid().ToString(),
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Fatal,
                        Code = OperationOutcome.IssueType.Structure,
                        Diagnostics = "PAS Claim Inquiry PASClaimInquiryBundle SHALL be a `collection`.",
                    },
                },
            };
            contentLocation = string.Empty;

            return HttpStatusCode.UnprocessableEntity;
        }

        IEnumerable<Resource> claims = b.Entry.Select(e => e.Resource).Where(r => r is Claim);

        if (!claims.Any())
        {
            responseResource = null;
            responseOutcome = new OperationOutcome()
            {
                Id = Guid.NewGuid().ToString(),
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Fatal,
                        Code = OperationOutcome.IssueType.Structure,
                        Diagnostics = "Submitted bundle does not contain any Claim resources.",
                    },
                },
            };
            contentLocation = string.Empty;

            return HttpStatusCode.UnprocessableEntity;
        }

        Bundle response = new()
        {
            Id = Guid.NewGuid().ToString(),
            Meta = new()
            {
                LastUpdated = DateTimeOffset.UtcNow,
            },
            Type = Bundle.BundleType.Collection,
            Timestamp = DateTimeOffset.UtcNow,
            Entry = new List<Bundle.EntryComponent>(),
        };

        foreach (Claim c in claims)
        {
            ClaimResponse cr = new()
            {
                Id = Guid.NewGuid().ToString(),
                Meta = new()
                {
                    LastUpdated = DateTimeOffset.UtcNow,
                },
                Identifier = c.Identifier.Select(i => i).ToList(),
                Status = c.Status,
                Type = c.Type,
                Use = c.Use,
                Patient = c.Patient,
                Created = c.Created,
                Insurer = c.Insurer,
                Requestor = c.Provider,
                Request = new ResourceReference()
                {
                    Reference = $"Claim/{c.Id}",
                },
                Insurance = c.Insurance.Select(i => new ClaimResponse.InsuranceComponent()
                {
                    Sequence = i.Sequence,
                    Focal = i.Focal,
                    Coverage = i.Coverage,
                    Extension = i.Extension.Select(e => e).ToList(),
                    ElementId = i.ElementId,
                }).ToList(),
                Outcome = ClaimProcessingCodes.Complete,
                Item = c.Item.Select(i => new ClaimResponse.ItemComponent()
                {
                    ItemSequence = i.Sequence,
                    Extension = i.Extension.Select(e => e).ToList(),
                    ElementId = i.ElementId,
                    Adjudication = new()
                    {
                        new ClaimResponse.AdjudicationComponent()
                        {
                            Category = new("http://terminology.hl7.org/CodeSystem/adjudication", "submitted"),
                            Extension = new()
                            {
                               new Extension()
                               {
                                   Url = "http://hl7.org/fhir/us/davinci-pas/StructureDefinition/extension-reviewAction",
                                   Extension = new()
                                   {
                                       new()
                                       {
                                           Url = "number",
                                           Value = new FhirString($"AUTH{i.Sequence:0000}"),
                                       },
                                       new()
                                       {
                                           Url = "http://hl7.org/fhir/us/davinci-pas/StructureDefinition/extension-reviewActionCode",
                                           Value = new CodeableConcept("https://codesystem.x12.org/005010/306", "A1", "Certified in total"),
                                       }
                                   },
                               },
                            },
                        },
                    },
                    Detail = i.Detail.Select(d => new ClaimResponse.ItemDetailComponent()
                    {
                        DetailSequence = d.Sequence,
                        Extension = d.Extension.Select(e => e).ToList(),
                        ElementId = d.ElementId,
                        SubDetail = d.SubDetail.Select(sd => new ClaimResponse.SubDetailComponent()
                        {
                            SubDetailSequence = sd.Sequence,
                            Extension = sd.Extension.Select(e => e).ToList(),
                            ElementId = sd.ElementId,
                        }).ToList(),
                    }).ToList(),
                }).ToList(),
            };

            response.Entry.Add(new Bundle.EntryComponent()
            {
                FullUrl = $"urn:uuid:{cr.Id}",
                Resource = cr,
            });
        }

        responseResource = response;
        responseOutcome = new OperationOutcome()
        {
            Id = Guid.NewGuid().ToString(),
            Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Success,
                        Code = OperationOutcome.IssueType.Success,
                        Diagnostics = "See response bundle for details.",
                    },
                },
        };
        contentLocation = string.Empty;

        return HttpStatusCode.OK;
    }


    /// <summary>Gets an OperationDefinition for this operation.</summary>
    /// <param name="fhirVersion">The FHIR version.</param>
    /// <returns>The definition.</returns>
    public Hl7.Fhir.Model.OperationDefinition? GetDefinition(
        FhirCandle.Models.TenantConfiguration.SupportedFhirVersions fhirVersion)
    {
        // operation has canonical definition in package
        return null;
    }
}


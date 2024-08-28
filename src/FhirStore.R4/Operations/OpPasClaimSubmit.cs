// <copyright file="OpPasClaimSubmit.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FhirCandle.Operations;

/// <summary>
/// This operation is used to submit a Pre-Authorization Claim Request for adjudication as a
/// Bundle containing the PASClaimRequest and other referenced resources for processing.
/// </summary>
public class OpPasClaimSubmit : IFhirOperation
{
    /// <summary>Gets the name of the operation.</summary>
    public string OperationName => "$submit";

    /// <summary>Gets the operation version.</summary>
    public string OperationVersion => "1.2.0";

    /// <summary>Gets the canonical by FHIR version.</summary>
    public Dictionary<FhirCandle.Utils.FhirReleases.FhirSequenceCodes, string> CanonicalByFhirVersion => new()
    {
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R4, "http://hl7.org/fhir/us/davinci-pas/OperationDefinition/Claim-submit" },
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
    /// <param name="ctx">          The authentication.</param>
    /// <param name="store">        The store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="focusResource">The focus resource.</param>
    /// <param name="bodyResource"> The body resource.</param>
    /// <param name="opResponse">   [out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool DoOperation(
        FhirRequestContext ctx,
        Storage.VersionedFhirStore store,
        Storage.IVersionedResourceStore? resourceStore,
        Hl7.Fhir.Model.Resource? focusResource,
        Hl7.Fhir.Model.Resource? bodyResource,
        out FhirResponseContext opResponse)
    {
        if ((bodyResource == null) ||
            (bodyResource is not Bundle cb))
        {
            opResponse = new FhirResponseContext()
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
                            Diagnostics = "PAS Claim Submit requires a PASRequestBundle as input.",
                        },
                    },
                },
            };

            return false;
        }

        if (cb.Type != Bundle.BundleType.Collection)
        {
            opResponse = new FhirResponseContext()
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
                            Diagnostics = "PAS Claim Submit PASRequestBundle SHALL be a `collection`.",
                        },
                    },
                },
            };

            return false;
        }

        IEnumerable<Resource> claims = cb.Entry.Select(e => e.Resource).Where(r => r is Claim);

        if (!claims.Any())
        {
            opResponse = new FhirResponseContext()
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
                            Diagnostics = "Submitted bundle does not contain any Claim resources.",
                        },
                    },
                },
            };

            return false;
        }

        OperationOutcome responseOutcome = new OperationOutcome()
        {
            Id = Guid.NewGuid().ToString(),
            Issue = new List<OperationOutcome.IssueComponent>(),
        };

        // ensure that the first entry is a claim
        if (!(cb.Entry[0].Resource is Claim c))
        {
            responseOutcome.Issue.Add(new OperationOutcome.IssueComponent()
            {
                Severity = OperationOutcome.IssueSeverity.Error,
                Code = OperationOutcome.IssueType.BusinessRule,
                Diagnostics = $"First entry in bundle is not a Claim.",
            });

            opResponse = new FhirResponseContext()
            {
                StatusCode = HttpStatusCode.BadRequest,
                Outcome = responseOutcome,
            };

            return false;
        }

        if (!c.Identifier.Any())
        {
            responseOutcome.Issue.Add(new OperationOutcome.IssueComponent()
            {
                Severity = OperationOutcome.IssueSeverity.Error,
                Code = OperationOutcome.IssueType.Required,
                Diagnostics = $"Claim {c.Id} is missing mandatory `identifier` element.",
            });

            opResponse = new FhirResponseContext()
            {
                StatusCode = HttpStatusCode.BadRequest,
                Outcome = responseOutcome,
            };

            return false;
        }

        if (c.Provider == null)
        {
            responseOutcome.Issue.Add(new OperationOutcome.IssueComponent()
            {
                Severity = OperationOutcome.IssueSeverity.Error,
                Code = OperationOutcome.IssueType.Required,
                Diagnostics = $"Claim {c.Id} is missing mandatory `provider` element.",
            });

            opResponse = new FhirResponseContext()
            {
                StatusCode = HttpStatusCode.BadRequest,
                Outcome = responseOutcome,
            };

            return false;
        }

        if (!c.Insurance.Any())
        {
            responseOutcome.Issue.Add(new OperationOutcome.IssueComponent()
            {
                Severity = OperationOutcome.IssueSeverity.Error,
                Code = OperationOutcome.IssueType.Required,
                Diagnostics = $"Claim {c.Id} is missing mandatory `insurance` element.",
            });

            opResponse = new FhirResponseContext()
            {
                StatusCode = HttpStatusCode.BadRequest,
                Outcome = responseOutcome,
            };

            return false;
        }

        if (!c.Item.Any())
        {
            responseOutcome.Issue.Add(new OperationOutcome.IssueComponent()
            {
                Severity = OperationOutcome.IssueSeverity.Error,
                Code = OperationOutcome.IssueType.Required,
                Diagnostics = $"Claim {c.Id} is missing mandatory `item` element.",
            });

            opResponse = new FhirResponseContext()
            {
                StatusCode = HttpStatusCode.BadRequest,
                Outcome = responseOutcome,
            };

            return false;
        }

        // store the bundle
        //if (!store.TryInstanceCreate(cb, true, out _, out string id))
        if (!store.InstanceCreate(new FhirRequestContext(store, "POST", "Bundle", cb), out _))
        {
            responseOutcome.Issue.Add(new OperationOutcome.IssueComponent()
            {
                Severity = OperationOutcome.IssueSeverity.Error,
                Code = OperationOutcome.IssueType.Exception,
                Diagnostics = $"Failed to store claim request bundle.",
            });

            opResponse = new FhirResponseContext()
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Outcome = responseOutcome,
            };

            return false;
        }

        // build a claim response
        ClaimResponse cr = new()
        {
            Id = Guid.NewGuid().ToString(),
            Meta = new()
            {
                LastUpdated = DateTimeOffset.UtcNow,
            },
            Identifier = new List<Identifier>()
            {
                new Identifier()
                {
                    System = "http://hl7.org/fhir/us/davinci-pas/ClaimResponse",
                    Value = Guid.NewGuid().ToString(),
                },
            },
            Status = FinancialResourceStatusCodes.Active,
            Type = new CodeableConcept()
            {
                Coding = new List<Coding>()
                {
                    new Coding()
                    {
                        System = "http://terminology.hl7.org/CodeSystem/claim-type",
                        Code = "professional",
                    },
                },
            },
            Use = ClaimUseCode.Preauthorization,
            Patient = c.Patient,
            Created = DateTime.UtcNow.ToString("o"),
            Insurer = c.Insurer,
            Request = new ResourceReference()
            {
                Reference = $"Claim/{c.Id}",
            },
            Outcome = ClaimProcessingCodes.Queued,
            Disposition = "Claim accepted.",
            // Item = new List<ClaimResponse.ItemComponent>(),
            Item = c.Item.Select(ci => new ClaimResponse.ItemComponent()
            {
                ItemSequence = ci.Sequence,
                NoteNumber = new List<int?>() { 1 },
                Adjudication = new List<ClaimResponse.AdjudicationComponent>()
                {
                    new ClaimResponse.AdjudicationComponent()
                    {
                        Category = new CodeableConcept()
                        {
                            Coding = new List<Coding>()
                            {
                                new Coding()
                                {
                                    System = "http://terminology.hl7.org/CodeSystem/adjudication",
                                    Code = "submitted",
                                },
                            },
                        },
                    },
                },
            }).ToList(),
        };

        // store the claim response locally
        if (!store.InstanceCreate(new FhirRequestContext(store, "POST", "ClaimResponse", cr), out _))
        {
            responseOutcome.Issue.Add(new OperationOutcome.IssueComponent()
            {
                Severity = OperationOutcome.IssueSeverity.Error,
                Code = OperationOutcome.IssueType.Exception,
                Diagnostics = $"Failed to store claim response.",
            });

            opResponse = new FhirResponseContext()
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Outcome = responseOutcome,
            };

            return false;
        }

        // build a claim response bundle
        Bundle crb = new()
        {
            Id = Guid.NewGuid().ToString(),
            Meta = new()
            {
                LastUpdated = DateTimeOffset.UtcNow,
            },
            Identifier = cb.Identifier,
            Type = Bundle.BundleType.Collection,
            Timestamp = DateTimeOffset.UtcNow,
            Entry = new List<Bundle.EntryComponent>()
            {
                new Bundle.EntryComponent()
                {
                    FullUrl = $"ClaimResponse/{cr.Id}",
                    Resource = cr,
                    // Response = new Bundle.ResponseComponent()
                    // {
                    //     Status = HttpStatusCode.Created.ToString(),
                    //     Etag = Guid.NewGuid().ToString(),
                    //     LastModified = cr.Meta.LastUpdated,
                    //     Location = $"ClaimResponse/{cr.Id}",
                    // },
                },
            },
        };

        // copy items from the claim request bundle to the response bundle
        foreach (Bundle.EntryComponent e in cb.Entry)
        {
            if (e.Resource == null)
            {
                continue;
            }

            // skip all resources except for organization, patient, and coverage
            if ((e.Resource is not Organization) &&
                (e.Resource is not Patient) &&
                (e.Resource is not Coverage))
            {
                continue;
            }

            crb.Entry.Add(new Bundle.EntryComponent()
            {
                FullUrl = e.FullUrl,
                Resource = e.Resource,
                // Response = new Bundle.ResponseComponent()
                // {
                //     Status = HttpStatusCode.Created.ToString(),
                //     Etag = Guid.NewGuid().ToString(),
                //     LastModified = e.Resource?.Meta?.LastUpdated ?? null,
                //     Location = e.FullUrl,
                // },
            });
        }

        // store the claim response bundle
        if (!store.InstanceCreate(new FhirRequestContext(store, "POST", "Bundle", crb), out _))
        {
            responseOutcome.Issue.Add(new OperationOutcome.IssueComponent()
            {
                Severity = OperationOutcome.IssueSeverity.Error,
                Code = OperationOutcome.IssueType.Exception,
                Diagnostics = $"Failed to store claim response bundle.",
            });

            opResponse = new FhirResponseContext()
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Outcome = responseOutcome,
            };

            return false;
        }

        responseOutcome.Issue.Add(new OperationOutcome.IssueComponent()
        {
            Severity = OperationOutcome.IssueSeverity.Success,
            Code = OperationOutcome.IssueType.Informational,
            Diagnostics = $"Claim request has been accepted and a claim response bundle stored.",
        });

        opResponse = new FhirResponseContext()
        {
            StatusCode = HttpStatusCode.OK,
            Resource = crb,
            Outcome = responseOutcome,
        };

        return true;
    }


    /// <summary>Gets an OperationDefinition for this operation.</summary>
    /// <param name="fhirVersion">The FHIR version.</param>
    /// <returns>The definition.</returns>
    public Hl7.Fhir.Model.OperationDefinition? GetDefinition(
        FhirCandle.Utils.FhirReleases.FhirSequenceCodes fhirVersion)
    {
        // operation has canonical definition in package
        return null;
    }
}


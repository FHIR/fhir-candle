// <copyright file="CDexTaskProcess.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>


using FhirCandle.Interactions;
using FhirCandle.Models;
using FhirCandle.Storage;
using Hl7.Fhir.Model;
using System.Net;

namespace FhirCandle.R4.InteractionHooks;

public class CDexTaskProcess : IFhirInteractionHook
{
    /// <summary>Gets the name of the hook - used for logging.</summary>
    public string Name => "DaVinci CDex Task Process Hook";

    /// <summary>Gets the identifier of the hook - MUST be UNIQUE.</summary>
    public string Id => "036a8204-4d4f-46fc-a715-900bc2790a16";

    /// <summary>Gets the supported FHIR versions.</summary>
    public HashSet<FhirCandle.Utils.FhirReleases.FhirSequenceCodes> SupportedFhirVersions => new()
    {
        FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R4,
    };

    /// <summary>
    /// If this operation requires a specific FHIR package to be loaded, the package identifier.
    /// </summary>
    public string RequiresPackage => "hl7.fhir.us.davinci-cdex";

    /// <summary>Gets the interactions by resource.</summary>
    public Dictionary<string, HashSet<Common.StoreInteractionCodes>> InteractionsByResource => new()
    {
        { "Task", new() {
            Common.StoreInteractionCodes.TypeCreate,
            Common.StoreInteractionCodes.TypeCreateConditional,
            Common.StoreInteractionCodes.InstanceUpdate,
            Common.StoreInteractionCodes.InstanceUpdateConditional
        } },
    };

    /// <summary>Gets a list of states of the hook requests.</summary>
    public HashSet<Common.HookRequestStateCodes> HookRequestStates => new()
    {
        Common.HookRequestStateCodes.Post,
    };

    public bool Enabled { get => true; set => throw new NotImplementedException(); }

    /// <summary>Executes the interaction hook operation.</summary>
    /// <param name="ctx">          The context.</param>
    /// <param name="store">        The store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="resource">     The resource.</param>
    /// <param name="hookResponse"> [out] The hook response.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool DoInteractionHook(
        FhirRequestContext ctx, 
        VersionedFhirStore store, 
        IVersionedResourceStore? resourceStore, 
        Resource? resource,
        out FhirResponseContext hookResponse)
    {
        // filter resources we don't care about
        if ((resource == null) ||
            (resource is not Hl7.Fhir.Model.Task task))
        {
            hookResponse = new()
            {
                Outcome = new OperationOutcome()
                {
                    Issue = new List<OperationOutcome.IssueComponent>()
                    {
                        new()
                        {
                            Severity = OperationOutcome.IssueSeverity.Fatal,
                            Code = OperationOutcome.IssueType.Exception,
                            Diagnostics = $"Invalid resource type ({ctx.ResourceType}) for this hook (expecting Task)."
                        }
                    }
                },
            };
            return false;
        }

        // filter out tasks that are not: 'requested' & 'order'
        if ((task.Status != Hl7.Fhir.Model.Task.TaskStatus.Requested) ||
            (task.Intent != Hl7.Fhir.Model.Task.TaskIntent.Order))
        {
            hookResponse = new()
            {
                Outcome = new OperationOutcome()
                {
                    Issue = new List<OperationOutcome.IssueComponent>()
                    {
                        new()
                        {
                            Severity = OperationOutcome.IssueSeverity.Information,
                            Code = OperationOutcome.IssueType.Informational,
                            Diagnostics = $"Task is not in the 'requested' & 'order' state."
                        }
                    }
                },
            };
            return false;
        }

        // look for any 'data-query' inputs
        List<Hl7.Fhir.Model.Task.ParameterComponent> dataQueryInputs = task.Input.Where(
            i => i.Type.Coding.Any(c => c.System == "http://hl7.org/fhir/us/davinci-hrex/CodeSystem/hrex-temp" && c.Code == "data-query")
            ).ToList();

        if (!dataQueryInputs.Any())
        {
            hookResponse = new()
            {
                Outcome = new OperationOutcome()
                {
                    Issue = new List<OperationOutcome.IssueComponent>()
                    {
                        new()
                        {
                            Severity = OperationOutcome.IssueSeverity.Information,
                            Code = OperationOutcome.IssueType.Informational,
                            Diagnostics = $"No 'data-query' inputs found."
                        }
                    }
                },
            };
            return false;
        }

        // process each 'data-query' input
        foreach (Hl7.Fhir.Model.Task.ParameterComponent dataQuery in dataQueryInputs) 
        {
            if (dataQuery.Value is not FhirString fs)
            {
                hookResponse = new()
                {
                    Outcome = new OperationOutcome()
                    {
                        Issue = new List<OperationOutcome.IssueComponent>()
                        {
                            new()
                            {
                                Severity = OperationOutcome.IssueSeverity.Fatal,
                                Code = OperationOutcome.IssueType.Exception,
                                Diagnostics = $"Invalid 'data-query' input value type ({dataQuery.Value.TypeName})."
                            }
                        }
                    },
                };
                return false;
            }

            string query = fs.Value;

            if (string.IsNullOrEmpty(query))
            {
                hookResponse = new()
                {
                    Outcome = new OperationOutcome()
                    {
                        Issue = new List<OperationOutcome.IssueComponent>()
                        {
                            new()
                            {
                                Severity = OperationOutcome.IssueSeverity.Fatal,
                                Code = OperationOutcome.IssueType.Exception,
                                Diagnostics = $"Invalid 'data-query' input value (empty)."
                            }
                        }
                    },
                };
                return false;
            }

            FhirRequestContext queryRequest = new(store, "GET", query);

            if ((!store.PerformInteraction(queryRequest, out FhirResponseContext queryResponse, false)) ||
                (queryResponse.Resource == null) ||
                (queryResponse.Resource is not Bundle resultBundle))
            {
                hookResponse = new()
                {
                    Outcome = new OperationOutcome()
                    {
                        Issue = new List<OperationOutcome.IssueComponent>()
                        {
                            new()
                            {
                                Severity = OperationOutcome.IssueSeverity.Fatal,
                                Code = OperationOutcome.IssueType.Exception,
                                Diagnostics = $"Error performing query ({query})."
                            }
                        }
                    },
                };
                return false;
            }

            // ensure this bundle has an ID
            if (string.IsNullOrEmpty(resultBundle.Id))
            {
                resultBundle.Id = Guid.NewGuid().ToString();
            }

            // add this bundle to be contained in our task
            task.Contained.Add(resultBundle);

            // add results to the task output
            task.Output.Add(new Hl7.Fhir.Model.Task.OutputComponent()
            {
                Type = new CodeableConcept("http://hl7.org/fhir/us/davinci-hrex/CodeSystem/hrex-temp", "data-query"),
                Value = new ResourceReference($"#{resultBundle.Id}"),
            });
        }

        // set the task status to completed
        task.Status = Hl7.Fhir.Model.Task.TaskStatus.Completed;

        // update our task
        if (store.InstanceUpdate(new FhirRequestContext(store, "PUT", $"Task/{task.Id}", task), out FhirResponseContext opResponse))
        {
            hookResponse = new()
            {
                StatusCode = HttpStatusCode.OK,
                Resource = task,
                Outcome = new OperationOutcome()
                {
                    Issue = new List<OperationOutcome.IssueComponent>()
                    {
                        new()
                        {
                            Severity = OperationOutcome.IssueSeverity.Information,
                            Code = OperationOutcome.IssueType.Informational,
                            Diagnostics = $"Task/{task.Id} updated."
                        }
                    }
                },
            };
            return true;
        }

        hookResponse = new()
        {
            Outcome = new OperationOutcome()
            {
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new()
                    {
                        Severity = OperationOutcome.IssueSeverity.Fatal,
                        Code = OperationOutcome.IssueType.Exception,
                        Diagnostics = $"Error processing Task/{task.Id}."
                    }
                }
            },
        };
        return false;
    }
}

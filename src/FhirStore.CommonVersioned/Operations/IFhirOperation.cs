// <copyright file="IFhirOperation.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using System.Net;

namespace FhirCandle.Operations;

/// <summary>Interface for executable FHIR operations.</summary>
public interface IFhirOperation
{
    /// <summary>Gets the name of the operation.</summary>
    string OperationName { get; }

    /// <summary>Gets the operation version.</summary>
    string OperationVersion { get; }

    /// <summary>Gets the canonical by FHIR version.</summary>
    Dictionary<FhirCandle.Utils.FhirReleases.FhirSequenceCodes, string> CanonicalByFhirVersion { get; }

    /// <summary>Gets a value indicating whether this object is named query.</summary>
    bool IsNamedQuery { get; }

    /// <summary>Gets a value indicating whether we allow get.</summary>
    bool AllowGet { get; }

    /// <summary>Gets a value indicating whether we allow post.</summary>
    bool AllowPost { get; }

    /// <summary>Gets a value indicating whether we allow system level.</summary>
    bool AllowSystemLevel { get; }

    /// <summary>Gets a value indicating whether we allow resource level.</summary>
    bool AllowResourceLevel { get; }

    /// <summary>Gets a value indicating whether we allow instance level.</summary>
    bool AllowInstanceLevel { get; }

    /// <summary>Gets a value indicating whether we can accept non-FHIR formats.</summary>
    bool AcceptsNonFhir { get; }

    /// <summary>Gets a value indicating whether we can return non-FHIR formats.</summary>
    bool ReturnsNonFhir { get; }

    /// <summary>
    /// If this operation requires a specific FHIR package to be loaded, the package identifier.
    /// </summary>
    string RequiresPackage { get; }

    /// <summary>Gets the supported resources.</summary>
    HashSet<string> SupportedResources { get; }

    ///// <summary>Gets or sets a value indicating whether this object is enabled.</summary>
    //bool Enabled { get; set; }

    // TODO: Consider return for non-FHIR responses to operations.

    /// <summary>Executes the FHIR operation.</summary>
    /// <param name="ctx">          The authentication.</param>
    /// <param name="store">        The store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="focusResource">The focus resource.</param>
    /// <param name="bodyResource"> The body resource.</param>
    /// <param name="response">     [out] The response resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool DoOperation(
        FhirRequestContext ctx,
        Storage.VersionedFhirStore store,
        Storage.IVersionedResourceStore? resourceStore,
        Hl7.Fhir.Model.Resource? focusResource,
        Hl7.Fhir.Model.Resource? bodyResource,
        out FhirResponseContext response);

    /// <summary>Gets an OperationDefinition resource describing this operation.</summary>
    /// <param name="fhirVersion">The FHIR version.</param>
    /// <returns>The definition.</returns>
    Hl7.Fhir.Model.OperationDefinition? GetDefinition(FhirCandle.Utils.FhirReleases.FhirSequenceCodes fhirVersion);

}


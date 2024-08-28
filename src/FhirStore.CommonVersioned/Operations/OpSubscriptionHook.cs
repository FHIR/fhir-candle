// <copyright file="OpSubscriptionHook.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using FhirCandle.Operations;
using FhirCandle.Storage;
using FhirCandle.Extensions;
using System.Net;
using System.Reflection;

namespace FhirStore.CommonVersioned.Operations;

/// <summary>An operation subscription hook.</summary>
public class OpSubscriptionHook : IFhirOperation
{
    /// <summary>Gets the name of the operation.</summary>
    public string OperationName => "$subscription-hook";

    /// <summary>Gets the operation version.</summary>
    public string OperationVersion => "0.0.1";

    /// <summary>Gets the canonical by FHIR version.</summary>
    public Dictionary<FhirCandle.Utils.FhirReleases.FhirSequenceCodes, string> CanonicalByFhirVersion => new()
    {
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R4, "http://argo.run/fhir/OperationDefinition/subscription-hook" },
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R4B, "http://argo.run/fhir/OperationDefinition/subscription-hook" },
        { FhirCandle.Utils.FhirReleases.FhirSequenceCodes.R5, "http://argo.run/fhir/OperationDefinition/subscription-hook" },
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
    public HashSet<string> SupportedResources => new();

    /// <summary>Executes the system $subscription-hook operation.</summary>
    /// <param name="ctx">          The authentication.</param>
    /// <param name="store">        The store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="focusResource">The focus resource.</param>
    /// <param name="bodyResource"> The body resource.</param>
    /// <param name="opResponse">   [out] The response resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool DoOperation(
        FhirRequestContext ctx,
        VersionedFhirStore store,
        IVersionedResourceStore? resourceStore,
        Hl7.Fhir.Model.Resource? focusResource,
        Hl7.Fhir.Model.Resource? bodyResource,
        out FhirResponseContext opResponse)
    {
        if ((bodyResource == null) ||
            (!(bodyResource is Hl7.Fhir.Model.Bundle bundle)) ||
            (!bundle.Entry.Any()) ||
            (bundle.Entry.First().Resource == null))
        {
            opResponse = new()
            {
                StatusCode = HttpStatusCode.UnprocessableEntity,
                Outcome = FhirCandle.Serialization.SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.UnprocessableEntity,
                    "Posted content is not a valid Subscription notification bundle"),
            };

            return false;
        }

        if (string.IsNullOrEmpty(bundle.Id))
        {
            bundle.Id = Guid.NewGuid().ToString();
        }

        ParsedSubscriptionStatus? status = store.ParseNotificationBundle(bundle);

        if (status == null)
        {
            opResponse = new()
            {
                StatusCode = HttpStatusCode.UnprocessableEntity,
                Outcome = FhirCandle.Serialization.SerializationUtils.BuildOutcomeForRequest(
                    HttpStatusCode.UnprocessableEntity,
                    "Posted content is not a valid Subscription notification bundle"),
            };

            return false;
        }

        // TODO: Clean up interfaces and types so we can avoid casts like this
        // store this bundle in our store
        Hl7.Fhir.Model.Resource? r = ((IVersionedResourceStore)((IFhirStore)store)["Bundle"]).InstanceCreate(ctx, bundle, true);

        // register the notification received event
        store.RegisterReceivedNotification(r?.Id ?? bundle.Id, status);

        opResponse = new()
        {
            StatusCode = HttpStatusCode.OK,
            Outcome = FhirCandle.Serialization.SerializationUtils.BuildOutcomeForRequest(
                HttpStatusCode.OK,
                "Subscription Notification Received"),
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
            Name = "resource",
            Use = Hl7.Fhir.Model.OperationParameterUse.In,
            Min = 1,
            Max = "1",
            Type = Hl7.Fhir.Model.FHIRAllTypes.Bundle,
        });

        def.Parameter.Add(new()
        {
            Name = "return",
            Use = Hl7.Fhir.Model.OperationParameterUse.Out,
            Min = 1,
            Max = "1",
            Type = Hl7.Fhir.Model.FHIRAllTypes.OperationOutcome,
        });

        return def;
    }
}

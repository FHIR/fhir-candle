// <copyright file="Common.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Text.RegularExpressions;

namespace FhirCandle.Storage;

/// <summary>Common functionality for FHIR Stores.</summary>
public static partial class Common
{

    /// <summary>Values that represent store interactions.</summary>
    public enum StoreInteractionCodes
    {
        //CompartmentOperation,     // compartment operations are not defined - either they are instance operations or type operations
        CompartmentSearch,
        CompartmentTypeSearch,

        InstanceDelete,
        InstanceDeleteHistory,
        InstanceDeleteVersion,
        InstanceOperation,
        InstancePatch,
        InstancePatchConditional,
        InstanceRead,
        InstanceReadHistory,
        InstanceReadVersion,
        InstanceUpdate,
        InstanceUpdateConditional,

        TypeCreate,
        TypeCreateConditional,
        TypeDeleteConditional,
        TypeDeleteConditionalSingle,
        TypeDeleteConditionalMultiple,
        TypeHistory,
        TypeOperation,
        TypeSearch,

        SystemCapabilities,
        SystemBundle,
        SystemDeleteConditional,
        SystemHistory,
        SystemOperation,
        SystemSearch,
    }

    /// <summary>Values that represent hook request states.</summary>
    public enum HookRequestStateCodes
    {
        /// <summary>Hooks executed before a request is processed.</summary>
        Pre,

        /// <summary>Hooks executed after a request is processed.</summary>
        Post,

        ///// <summary>Hooks executed after a request is sucessfully processed.</summary>
        //OnSuccess,
        
        ///// <summary>Hooks executed after a request has failed.</summary>
        //OnFail,
    }
}

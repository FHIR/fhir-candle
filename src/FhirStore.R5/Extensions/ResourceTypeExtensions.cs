// <copyright file="ResourceTypeExtensions.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Hl7.FhirPath;

namespace FhirCandle.Extensions;

/// <summary>Resource Type extensions for version-specific FHIR stores.</summary>
public static class ResourceTypeExtensions
{
    /// <summary>Copies the targets described by targets.</summary>
    /// <param name="targets">The targets.</param>
    /// <returns>A ResourceType[]?</returns>
    public static ResourceType[]? CopyTargetsToRt(IEnumerable<VersionIndependentResourceTypesAll?>? targets)
    {
        if (targets is null)
        {
            return null;
        }

        List<ResourceType> results = [];

        foreach (VersionIndependentResourceTypesAll? r in targets)
        {
            if (r is not null)
            {
                ResourceType? rt = ModelInfo.FhirTypeNameToResourceType(r.ToString()!);
                if (rt is not null)
                {
                    results.Add(rt.Value);
                }
            }
        }

        return results.ToArray();
    }

    /// <summary>Copies the targets described by targets.</summary>
    /// <param name="targets">The targets.</param>
    /// <returns>A ResourceType[]?</returns>
    public static VersionIndependentResourceTypesAll[]? CopyTargetsForSp(IEnumerable<ResourceType?>? targets)
    {
        return targets?
            .Where(t => t is not null)
            .Select(t => EnumUtility.GetLiteral(t))
            .Select(t => EnumUtility.ParseLiteral<VersionIndependentResourceTypesAll>(t!))
            .Where(t => t is not null)
            .Select(t => (VersionIndependentResourceTypesAll)t!)
            .ToArray()
            ?? null;
    }


    /// <summary>Copies the targets described by targets.</summary>
    /// <param name="targets">The targets.</param>
    /// <returns>A ResourceType[]?</returns>
    public static VersionIndependentResourceTypesAll[]? CopyTargetsForSp(IEnumerable<VersionIndependentResourceTypesAll?>? targets)
    {
        return targets?
            .Where(t => t is not null)
            .Select(t => (VersionIndependentResourceTypesAll)t!)
            //.Select(t => EnumUtility.GetLiteral(t))
            //.Select(t => EnumUtility.ParseLiteral<ResourceType>(t!))
            //.Where(t => t is not null)
            //.Select(t => (ResourceType)t!)
            .ToArray()
            ?? null;
    }

    /// <summary>Copies the targets described by targets.</summary>
    /// <param name="targets">The targets.</param>
    /// <returns>A ResourceType[]?</returns>
    public static VersionIndependentResourceTypesAll?[]? CopyTargetsNullable(this IEnumerable<ResourceType>? targets)
    {
        return targets?.Select(r => (VersionIndependentResourceTypesAll?)r).ToArray() ?? null;
    }

    /// <summary>Copies the targets described by targets.</summary>
    /// <param name="targets">The targets.</param>
    /// <returns>A ResourceType[]?</returns>
    public static IEnumerable<VersionIndependentResourceTypesAll?> CopyTargetsNullable(this IEnumerable<string>? targets)
    {
        List<VersionIndependentResourceTypesAll?> resourceTypes = new();

        if (targets is null)
        {
            return resourceTypes.AsEnumerable();
        }

        foreach (string r in targets)
        {
            try
            {
                VersionIndependentResourceTypesAll? rt = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<VersionIndependentResourceTypesAll>(r);
                if (rt is not null)
                {
                    resourceTypes.Add(rt!);
                }
            }
            catch (Exception)
            {
            }
        }

        return resourceTypes.AsEnumerable();
    }
}

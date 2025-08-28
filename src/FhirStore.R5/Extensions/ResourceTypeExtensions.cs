// <copyright file="ResourceTypeExtensions.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
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
        if (targets == null)
        {
            return null;
        }

        List<ResourceType> results = [];

        foreach (VersionIndependentResourceTypesAll? r in targets)
        {
            if (r != null)
            {
                ResourceType? rt = ModelInfo.FhirTypeNameToResourceType(r.ToString()!);
                if (rt != null)
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

        if (targets == null)
        {
            return resourceTypes.AsEnumerable();
        }

        foreach (string r in targets)
        {
            try
            {
                VersionIndependentResourceTypesAll? rt = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<VersionIndependentResourceTypesAll>(r);
                if (rt != null)
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

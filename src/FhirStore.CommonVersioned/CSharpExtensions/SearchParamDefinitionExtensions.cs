// <copyright file="SearchParamDefinitionExtensions.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Hl7.Fhir.Model;

namespace FhirCandle.Extensions;

/// <summary>A search parameter definition extensions.</summary>
public static class SearchParamDefinitionExtensions
{
    /// <summary>
    /// A ModelInfo.SearchParamDefinition extension method that makes a deep copy of this object.
    /// </summary>
    /// <param name="sp">The sp.</param>
    /// <returns>A copy of this object.</returns>
    public static ModelInfo.SearchParamDefinition Clone(this ModelInfo.SearchParamDefinition sp)
    {
        return new()
        {
            Resource = sp.Resource,
            Name = sp.Name,
            Url = sp.Url,
            Description = sp.Description,
            Type = sp.Type,
            Component = sp.Component?.Select(v => v).ToArray() ?? null,
            Path = sp.Path?.Select(v => v).ToArray() ?? null,
            XPath = sp.XPath,
            Expression = sp.Expression,
            Target = sp.Target?.Select(v => v).ToArray() ?? null,
        };
    }

    /// <summary>A ModelInfo.SearchParamDefinition extension method that clone with.</summary>
    /// <param name="sp">    The sp.</param>
    /// <param name="target">(Optional) Target for the.</param>
    /// <returns>A ModelInfo.SearchParamDefinition.</returns>
    public static ModelInfo.SearchParamDefinition CloneWith(
        this ModelInfo.SearchParamDefinition sp,
        ResourceType[]? target = null)
    {
        return new()
        {
            Resource = sp.Resource,
            Name = sp.Name,
            Url = sp.Url,
            Description = sp.Description,
            Type = sp.Type,
            Component = sp.Component?.Select(v => v).ToArray() ?? null,
            Path = sp.Path?.Select(v => v).ToArray() ?? null,
            XPath = sp.XPath,
            Expression = sp.Expression,
            Target = target,
        };
    }
}

// <copyright file="ParsedCompartment.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>



using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Hl7.Fhir.Utility;

namespace FhirCandle.Compartments;

public class ParsedCompartment
{
    /// <summary>
    /// Represents an included resource within a compartment.
    /// </summary>
    public record class IncludedResource
    {
        /// <summary>
        /// Gets the type of the resource.
        /// </summary>
        public required string ResourceType { get; init; }

        /// <summary>
        /// Gets the search parameter codes associated with the resource.
        /// </summary>
        public required string[] SearchParamCodes { get; init; }
    }

    /// <summary>
    /// Gets the URL of the compartment definition.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the name of the compartment definition.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the version of the compartment definition.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the type of the compartment.
    /// </summary>
    public required string CompartmentType { get; init; }

    /// <summary>
    /// Gets the included resources within the compartment.
    /// </summary>
    public required Dictionary<string, IncludedResource> IncludedResources { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsedCompartment"/> class.
    /// </summary>
    /// <param name="cd">The compartment definition from which to parse the data.</param>
    /// <exception cref="Exception">Thrown when the compartment definition or its resources lack required elements.</exception>
    [SetsRequiredMembers]
    public ParsedCompartment(Hl7.Fhir.Model.CompartmentDefinition cd)
    {
        Url = cd.Url;
        Name = cd.Name;
        Version = cd.Version;
        CompartmentType = cd.Code.GetLiteral() ?? throw new Exception($"Cannot parse compartment definition without a code element!");

        IncludedResources = cd.Resource
            .Where(r => (r.Code != null) && (r.Param.Count() > 0))
            .Select(r => new IncludedResource
            {
                ResourceType = r.Code.GetLiteral()!,
                SearchParamCodes = r.Param.ToArray(),
            })
            .ToDictionary(ir => ir.ResourceType, ir => ir);
    }
}

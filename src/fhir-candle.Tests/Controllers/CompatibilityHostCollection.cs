// <copyright file="CompatibilityHostCollection.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace fhir.candle.Tests.Controllers;

/// <summary>Serializes host-owning tests because existing services have process-static instances.</summary>
[CollectionDefinition(CompatibilityHostCollection.Name, DisableParallelization = true)]
public sealed class CompatibilityHostCollection
{
    /// <summary>The collection shared by host, package, and notification integration cases.</summary>
    public const string Name = "Compatibility hosts";
}

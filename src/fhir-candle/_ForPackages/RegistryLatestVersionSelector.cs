// <copyright file="RegistryLatestVersionSelector.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using SemVer = SemanticVersioning.Version;

namespace fhir.candle._ForPackages;

internal static class RegistryLatestVersionSelector
{
    /// <summary>Selects the original version with highest semantic precedence, keeping the first on a tie.</summary>
    /// <param name="orderedRegistryWinners">The eligible winners in registry registration order.</param>
    internal static SemVer? SelectWinner(IReadOnlyList<SemVer> orderedRegistryWinners)
    {
        SemVer? winner = null;
        foreach (SemVer candidate in orderedRegistryWinners)
        {
            if (winner is null || candidate.CompareTo(winner) > 0)
            {
                winner = candidate;
            }
        }
        return winner;
    }
}

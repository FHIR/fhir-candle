// <copyright file="DecimalSearchApproximation.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Search;

/// <summary>The local closed, query-relative 10% policy for decimal approximation.</summary>
internal static class DecimalSearchApproximation
{
    /// <summary>Matches exact zero or a same-sign candidate within 10% of the requested magnitude.</summary>
    internal static bool IsMatch(decimal candidate, decimal requested)
    {
        if (requested == 0)
        {
            return candidate == 0;
        }

        if ((candidate == 0) || (decimal.Sign(candidate) != decimal.Sign(requested)))
        {
            return false;
        }

        return decimal.Abs(candidate - requested) <= decimal.Abs(requested) * 0.1m;
    }
}

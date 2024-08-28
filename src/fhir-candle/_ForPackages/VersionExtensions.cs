﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

#if NETSTANDARD2_0
using FhirCandle.Polyfill;
#endif

namespace fhir.candle._ForPackages
{
    internal static class VersionExtensions
    {
        /// <summary>
        /// Gets the regular expression for matching known core package names.
        /// </summary>
        /// <returns>A regular expression.</returns>
        private static readonly Regex _matchCorePackageOnly = new Regex("^hl7\\.fhir\\.(r\\d+[A-Za-z]?)\\.core$", RegexOptions.Compiled);

        /// <summary>
        /// Determines whether the specified package ID belongs to the FHIR core package.
        /// </summary>
        /// <param name="packageId">The package ID to check.</param>
        /// <returns><c>true</c> if the package ID belongs to the FHIR core package; otherwise, <c>false</c>.</returns>
        public static bool PackageIsFhirCore(string packageId)
        {
            return _matchCorePackageOnly.IsMatch(packageId);
        }

        /// <summary>
        /// Retrieves the FHIR versions from a dictionary of package IDs and versions.
        /// </summary>
        /// <param name="packages">The dictionary of package IDs and versions.</param>
        /// <returns>A list of FHIR version numbers if provided (e.g., 4.0.1), R-literals if not (e.g., R4).</returns>
        public static List<string> FhirVersionsFromPackages(Dictionary<string, string?>? packages)
        {
            List<string> fhirVersions = new();

            if (packages == null)
            {
                return fhirVersions;
            }

            foreach ((string packageId, string? version) in packages)
            {
                Match match = _matchCorePackageOnly.Match(packageId);
                if (!match.Success)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(version))
                {
                    fhirVersions.Add(match.Groups[0].Value.ToUpperInvariant());
                }
                else
                {
                    fhirVersions.Add(version!);
                }
            }

            return fhirVersions;
        }
    }
}

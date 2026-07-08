// <copyright file="Common.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

#if NET8_0_OR_GREATER
using System.Collections.Immutable;
#elif NETSTANDARD2_0
using FhirCandle.Polyfill;
#endif

namespace FhirCandle.Search;

/// <summary>Common search definitions.</summary>
public static class Common
{
    /// <summary>(Immutable) Options for controlling the HTTP.</summary>
#if NET8_0_OR_GREATER
    public static readonly ImmutableHashSet<string> HttpParameters = ImmutableHashSet.Create(new string[]
#elif NETSTANDARD2_0
    public static readonly HashSet<string> HttpParameters = ImmutableHashSet<string>.Create(new string[]
#endif
    {
        /// <summary>Override the HTTP content negotiation.</summary>
        "_format",

        /// <summary>Ask for a pretty printed response for human convenience.</summary>
        "_pretty",

        /// <summary>Ask for a predefined short form of the resource in response.</summary>
        "_summary",

        /// <summary>Ask for a particular set of elements to be returned.</summary>
        "_elements",
    });

    /// <summary>(Immutable) Options for controlling the search result.</summary>
#if NET8_0_OR_GREATER
    public static readonly ImmutableHashSet<string> SearchResultParameters = ImmutableHashSet.Create(new string[]
#elif NETSTANDARD2_0
    public static readonly HashSet<string> SearchResultParameters = ImmutableHashSet<string>.Create(new string[]
#endif
    {
        /// <summary>Request different types of handling for contained resources.</summary>
        "_contained",

        /// <summary>Limit the number of match results per page of response..</summary>
        "_count",

        /// <summary>Include additional resources according to a GraphDefinition.</summary>
        "_graph",

        /// <summary>Include additional resources, based on following links forward across references.</summary>
        "_include",

        /// <summary>Include additional resources, based on following links forward across references in an included resource.</summary>
        "_include:iterate",

        /// <summary>Include additional resources, based on following reverse links across references.</summary>
        "_revinclude",

        /// <summary>Request match relevance in results.</summary>
        "_score",

        /// <summary>Request which order results should be returned in.</summary>
        "_sort",

        /// <summary>Request a precision of the total number of results for a request.</summary>
        "_total"
    });

    /// <summary>
    /// Determines whether a URL query string contains at least one real search parameter — that
    /// is, a parameter that is neither an HTTP control parameter (<c>_format</c>, <c>_pretty</c>,
    /// <c>_summary</c>, <c>_elements</c>) nor a search-result parameter (<c>_count</c>,
    /// <c>_include</c>, <c>_sort</c>, ...). A leading '?' is tolerated. Used by the write paths to
    /// decide whether a request is genuinely conditional (create / update / type-delete) rather
    /// than a plain write that merely carries serialization-control parameters.
    /// </summary>
    /// <param name="urlQuery">The URL query string (with or without a leading '?').</param>
    /// <returns>True if at least one real search parameter is present; otherwise, false.</returns>
    public static bool QueryContainsSearchParameters(string? urlQuery)
    {
        if (string.IsNullOrEmpty(urlQuery))
        {
            return false;
        }

        System.Collections.Specialized.NameValueCollection queryParams =
            System.Web.HttpUtility.ParseQueryString(urlQuery);

        foreach (string? key in queryParams.AllKeys ?? Array.Empty<string>())
        {
            if (string.IsNullOrEmpty(key) ||
                HttpParameters.Contains(key) ||
                SearchResultParameters.Contains(key))
            {
                continue;
            }

            return true;
        }

        return false;
    }
}

// <copyright file="ParsedResultParameter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Extensions;
using FhirCandle.Storage;
using Hl7.Fhir.Model;
using Hl7.FhirPath;

namespace FhirCandle.Models;

/// <summary>A parsed search result parameter.</summary>
public class ParsedResultParameters
{
    /// <summary>(Immutable) Options for controlling the search result.</summary>
    public static readonly HashSet<string> SearchResultParameters = new()
    {
        "_contained",
        "_count",
        "_elements",
        "_graph",
        "_include",
        "_include:iterate",
        "_maxresults",
        "_revinclude",
        "_score",
        "_sort",
        "_summary",
        "_total",
    };

    /// <summary>Gets or sets the inclusion FHIRpath extractions, keyed by resource.</summary>
    public Dictionary<string, List<ModelInfo.SearchParamDefinition>> Inclusions { get; set; } = new();

    /// <summary>Gets or sets the iterative inclusion FHIRpath extractions, keyed by resource.</summary>
    public Dictionary<string, List<string>> IterativeInclusions { get; set; } = new();

    /// <summary>Gets or sets the reverse inclusion search parameter definitions, keyed by resource.</summary>
    public Dictionary<string, List<ModelInfo.SearchParamDefinition>> ReverseInclusions { get; set; } = new();

    /// <summary>
    /// Represents a request to sort search results by a specific search parameter.
    /// </summary>
    /// <param name="SearchParameterCode">The code of the search parameter to sort by.</param>
    /// <param name="Ascending">Indicates whether the sorting should be in ascending order.</param>
    public record class SortRequest(
        string RequestLiteral,
        string SearchParameterCode,
        string? Modifier,
        bool Ascending,
        string SelectExpression,
        CompiledExpression Compiled);

    /// <summary>
    /// Gets or sets the array of sort requests.
    /// </summary>
    public SortRequest[] SortRequests { get; set; } = [];

    /// <summary>
    /// The parameter _count is defined as an instruction to the server regarding how many resources
    /// should be returned in a single page. Servers SHALL NOT return more resources in a single page than
    /// requested, even if they don't support paging, but may return less than the client requested.
    /// </summary>
    public int? PageMatchCount { get; set; } = null;

    /// <summary>
    /// Gets or sets the maximum number of results to return.
    /// </summary>
    public long? MaxResults { get; set; } = null;

    /// <summary>The applied query string.</summary>
    private string _appliedQueryString = string.Empty;

    /// <summary>
    /// Initializes a new instance of the FhirCandle.Models.ParsedResultParameters class.
    /// </summary>
    /// <param name="queryString">The query string.</param>
    /// <param name="store">      The FHIR store.</param>
    public ParsedResultParameters(
        string queryString,
        VersionedFhirStore store,
        IVersionedResourceStore resourceStore,
        string resourceType)
    {
        parse(queryString, store, resourceStore, resourceType);
    }

    /// <summary>Gets applied query string.</summary>
    /// <returns>The applied query string.</returns>
    public string GetAppliedQueryString()
    {
        return _appliedQueryString;
    }

    /// <summary>Enumerates parse in this collection.</summary>
    /// <param name="queryString">The query string.</param>
    /// <param name="store">      The FHIR store.</param>
    private void parse(
        string queryString,
        VersionedFhirStore store,
        IVersionedResourceStore resourceStore,
        string resourceType)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return;
        }

        List<string> applied = new();

        System.Collections.Specialized.NameValueCollection query = System.Web.HttpUtility.ParseQueryString(queryString);
        foreach (string key in query)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            string? value = query[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            switch (key)
            {
                case "_contained":
                    break;

                case "_count":
                    {
                        if (int.TryParse(value, out int count) &&
                            (count >= 0))
                        {
                            PageMatchCount = count;
                            applied.Add(key + "=" + value);
                        }
                    }
                    break;

                case "_elements":
                    break;

                case "_graph":
                    break;

                case "_include":
                    {
                        foreach (string val in value.Split(','))
                        {
                            string[] components = val.Split(':');

                            ResourceType? rt = null;

                            switch (components.Length)
                            {
                                // _include=[resource]:[parameter]
                                case 2:
                                    break;

                                // _include=[resource]:[parameter]:[targetType]
                                case 3:
                                    rt = ModelInfo.FhirTypeNameToResourceType(components[2]);
                                    break;

                                // invalid / unknown
                                default:
                                    continue;
                            }

                            if ((!store.TryGetSearchParamDefinition(components[0], components[1], out ModelInfo.SearchParamDefinition? spDefinition)) ||
                                (spDefinition == null))
                            {
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(spDefinition.Expression))
                            {
                                continue;
                            }

                            if (!Inclusions.ContainsKey(components[0]))
                            {
                                Inclusions.Add(components[0], new());
                            }

                            // if we have a third component, it's a resource type
                            if (rt != null)
                            {
                                // override the default allowed targets to only the one specified
                                spDefinition = spDefinition.CloneWith(new ResourceType[] { (ResourceType)rt });
                            }

                            Inclusions[components[0]].Add(spDefinition);
                            applied.Add(key + "=" + value);
                        }
                    }
                    break;

                case "_include:iterate":
                    {
                        foreach (string val in value.Split(','))
                        {
                            string[] components = val.Split(':');

                            if (components.Length != 2)
                            {
                                continue;
                            }

                            if ((!store.TryGetSearchParamDefinition(components[0], components[1], out ModelInfo.SearchParamDefinition? spDefinition)) ||
                                (spDefinition == null))
                            {
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(spDefinition.Expression))
                            {
                                continue;
                            }

                            if (!IterativeInclusions.ContainsKey(components[0]))
                            {
                                IterativeInclusions.Add(components[0], new());
                            }

                            IterativeInclusions[components[0]].Add(spDefinition.Expression);
                            applied.Add(key + "=" + value);
                        }
                    }
                    break;

                case "_maxresults":
                    {
                        if (long.TryParse(value, out long maxResults) &&
                            (maxResults >= 0))
                        {
                            MaxResults = maxResults;
                            applied.Add(key + "=" + value);
                        }
                    }
                    break;

                case "_revinclude":
                    {
                        foreach (string val in value.Split(','))
                        {

                            string[] components = val.Split(':');

                            ResourceType? rt = null;

                            switch (components.Length)
                            {
                                // _revinclude=[resource]:[parameter]
                                case 2:
                                    break;

                                // _revinclude=[resource]:[parameter]:[targetType]
                                case 3:
                                    rt = ModelInfo.FhirTypeNameToResourceType(components[2]);
                                    break;

                                // invalid / unknown
                                default:
                                    continue;
                            }

                            if ((!store.TryGetSearchParamDefinition(components[0], components[1], out ModelInfo.SearchParamDefinition? spDefinition)) ||
                                (spDefinition == null))
                            {
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(spDefinition.Expression))
                            {
                                continue;
                            }

                            // if we have a third component, it's a resource type
                            if (rt != null)
                            {
                                // override the default allowed targets to only the one specified
                                spDefinition = spDefinition.CloneWith(new ResourceType[] { (ResourceType)rt });
                            }

                            if (!ReverseInclusions.ContainsKey(components[0]))
                            {
                                ReverseInclusions.Add(components[0], new());
                            }

                            ReverseInclusions[components[0]].Add(spDefinition);
                            applied.Add(key + "=" + value);
                        }
                    }
                    break;

                case "_score":
                    break;

                case "_sort":
                    {
                        List<SortRequest> sr = [];

                        foreach (string val in value.Split(','))
                        {
                            string[] components = val.Split(':');

                            string name;
                            bool ascending;

                            if (components[0].StartsWith('-'))
                            {
                                name = components[0][1..];
                                ascending = false;
                            }
                            else
                            {
                                name = components[0];
                                ascending = true;
                            }

                            string? modifier = components.Length > 1 ? components[1] : null;

                            if (!resourceStore.TryGetSearchParamDefinition(name, out ModelInfo.SearchParamDefinition? spDefinition))
                            {
                                continue;
                            }

                            // do not sort on composite search parameters
                            if ((spDefinition.Component != null) && (spDefinition.Component.Any()))
                            {
                                continue;
                            }

                            string selectExpression = spDefinition.Expression ?? string.Empty;
                            CompiledExpression? compiled = store.GetCompiledSearchParameter(spDefinition.Resource ?? resourceType, name, selectExpression);

                            if (compiled == null)
                            {
                                continue;
                            }

                            sr.Add(
                                new SortRequest(
                                    val,
                                    name,
                                    modifier,
                                    ascending,
                                    selectExpression,
                                    compiled));
                        }

                        SortRequests = sr.ToArray();

                        applied.Add(key + "=" + string.Join(',', sr.Select(r => r.RequestLiteral)));
                    }
                    break;

                case "_summary":
                    break;

                case "_total":
                    break;
            }
        }

        _appliedQueryString = string.Join('&', applied);
    }
}

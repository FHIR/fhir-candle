// <copyright file="ParsedSearchParameter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Extensions;
using FhirCandle.Search;
using FhirCandle.Storage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Hl7.FhirPath;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using static FhirCandle.Search.SearchDefinitions;
using System.Runtime.Versioning;

namespace FhirCandle.Models;

/// <summary>A parsed search parameter.</summary>
public class ParsedSearchParameter : ICloneable
{
    /// <summary>(Immutable) Options for controlling all resource.</summary>
    internal static readonly Dictionary<string, SearchParamDefinition> _allResourceParameters = new()
    {
        {"_content", new()
            {
                Name = "_content",
                Code = "_content",
                Type = SearchParamType.Special,
                Description = "Search on the entire content of the resource"
            }
        },

        { "_filter", new()
            {
                Name = "_filter",
                Code = "_filter",
                Type = SearchParamType.Special,
                Description = "Filter search parameter which supports a more sophisticated grammar for searching. See documentation for further details."
            }
        },

        { "_has", new()
            {
                Name = "_has",
                Code = "_has",
                Type = SearchParamType.Special,
                Description = "Provides limited support for reverse chaining - that is, selecting resources based on the properties of resources that refer to them (instead of chaining where resources can be selected based on the properties of resources that they refer to). See the FHIR search page for further documentation"
            }
        },

        { "_id", new()
            {
                Name = "_id",
                Code = "_id",
                Type = SearchParamType.Token,
                Expression = "Resource.id",
                Description = "Logical id of this artifact"
            }
        },

        { "_in", new()
            {
                Name = "_in",
                Code = "_in",
                Type = SearchParamType.Reference,
                Description = "Allows for the retrieval of resources that are active members of a CareTeam, Group, or List",
            }
        },

        { "_language", new()
            {
                Name = "_language",
                Code = "_language",
                Type = SearchParamType.Token,
                Expression = "Resource.language",
                Description = "Language of the resource content",
            }
        },

        { "_lastUpdated", new()
            {
                Name = "_lastUpdated",
                Code = "_lastUpdated",
                Type = SearchParamType.Date,
                Expression = "Resource.meta.lastUpdated",
                Description = "When the resource version last changed, see documentation for expectations and limitations",
            }
        },

        { "_list", new()
            {
                Name = "_list",
                Code = "_list",
                Type = SearchParamType.Special,
                Description = "Allows for the retrieval of resources that are referenced by a List resource or by one of the pre-defined functional lists",
            }
        },

        { "_profile", new()
            {
                Name = "_profile",
                Code = "_profile",
                Type = SearchParamType.Reference,
                Expression = "Resource.meta.profile",
                Description = "Profiles this resource claims to conform to",
            }
        },

        { "_query", new()
            {
                Name = "_query",
                Code = "_query",
                Type = SearchParamType.Token,
                Description = "A custom search profile that describes a specific defined query operation",
            }
        },

        { "_security", new()
            {
                Name = "_security",
                Code = "_security",
                Type = SearchParamType.Token,
                Expression = "Resource.meta.security",
                Description = "Security Labels applied to this resource",
            }
        },

        { "_source", new()
            {
                Name = "_source",
                Code = "_source",
                Type = SearchParamType.Uri,
                Expression = "Resource.meta.source",
                Description = "Identifies where the resource comes from",
            }
        },

        { "_tag", new()
            {
                Name ="_tag",
                Code = "_tag",
                Type = SearchParamType.Token,
                Expression = "Resource.meta.tag",
                Description = "Tags applied to this resource",
            }
        },

        // Note that I have not implemented the _text search parameter, so I do not want to list it here.
        // { "_text", new()
        //     {
        //         Name = "_text",
        //         Code = "_text",
        //         Type = SearchParamType.String,
        //         Description = "Advanced implementation-dependant search against the narrative content of a resource.",
        //     }
        // },

        { "_type", new()
            {
                Name = "_type",
                Code = "_type",
                Type = SearchParamType.Token,
                Expression = "%resource",
                Description = "A resource type filter",
            }
        },
    };

    /// <summary>A segmented reference.</summary>
    public record struct SegmentedReference(
        string ResourceType,
        string Id,
        string ResourceVersion,
        string CanonicalVersion,
        string Url);

    /// <summary>Gets or sets the type of the resource.</summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets the values.</summary>
    public required string[] Values { get; set; }

    /// <summary>Gets or sets the applied value flags.</summary>
    public bool[] IgnoredValueFlags { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether this parameter has been ignored.</summary>
    public bool IgnoredParameter { get; set; } = false;

    public string? IgnoredReason { get; set; } = null;

    /// <summary>Gets or sets the chained parameter.</summary>
    public Dictionary<string, ParsedSearchParameter>? ChainedParameters { get; set; } = null;

    public ParsedSearchParameter? ReverseChainedParameterLink { get; set; } = null;

    public ParsedSearchParameter? ReverseChainedParameterFilter { get; set; } = null;

    /// <summary>Gets or sets the composite components.</summary>
    public ParsedSearchParameter[]? CompositeComponents { get; set; } = null;

    /// <summary>Gets or sets the date starts.</summary>
    public DateTimeOffset[]? ValueDateStarts { get; set; } = null;

    /// <summary>Gets or sets the date ends.</summary>
    public DateTimeOffset[]? ValueDateEnds { get; set; } = null;

    /// <summary>Gets or sets the values for integer types.</summary>
    public long[]? ValueInts { get; set; } = null;

    /// <summary>Gets or sets the values for decimal types.</summary>
    public decimal[]? ValueDecimals { get; set; } = null;

    /// <summary>Gets or sets the value FHIR codes.</summary>
    public Hl7.Fhir.ElementModel.Types.Code[]? ValueFhirCodes { get; set; }

    /// <summary>Gets or sets a list of types of the value FHIR codes.</summary>
    public Hl7.Fhir.ElementModel.Types.Code[]? ValueFhirCodeTypes { get; set; }

    /// <summary>Gets or sets the value bools.</summary>
    public bool[]? ValueBools { get; set; } = null;

    /// <summary>Gets or sets the value references.</summary>
    public SegmentedReference[]? ValueReferences { get; set; }

    /// <summary>Gets or sets the prefix.</summary>
    public SearchPrefixCodes?[] Prefixes { get; set; } = [];

    /// <summary>Gets or sets the type of the parameter.</summary>
    public required SearchParamType ParamType { get; set; }

    /// <summary>Gets or sets the modifier.</summary>
    public string? ModifierLiteral { get; set; } = null;

    /// <summary>Gets or sets the modifier.</summary>
    public SearchModifierCodes Modifier { get; set; } = SearchModifierCodes.None;

    /// <summary>Gets or sets the fhirPath extraction query.</summary>
    public required string SelectExpression { get; set; }

    /// <summary>Gets or sets the compiled expression.</summary>
    public required CompiledExpression? CompiledExpression { get; set; }

    public required string? RequestedKeyLiteral { get; set; }
    public required string? RequestedValueLiteral { get; set; }

    [SetsRequiredMembers]
    public ParsedSearchParameter(ParsedSearchParameter other)
    {
        ResourceType = other.ResourceType;
        Name = other.Name;
        Values = other.Values.Select(v => v).ToArray();
        IgnoredValueFlags = other.IgnoredValueFlags.Select(v => v).ToArray();
        IgnoredParameter = other.IgnoredParameter;
        IgnoredReason = other.IgnoredReason;
        ChainedParameters = other.ChainedParameters?.DeepCopy();
        ReverseChainedParameterLink = ReverseChainedParameterLink is null ? null : new ParsedSearchParameter(ReverseChainedParameterLink);
        ReverseChainedParameterFilter = ReverseChainedParameterFilter is null ? null : new ParsedSearchParameter(ReverseChainedParameterFilter);
        CompositeComponents = other.CompositeComponents?.Select(c => new ParsedSearchParameter(c)).ToArray();
        ValueDateStarts = other.ValueDateStarts?.Select(v => v).ToArray();
        ValueDateEnds = other.ValueDateEnds?.Select(v => v).ToArray();
        ValueInts = other.ValueInts?.Select(v => v).ToArray();
        ValueDecimals = other.ValueDecimals?.Select(v => v).ToArray();
        ValueFhirCodes = other.ValueFhirCodes?.Select(v => v).ToArray();
        ValueFhirCodeTypes = other.ValueFhirCodeTypes?.Select(v => v).ToArray();
        ValueBools = other.ValueBools?.Select(v => v).ToArray();
        ValueReferences = other.ValueReferences?.Select(v => v with { }).ToArray();
        Prefixes = other.Prefixes.Select(v => v).ToArray();
        ParamType = other.ParamType;
        ModifierLiteral = other.ModifierLiteral;
        Modifier = other.Modifier;
        SelectExpression = other.SelectExpression;
        CompiledExpression = other.CompiledExpression;
        RequestedKeyLiteral = other.RequestedKeyLiteral;
        RequestedValueLiteral = other.RequestedValueLiteral;
    }

    /// <summary>
    /// Initializes a new instance of the FhirCandle.Models.ParsedSearchParameter class.
    /// </summary>
    /// <param name="store">          The FHIR store.</param>
    /// <param name="resourceStore">  The resource store.</param>
    /// <param name="resourceType">   Type of the resource.</param>
    /// <param name="name">           The search parameter name.</param>
    /// <param name="modifierLiteral">The search modifier literal.</param>
    /// <param name="modifierCode">   The search modifier code.</param>
    /// <param name="value">          The http-parameter value string.</param>
    /// <param name="spd">            The search parameter definition.</param>
    /// <param name="component">      (Optional) The component.</param>
    [SetsRequiredMembers]
    public ParsedSearchParameter(
        VersionedFhirStore store,
        IVersionedResourceStore resourceStore,
        string resourceType,
        string name,
        string modifierLiteral,
        SearchModifierCodes modifierCode,
        string value,
        SearchParamDefinition? spd,
        SearchParamComponent? component = null)
    {
        Name = name;
        ResourceType = resourceType;
        Modifier = modifierCode;
        ModifierLiteral = modifierLiteral;

        if ((spd is null) || string.IsNullOrEmpty(spd.Expression))
        {
            ParamType = spd?.Type ?? SearchParamType.Special;
            SelectExpression = string.Empty;
            CompiledExpression = null;
            Values = [];
            IgnoredParameter = true;
            IgnoredReason = $"Search parameter definition for '{name}' is null or has no expression.";
            return;
        }

        SearchParamDefinition definition = spd;

        // use the component first
        if (component is not null)
        {
            // need to resolve component URL
            if (!resourceStore.TryGetSearchParamDefinition(component.Value.Definition, out SearchParamDefinition? componentDefinition))
            {
                // ignore this parameter
                ParamType = SearchParamType.Special;
                SelectExpression = string.Empty;
                CompiledExpression = null;
                Values = [];
                IgnoredParameter = true;
                IgnoredReason = $"Search parameter component definition '{component.Value.Definition}' for '{name}' is not defined in the store.";
                return;
            }

            definition = componentDefinition;
            Name = componentDefinition.Name ?? Name;
            ParamType = componentDefinition.Type;
            SelectExpression = string.IsNullOrEmpty(component.Value.Expression) ? componentDefinition.Expression ?? string.Empty : component.Value.Expression;

            if (string.IsNullOrEmpty(SelectExpression))
            {
                // ignore this parameter
                ParamType = SearchParamType.Special;
                SelectExpression = string.Empty;
                CompiledExpression = null;
                Values = [];
                IgnoredParameter = true;
                IgnoredReason = $"Search parameter component definition '{component.Value.Definition}' for '{name}' has no expression.";
                return;
            }

            CompiledExpression = store.GetCompiledSearchParameter(spd.Resource ?? string.Empty, $"{spd.Name}${componentDefinition.Name}", SelectExpression);
        }
        else
        {
            ParamType = spd.Type;
            SelectExpression = spd.Expression ?? string.Empty;
            CompiledExpression = store.GetCompiledSearchParameter(spd.Resource ?? string.Empty, name, SelectExpression);

            if (spd.Type == SearchParamType.Composite)
            {
                if (!(spd.Component?.Any() ?? false))
                {
                    // ignore this parameter
                    ParamType = SearchParamType.Special;
                    SelectExpression = string.Empty;
                    CompiledExpression = null;
                    Values = [];
                    IgnoredParameter = true;
                    IgnoredReason = $"Search parameter definition '{name}' is a composite but has no components defined.";
                    return;
                }

                ExtractCompositeParams(
                    store,
                    resourceStore,
                    resourceType,
                    spd,
                    modifierLiteral,
                    modifierCode,
                    value,
                    out List<ParsedSearchParameter> cpValues);

                CompositeComponents = cpValues.ToArray();

                // we do not want to run composite parameters through the normal parsing logic
                Prefixes = [];
                Values = [];

                return;
            }
        }

        if (string.IsNullOrEmpty(value))
        {
            Values = [];
            IgnoredParameter = true;
            IgnoredReason = $"Search parameter '{name}' has no value provided.";
            return;
        }

        // parse the value string into prefixes and values
        ExtractValues(value, definition);

        Values ??= [];

        // by default, assume all values are applied (will be updated during typed parsing)
        IgnoredValueFlags = Enumerable.Repeat(false, Values.Length).ToArray<bool>();

        ProcessTypedValues(value, definition);

        // check for no valid values to apply
        if (IgnoredValueFlags.Any() && IgnoredValueFlags.All(x => x))
        {
            IgnoredParameter = true;
            IgnoredReason = $"Search parameter '{name}' has no valid values to apply after processing.";
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsedSearchParameter"/> class.
    /// </summary>
    /// <param name="store">        The FHIR store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="parseResult">  The parse result.</param>
    /// <param name="value">        The http-parameter value string.</param>
    /// <param name="requestKeyLiteral"></param>
    /// <param name="requestValueLiteral"></param>
    [SetsRequiredMembers]
    private ParsedSearchParameter(
        VersionedFhirStore store,
        IVersionedResourceStore resourceStore,
        SearchKeyParseResult parseResult,
        string value,
        string? requestKeyLiteral,
        string? requestValueLiteral)
    {
        Name = parseResult.SearchParameterName;
        ResourceType = parseResult.ResourceType;
        Modifier = parseResult.ModifierCode;
        ModifierLiteral = parseResult.ModifierLiteral;
        RequestedKeyLiteral = requestKeyLiteral;
        RequestedValueLiteral = requestValueLiteral;

        // check for reverse chained parameters - short circuit the constructor logic
        if ((parseResult.ReverseLinkKey is not null) &&
            (parseResult.ReverseLinkFilter is not null))
        {
            ReverseChainedParameterLink = new ParsedSearchParameter(
                store,
                (IVersionedResourceStore)((IFhirStore)store)[parseResult.ReverseLinkKey.ResourceType],
                parseResult.ReverseLinkKey,
                string.Empty,
                null,
                null);
            ReverseChainedParameterLink.IgnoredParameter = false;

            ReverseChainedParameterFilter = new ParsedSearchParameter(
                store,
                (IVersionedResourceStore)((IFhirStore)store)[parseResult.ReverseLinkFilter.ResourceType],
                parseResult.ReverseLinkFilter,
                value,
                null,
                null);

            ParamType = SearchParamType.Special;
            SelectExpression = string.Empty;
            CompiledExpression = null;
            Values = [];
            IgnoredParameter = false;
            return;
        }

        if ((parseResult.SearchParameterDefinition is null) ||
            string.IsNullOrEmpty(parseResult.SearchParameterDefinition.Expression))
        {
            ParamType = parseResult.SearchParameterDefinition?.Type ?? SearchParamType.Special;
            SelectExpression = string.Empty;
            CompiledExpression = null;
            Values = [];
            IgnoredParameter = true;
            return;
        }

        ParamType = parseResult.SearchParameterDefinition.Type;
        SelectExpression = parseResult.SearchParameterDefinition.Expression;
        CompiledExpression = store.GetCompiledSearchParameter(
            parseResult.SearchParameterDefinition.Resource ?? string.Empty,
            parseResult.SearchParameterName,
            SelectExpression);

        if (parseResult.SearchParameterDefinition.Type == SearchParamType.Composite)
        {
            if (!(parseResult.SearchParameterDefinition.Component?.Any() ?? false))
            {
                // ignore this parameter
                ParamType = SearchParamType.Special;
                SelectExpression = string.Empty;
                CompiledExpression = null;
                Values = [];
                IgnoredParameter = true;
                return;
            }

            ExtractCompositeParams(
                store,
                resourceStore,
                ResourceType,
                parseResult.SearchParameterDefinition,
                parseResult.ModifierLiteral,
                parseResult.ModifierCode,
                value,
                out List<ParsedSearchParameter> cpValues);

            CompositeComponents = cpValues.ToArray();

            // we do not want to run composite parameters through the normal parsing logic
            Prefixes = [];
            Values = [];

            return;
        }

        // check for chained parameters - short circuit the constructor logic
        if (parseResult.ChainedKeys is not null)
        {
            ChainedParameters = new();
            foreach (SearchKeyParseResult ck in parseResult.ChainedKeys)
            {
                ChainedParameters.Add(
                    ck.ResourceType,
                    new ParsedSearchParameter(
                        store,
                        (IVersionedResourceStore)((IFhirStore)store)[ck.ResourceType],
                        ck,
                        value,
                        requestKeyLiteral,
                        requestValueLiteral));
            }

            // when chaining, we do not want to parse the value - it is handled at the last link in the chain
            Values = [];
            return;
        }

        if (string.IsNullOrEmpty(value))
        {
            Values = [];
            IgnoredParameter = true;
            return;
        }

        // parse the value string into prefixes and values
        ExtractValues(value, parseResult.SearchParameterDefinition);

        Values ??= [];

        // by default, assume all values are applied (will be updated during typed parsing)
        IgnoredValueFlags = Enumerable.Repeat(false, Values.Length).ToArray<bool>();

        ProcessTypedValues(value, parseResult.SearchParameterDefinition);

        // check for no valid values to apply
        if ((IgnoredValueFlags.Length != 0) &&
            IgnoredValueFlags.All(x => x))
        {
            IgnoredParameter = true;
        }
    }

    /// <summary>Gets applied query string.</summary>
    /// <param name="includeName">(Optional) True to include, false to exclude the name.</param>
    /// <returns>The applied query string.</returns>
    public string GetAppliedQueryString(bool includeName = true)
    {
        if (IgnoredParameter)
        {
            return string.Empty;
        }

        if ((RequestedKeyLiteral is not null) && (RequestedValueLiteral is not null))
        {
            return $"{RequestedKeyLiteral}={RequestedValueLiteral}";
        }

        System.Text.StringBuilder sb = new();

        // nest into chained parameters
        if (ChainedParameters?.Any() ?? false)
        {
            if (includeName)
            {
                sb.Append(Name);

                if (Modifier != SearchModifierCodes.None)
                {
                    sb.Append(':');
                    sb.Append(ModifierLiteral);
                }

                sb.Append('.');
            }

            sb.Append(ChainedParameters.First().Value.GetAppliedQueryString(includeName));

            return sb.ToString();
        }

        // nest into composite parameters
        if (CompositeComponents?.Any() ?? false)
        {
            if (includeName)
            {
                sb.Append(Name);

                if (Modifier != SearchModifierCodes.None)
                {
                    sb.Append(':');
                    sb.Append(ModifierLiteral);
                }

                sb.Append('=');
            }

            sb.Append(string.Join('$', CompositeComponents.Select(c => c.GetAppliedQueryString(false))));

            return sb.ToString();
            //sb.Append(string.Join('$', CompositeComponents.SelectMany(c => c.Values)));
        }

        // iterate across values
        for (int i = 0; i < Values.Length; i++)
        {
            if (IgnoredValueFlags[i])
            {
                continue;
            }

            if (includeName)
            {
                if (sb.Length == 0)
                {
                    sb.Append(Name);

                    if (Modifier != SearchModifierCodes.None)
                    {
                        sb.Append(':');
                        sb.Append(ModifierLiteral);
                    }

                    sb.Append('=');
                }
                else
                {
                    sb.Append(',');
                }
            }
            else if (sb.Length != 0)
            {
                sb.Append(',');
            }

            if ((Prefixes.Length > i) && (Prefixes[i] is not null))
            {
                sb.Append(Prefixes[i]!.ToLiteral() ?? string.Empty);
            }

            sb.Append(Values[i]);
        }

        return sb.ToString();
    }

    /// <summary>Extracts the composite parameters.</summary>
    /// <param name="store">        The FHIR store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="modifierCode"></param>
    /// <param name="value">        The http-parameter value string.</param>
    /// <param name="resourceType"></param>
    /// <param name="spd"></param>
    /// <param name="modifierLiteral"></param>
    /// <param name="cpValues"></param>
    /// <returns>The extracted composite parameters.</returns>
    private static void ExtractCompositeParams(
        VersionedFhirStore store,
        IVersionedResourceStore resourceStore,
        string resourceType,
        SearchParamDefinition spd,
        string modifierLiteral,
        SearchModifierCodes modifierCode,
        string value,
        out List<ParsedSearchParameter> cpValues)
    {
        cpValues = new();

        if (!(spd.Component?.Any() ?? false))
        {
            // ignore this parameter
            return;
        }

        string[] split = value.Split('$');

        if (split.Length != spd.Component.Length)
        {
            // ignore this parameter
            return;
        }

        for (int i = 0; i < split.Length; i++)
        {
            // create new search parameters for each component

            // create the composite parameter
            cpValues.Add(new ParsedSearchParameter(
                store,
                resourceStore,
                resourceType,
                spd.Name ?? spd.Code ?? string.Empty,
                modifierLiteral,
                modifierCode,
                split[i],
                spd,
                spd.Component[i]));
        }

        // note this is wrong - composite parameters do not contain the name of the parameter
        //// work backwards through the composite values so we can understand multi-valued components
        //for (int i = split.Length - 1; i >= 0; i--)
        //{
        //    int delimIndex = split[i].IndexOf('$');

        //    if (delimIndex == -1)
        //    {
        //        // track this value
        //        compositeValues.Add(split[i]);
        //        continue;
        //    }

        //    string cName = split[i].Substring(0, delimIndex);
        //    string cValue = split[i].Substring(delimIndex + 1);

        //    // track this value
        //    compositeValues.Add(cValue);

        //    // parse a single component of this composite parameter
        //    cpValues.AddRange(Parse($"{cName}={string.Join(',', compositeValues)}", store, resourceStore));

        //    // clear our tracked values
        //    compositeValues.Clear();
        //}
    }

    /// <summary>Extracts the values from a query parameter string.</summary>
    /// <param name="value">The value.</param>
    /// <param name="spd">  The search parameter definition.</param>
    private void ExtractValues(
        string value,
        SearchParamDefinition spd)
    {
        List<SearchPrefixCodes?> prefixes = new();
        List<string> values = new();

        // parse parameter string, looking for multi-value
        int index = 0;
        while (index < value.Length)
        {
            int nextIndex = value.IndexOf(',', index);
            if (nextIndex == -1)
            {
                // unescape commas
                values.Add(value.Substring(index).Replace("\\,", ","));
                break;
            }

            // check for no content (e.g., ",,")
            if (nextIndex == (index + 1))
            {
                // ignore this value and continue
                continue;
            }

            // check to see if this comma is escaped
            if (value[nextIndex - 1] == '\\')
            {
                // do not move the start, keep looking for the next comma
                continue;
            }

            // unescape any escaped commas and add this value
            values.Add(value.Substring(index, nextIndex - index).Replace("\\,", ","));
            index = nextIndex + 1;
        }

        // check for prefixes
        switch (spd!.Type)
        {
            // parameter types that allow prefixes
            case SearchParamType.Number:
            case SearchParamType.Date:
            case SearchParamType.Quantity:
                for (int i = 0; i < values.Count; i++)
                {
                    if (values[i].Length < 2)
                    {
                        continue;
                    }

                    if (values[i].Substring(0, 2).TryFhirEnum(out SearchPrefixCodes prefix))
                    {
                        prefixes.Add(prefix);
                        values[i] = values[i].Substring(2);
                    }
                    else
                    {
                        prefixes.Add(null);
                    }
                }

                break;
            //case SearchParamType.String:
            //case SearchParamType.Token:
            //case SearchParamType.Reference:
            //case SearchParamType.Composite:
            //case SearchParamType.Uri:
            //case SearchParamType.Special:
            default:
                break;
        }

        // update our object with parsed values
        Prefixes = prefixes.ToArray();
        Values = values.ToArray();
    }

    /// <summary>
    /// Process the typed values and flag any values that fail expected parsing as unapplied.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="spd">  The search parameter definition.</param>
    private void ProcessTypedValues(string value, SearchParamDefinition spd)
    {
        if (!(Values?.Any() ?? false))
        {
            return;
        }

        // parse value types that require additional conversion
        switch (spd!.Type)
        {
            case SearchParamType.Date:
                {
                    ValueDateStarts = new DateTimeOffset[Values.Length];
                    ValueDateEnds = new DateTimeOffset[Values.Length];

                    for (int i = 0; i < Values.Length; i++)
                    {
                        if (TryParseDateString(Values[i], out DateTimeOffset start, out DateTimeOffset end))
                        {
                            ValueDateStarts[i] = start;
                            ValueDateEnds[i] = end;
                        }
                        else
                        {
                            IgnoredValueFlags[i] = true;
                        }
                    }
                }
                break;

            case SearchParamType.Number:
                {
                    // check for input decimal types
                    if (value.Contains('.') || value.Contains("e-", StringComparison.OrdinalIgnoreCase))
                    {
                        // use decimal
                        ValueDecimals = new decimal[Values.Length];

                        for (int i = 0; i < Values.Length; i++)
                        {
                            if (decimal.TryParse(Values[i], out decimal val))
                            {
                                ValueDecimals[i] = val;
                            }
                            else
                            {
                                IgnoredValueFlags[i] = true;
                            }
                        }
                    }
                    else
                    {
                        // use longs
                        ValueInts = new long[Values.Length];

                        for (int i = 0; i < Values.Length; i++)
                        {
                            if (long.TryParse(Values[i], out long val))
                            {
                                ValueInts[i] = val;
                            }
                            else
                            {
                                IgnoredValueFlags[i] = true;
                            }
                        }
                    }
                }
                break;

            case SearchParamType.Quantity:
                {
                    // use decimal for values
                    ValueDecimals = new decimal[Values.Length];
                    ValueFhirCodes = new Hl7.Fhir.ElementModel.Types.Code[Values.Length];

                    // traverse values
                    for (int i = 0; i < Values.Length; i++)
                    {
                        string[] components = Values[i].Split('|', StringSplitOptions.RemoveEmptyEntries);

                        // value is always first
                        if (decimal.TryParse(components[0], out decimal val))
                        {
                            ValueDecimals[i] = val;
                        }

                        switch (components.Length)
                        {
                            // value
                            case 1:
                                ValueFhirCodes[i] = new(string.Empty, string.Empty);
                                break;

                            // value and code / unit
                            case 2:
                                ValueFhirCodes[i] = new(string.Empty, components[1]);
                                break;

                            // value, system, and code / unit
                            case 3:
                                ValueFhirCodes[i] = new(components[1], components[2]);
                                break;

                            // unknown parsing result
                            default:
                                IgnoredValueFlags[i] = true;
                                break;
                        }
                    }
                }
                break;

            case SearchParamType.Token:
                {
                    // check for boolean tokens
                    if (Values.All(v => v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("false", StringComparison.OrdinalIgnoreCase)))
                    {
                        ValueBools = new bool[Values.Length];

                        // traverse values
                        for (int i = 0; i < Values.Length; i++)
                        {
                            _ = bool.TryParse(Values[i], out ValueBools[i]);
                        }
                    }

                    // tokens always represent a code and system
                    ValueFhirCodes = new Hl7.Fhir.ElementModel.Types.Code[Values.Length];
                    ValueFhirCodeTypes = new Hl7.Fhir.ElementModel.Types.Code[Values.Length];

                    // traverse values
                    for (int i = 0; i < Values.Length; i++)
                    {
                        string[] components = Values[i].Split('|');

                        switch (components.Length)
                        {
                            // code only (no system)
                            case 1:
                                ValueFhirCodes[i] = new(null, components[0]);
                                break;

                            // system|code
                            case 2:
                                ValueFhirCodes[i] = new(components[0], components[1]);
                                break;

                            // system|code|value
                            default:
                                ValueFhirCodeTypes[i] = new(components[0], components[1]);
                                ValueFhirCodes[i] = new(null, components[2]);
                                break;
                        }
                    }
                }
                break;

            case SearchParamType.Reference:
                {
                    ValueReferences = new SegmentedReference[Values.Length];

                    // traverse values
                    for (int i = 0; i < Values.Length; i++)
                    {
                        if (!TryParseReference(value, out ValueReferences[i]))
                        {
                            IgnoredValueFlags[i] = true;
                        }
                    }

                    // references can use identifiers - it is better to attempt parsing them here for sanity later
                    ValueFhirCodes = new Hl7.Fhir.ElementModel.Types.Code[Values.Length];

                    // traverse values
                    for (int i = 0; i < Values.Length; i++)
                    {
                        string[] components = Values[i].Split('|', StringSplitOptions.TrimEntries);

                        if (components.Length == 1)
                        {
                            ValueFhirCodes[i] = new(null, components[0]);
                        }
                        else
                        {
                            ValueFhirCodes[i] = new(components[0], components[1]);
                        }
                    }

                }
                break;

                //case SearchParamType.String:
                //case SearchParamType.Reference:
                //case SearchParamType.Composite:
                //case SearchParamType.Uri:
                //case SearchParamType.Special:
                //default:
                //    //{
                //    //    for (int i = 0; i < _values.Length; i++)
                //    //    {
                //    //        AppliedValueFlags[i] = true;
                //    //    }
                //    //}
                //    break;
        }
    }

    /// <summary>Enumerates parse in this collection.</summary>
    /// <param name="queryString">  The query string.</param>
    /// <param name="store">        The FHIR store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="resourceType"> Type of the resource.</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process parse in this collection.
    /// </returns>
    public static ParsedSearchParameter[] Parse(
        string queryString,
        VersionedFhirStore store,
        IVersionedResourceStore resourceStore,
        string resourceType)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return [];
        }

        List<ParsedSearchParameter> results = [];

        System.Collections.Specialized.NameValueCollection query = System.Web.HttpUtility.ParseQueryString(queryString);
        foreach (string key in query)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            SearchKeyParseResult? parseResult = tryParseKey(key, store, resourceStore, resourceType);
            if (parseResult is null)
            {
                if (ParsedResultParameters.SearchResultParameters.Contains(key))
                {
                    // ignore this parameter
                    continue;
                }

                Console.WriteLine($"Search Parameter {key} is not a known search parameter.");
                continue;
            }

            results.Add(new ParsedSearchParameter(
                store,
                resourceStore,
                parseResult,
                query[key] ?? string.Empty,
                key,
                query[key]));

            continue;
        }

        return results.ToArray();
    }

    /// <summary>Encapsulates the result of a search key parse.</summary>
    /// <param name="ResourceType">             Type of the resource.</param>
    /// <param name="SearchParameterName">      Name of the search parameter.</param>
    /// <param name="ModifierLiteral">          The modifier literal.</param>
    /// <param name="ModifierCode">             The modifier code.</param>
    /// <param name="SearchParameterDefinition">The search parameter definition.</param>
    /// <param name="ChainedKeys">              The chained keys.</param>
    /// <param name="ReverseLinkKey">           The reverse link key.</param>
    /// <param name="ReverseLinkFilter">        A filter specifying the reverse link.</param>
    private record SearchKeyParseResult(
        string ResourceType,
        string SearchParameterName,
        string ModifierLiteral,
        SearchModifierCodes ModifierCode,
        SearchParamDefinition? SearchParameterDefinition,
        SearchKeyParseResult[]? ChainedKeys,
        SearchKeyParseResult? ReverseLinkKey,
        SearchKeyParseResult? ReverseLinkFilter);

    /// <summary>
    /// Attempts to parse a key from the given data, returning a default value rather than throwing
    /// an exception if it fails.
    /// </summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="key">          The key.</param>
    /// <param name="store">        The FHIR store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="resourceType"> Type of the resource.</param>
    /// <returns>A SearchKeyParseResult?</returns>
    private static SearchKeyParseResult? tryParseKey(
        string key,
        IFhirStore store,
        IVersionedResourceStore resourceStore,
        string resourceType)
    {
        string spName;
        string modifierLiteral;
        SearchModifierCodes modifierCode;
        SearchParamDefinition? spDefinition;
        string chainedKey;
        SearchKeyParseResult[]? chainedResults;

        int colonIndex = key.IndexOf(':');
        int dotIndex = key.IndexOf('.');

        // check for no modifier and no chaining
        if ((colonIndex == -1) && (dotIndex == -1))
        {
            spName = key;
            modifierLiteral = string.Empty;
            modifierCode = SearchModifierCodes.None;
            chainedKey = string.Empty;
            //chainedResult = null;
        }

        // check for modifier and no chaining
        else if ((colonIndex != -1) && (dotIndex == -1))
        {
            spName = key.Substring(0, colonIndex);
            chainedKey = string.Empty;

            // sort out reverse chaining
            if (spName.Equals("_has", StringComparison.Ordinal))
            {
                string[] revComponents = key.Substring(colonIndex + 1).Split(':', '.');
                if (revComponents.Length < 2)
                {
                    // ignore this parameter
                    return null;
                }

                string revResourceName = revComponents[0];
                string revLinkParamName = revComponents[1];

                if (!store.ContainsKey(revResourceName))
                {
                    // ignore this parameter
                    return null;
                }

                SearchParamDefinition? linkDefinition;

                if (_allResourceParameters.ContainsKey(revLinkParamName))
                {
                    linkDefinition = _allResourceParameters[revLinkParamName];
                }
                else if (!((IVersionedResourceStore)store[revResourceName]).TryGetSearchParamDefinition(revLinkParamName, out linkDefinition))
                {
                    // no definition found
                    Console.WriteLine($"Unable to resolve _has link: {revResourceName}:{revLinkParamName} -> {resourceType}");
                    return null;
                }

                SearchKeyParseResult? reverseLinkKey = new SearchKeyParseResult(
                    revResourceName,
                    revLinkParamName,
                    string.Empty,
                    SearchModifierCodes.None,
                    linkDefinition,
                    null,
                    null,
                    null);

                int continuationStart = colonIndex + 1 + revResourceName.Length + revLinkParamName.Length + 2;

                if (continuationStart >= key.Length)
                {
                    Console.WriteLine($"Unable to parse _has parameter: {key}");
                    return null;
                }

                SearchKeyParseResult? reverseLinkFilter = tryParseKey(
                    key.Substring(continuationStart),
                    store,
                    (IVersionedResourceStore)store[revResourceName],
                    revResourceName);

                return new SearchKeyParseResult(
                    resourceType,
                    spName,
                    string.Empty,
                    SearchModifierCodes.None,
                    null,
                    null,
                    reverseLinkKey,
                    reverseLinkFilter);
            }

            modifierLiteral = key.Substring(colonIndex + 1);

            // check for being a resource name
            if (store.ContainsKey(modifierLiteral))
            {
                modifierCode = SearchModifierCodes.ResourceType;
            }
            else if (!modifierLiteral.TryFhirEnum(out modifierCode))
            {
                // TODO: need to fail query
                throw new Exception($"unknown modifier in query: {modifierLiteral} ({key})");
            }
        }

        // check for chaining and no modifier
        else if ((colonIndex == -1) && (dotIndex != -1))
        {
            spName = key.Substring(0, dotIndex);
            modifierLiteral = string.Empty;
            modifierCode = SearchModifierCodes.None;
            chainedKey = key.Substring(dotIndex + 1);
            //chainedResult = TryParseKey(key.Substring(dotIndex + 1), resourceStore);
        }

        // check for type filter followed by chain
        else if (colonIndex < dotIndex)
        {
            spName = key.Substring(0, colonIndex);

            // sort out reverse chaining
            if (spName.Equals("_has", StringComparison.Ordinal))
            {
                string[] revComponents = key.Substring(colonIndex + 1).Split(':', '.');
                if (revComponents.Length < 2)
                {
                    // ignore this parameter
                    return null;
                }

                string revResourceName = revComponents[0];
                string revLinkParamName = revComponents[1];

                if (!store.ContainsKey(revResourceName))
                {
                    // ignore this parameter
                    return null;
                }

                SearchParamDefinition? linkDefinition;

                if (_allResourceParameters.ContainsKey(revLinkParamName))
                {
                    linkDefinition = _allResourceParameters[revLinkParamName];
                }
                else if (!((IVersionedResourceStore)store[revResourceName]).TryGetSearchParamDefinition(revLinkParamName, out linkDefinition))
                {
                    // no definition found
                    Console.WriteLine($"Unable to resolve _has link: {revResourceName}:{revLinkParamName} -> {resourceType}");
                    return null;
                }

                SearchKeyParseResult? reverseLinkKey = new SearchKeyParseResult(
                    revResourceName,
                    revLinkParamName,
                    string.Empty,
                    SearchModifierCodes.None,
                    linkDefinition,
                    null,
                    null,
                    null);

                int continuationStart = colonIndex + 1 + revResourceName.Length + revLinkParamName.Length + 2;

                if (continuationStart >= key.Length)
                {
                    Console.WriteLine($"Unable to parse _has parameter: {key}");
                    return null;
                }

                SearchKeyParseResult? reverseLinkFilter = tryParseKey(
                    key.Substring(continuationStart),
                    store,
                    (IVersionedResourceStore)store[revResourceName],
                    revResourceName);

                return new SearchKeyParseResult(
                    resourceType,
                    spName,
                    string.Empty,
                    SearchModifierCodes.None,
                    null,
                    null,
                    reverseLinkKey,
                    reverseLinkFilter);
            }
            modifierLiteral = key.Substring(colonIndex + 1, dotIndex - colonIndex - 1);

            // only allow a resource filter here
            if (!store.ContainsKey(modifierLiteral))
            {
                // TODO: need to fail query
                throw new Exception($"unacceptable modifier used in chaining query: {modifierLiteral} ({key})");
            }

            modifierCode = SearchModifierCodes.ResourceType;
            chainedKey = key.Substring(dotIndex + 1);
            //chainedResult = TryParseKey(key.Substring(dotIndex + 1), resourceStore);
        }

        // check for chain first
        else
        {
            spName = key.Substring(0, dotIndex);
            modifierLiteral = string.Empty;
            modifierCode = SearchModifierCodes.None;
            chainedKey = key.Substring(dotIndex + 1);
            //chainedResult = TryParseKey(key.Substring(dotIndex + 1), resourceStore);
        }

        // Observation?subject.name=peter
        // Observation?subject:Patient.name=peter
        // Patient?general-practitioner.name=Joe&general-practitioner.address-state=MN
        // Patient?general-practitioner:Practitioner.name=Joe&general-practitioner:Practitioner.address-state=MN
        // Patient?_has:Observation:patient:code=1234-5
        // Patient?_has:Observation:patient:_has:AuditEvent:entity:agent=MyUserId
        // Encounter?patient._has:Group:member:_id=102

        // check for search result parameters, which are not search parameters
        if (ParsedResultParameters.SearchResultParameters.Contains(spName))
        {
            return null;
        }

        if (_allResourceParameters.TryGetValue(spName, out SearchParamDefinition? parameter))
        {
            spDefinition = parameter;
        }
        else if (!resourceStore.TryGetSearchParamDefinition(spName, out spDefinition))
        {
            // no definition found
            Console.WriteLine($"Unable to resolve search parameter: {spName} in resource store");
            return null;
        }

        if (string.IsNullOrEmpty(chainedKey))
        {
            chainedResults = null;
        }
        else
        {
            List<SearchKeyParseResult> perResourceChains = [];

            if ((modifierCode == SearchModifierCodes.ResourceType) &&
                store.ContainsKey(modifierLiteral))
            {
                SearchKeyParseResult? res = tryParseKey(
                    chainedKey,
                    store,
                    (IVersionedResourceStore)store[modifierLiteral],
                    modifierLiteral);

                if (res is not null)
                {
                    perResourceChains.Add(res);
                }
            }
            else if (spDefinition?.Target?.Any() ?? false)
            {
                foreach (VersionIndependentResourceTypesAll spTarget in spDefinition.Target)
                {
                    string rtName = spTarget is Enum te
                        ? EnumUtility.GetLiteral(te)
                        : spTarget.ToString();

                    if (store.ContainsKey(rtName))
                    {
                        SearchKeyParseResult? res = tryParseKey(
                            chainedKey,
                            store,
                            (IVersionedResourceStore)store[rtName],
                            rtName);

                        if (res is not null)
                        {
                            perResourceChains.Add(res);
                        }
                    }
                }
            }

            if (perResourceChains.Any())
            {
                chainedResults = perResourceChains.ToArray();
            }
            else
            {
                chainedResults = null;
            }
        }

        return new SearchKeyParseResult(
            resourceType,
            spName,
            modifierLiteral,
            modifierCode,
            spDefinition,
            chainedResults,
            null,
            null);
    }

    /// <summary>Parse reference common.</summary>
    /// <param name="reference">The reference.</param>
    /// <returns>A SegmentedReference.</returns>
    private bool TryParseReference(string reference, out SegmentedReference sr)
    {
        if (string.IsNullOrEmpty(reference))
        {
            sr = default;
            IgnoredReason ??= "Empty reference";
            return false;
        }

        string[] parts = reference.Split('/');

        string cv;
        string cu;

        int index = reference.LastIndexOf('|');

        if (index != -1)
        {
            cv = reference.Substring(index + 1);
            cu = reference.Substring(0, index);
        }
        else
        {
            cv = string.Empty;
            cu = reference;
        }

        switch (parts.Length)
        {
            case 1:
                sr = new SegmentedReference(string.Empty, parts[0], string.Empty, cv, cu);
                return true;

            case 2:
                sr = new SegmentedReference(parts[0], parts[1], string.Empty, cv, cu);
                return true;

            case 4:
                if (parts[2].Equals("_history", StringComparison.Ordinal))
                {
                    sr = new SegmentedReference(parts[0], parts[1], parts[3], cv, cu);
                    return true;
                }
                break;
        }

        int len = parts.Length;

        // second to last is history literal
        if (parts[len - 2].Equals("_history", StringComparison.Ordinal))
        {
            sr = new SegmentedReference(parts[len - 4], parts[len - 3], parts[len - 1], cv, cu);
            return true;
        }

        sr = new SegmentedReference(string.Empty, string.Empty, string.Empty, cv, cu);
        return true;
    }

    /// <summary>Attempts to parse a date string.</summary>
    /// <param name="dateString">The date string.</param>
    /// <param name="start">     [out] The start.</param>
    /// <param name="end">       [out] The end.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParseDateString(string dateString, out DateTimeOffset start, out DateTimeOffset end)
    {
        if (string.IsNullOrEmpty(dateString))
        {
            start = DateTimeOffset.MinValue;
            end = DateTimeOffset.MaxValue;
            IgnoredReason ??= "Empty date string";
            return false;
        }

        // need to check for just year because DateTime refuses to parse that
        if (dateString.Length == 4)
        {
            start = new DateTimeOffset(int.Parse(dateString), 1, 1, 0, 0, 0, TimeSpan.Zero);
            end = start.AddYears(1).AddTicks(-1);
            return true;
        }

        // note that we are using DateTime and converting to DateTimeOffset to work through TZ stuff without manually parsing each format precision
        if (!DateTime.TryParse(dateString, null, DateTimeStyles.RoundtripKind, out DateTime dt))
        {
            Console.WriteLine($"Failed to parse date: {dateString}");
            IgnoredReason ??= $"Invalid date format: {dateString}";
            start = DateTimeOffset.MinValue;
            end = DateTimeOffset.MaxValue;
            return false;
        }

        start = new DateTimeOffset(dt, TimeSpan.Zero);

        switch (dateString.Length)
        {
            // YYYY
            case 4:
                end = start.AddYears(1).AddTicks(-1);
                break;

            // YYYY-MM
            case 7:
                end = start.AddMonths(1).AddTicks(-1);
                break;

            // YYYY-MM-DD
            case 10:
                end = start.AddDays(1).AddTicks(-1);
                break;

            // Note: this is not defined as valid, but wanted to support it
            // YYYY-MM-DDThh
            case 13:
                end = start.AddHours(1).AddTicks(-1);
                break;

            // YYYY-MM-DDThh:mm
            case 16:
                end = start.AddMinutes(1).AddTicks(-1);
                break;

            // Note: servers are allowed to ignore fractional seconds - I am choosing to do so.

            // YYYY-MM-DDThh:mm:ss
            case 19:
            // YYYY-MM-DDThh:mm:ssZ
            case 20:
            // YYYY-MM-DDThh:mm:ss+zz
            // YYYY-MM-DDThh:mm:ss.fZ
            case 22:
            // YYYY-MM-DDThh:mm:ss.ffZ
            case 23:
            // YYYY-MM-DDThh:mm:ss.fffZ
            case 24:
            // YYYY-MM-DDThh:mm:ss+zz:zz
            // YYYY-MM-DDThh:mm:ss.ffffZ
            case 25:
            // YYYY-MM-DDThh:mm:ss.f+zz:zz
            case 27:
            // YYYY-MM-DDThh:mm:ss.ff+zz:zz
            case 28:
            // YYYY-MM-DDThh:mm:ss.fff+zz:zz
            case 29:
            // YYYY-MM-DDThh:mm:ss.ffff+zz:zz
            case 30:
                end = start.AddSeconds(1).AddTicks(-1);
                break;

            default:
                Console.WriteLine($"Invalid date format: {dateString}");
                IgnoredReason ??= $"Invalid date format: {dateString}";
                start = DateTimeOffset.MinValue;
                end = DateTimeOffset.MaxValue;
                return false;
        }

        return true;
    }

    /// <summary>Attempts to parse a date string.</summary>
    /// <param name="dateString">The date string.</param>
    /// <param name="start">     [out] The start.</param>
    /// <param name="end">       [out] The end.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public static bool TryParseFhirDate(string? dateString, out DateTimeOffset start, out DateTimeOffset end)
    {
        if (string.IsNullOrEmpty(dateString))
        {
            start = DateTimeOffset.MinValue;
            end = DateTimeOffset.MaxValue;
            return false;
        }

        // need to check for just year because DateTime refuses to parse that
        if (dateString.Length == 4)
        {
            start = new DateTimeOffset(int.Parse(dateString), 1, 1, 0, 0, 0, TimeSpan.Zero);
            end = start.AddYears(1).AddTicks(-1);
            return true;
        }

        // note that we are using DateTime and converting to DateTimeOffset to work through TZ stuff without manually parsing each format precision
        if (!DateTime.TryParse(dateString, null, DateTimeStyles.RoundtripKind, out DateTime dt))
        {
            Console.WriteLine($"Failed to parse date: {dateString}");
            start = DateTimeOffset.MinValue;
            end = DateTimeOffset.MaxValue;
            return false;
        }

        start = new DateTimeOffset(dt, TimeSpan.Zero);

        switch (dateString.Length)
        {
            // YYYY
            case 4:
                end = start.AddYears(1).AddTicks(-1);
                break;

            // YYYY-MM
            case 7:
                end = start.AddMonths(1).AddTicks(-1);
                break;

            // YYYY-MM-DD
            case 10:
                end = start.AddDays(1).AddTicks(-1);
                break;

            // Note: this is not defined as valid, but wanted to support it
            // YYYY-MM-DDThh
            case 13:
                end = start.AddHours(1).AddTicks(-1);
                break;

            // YYYY-MM-DDThh:mm
            case 16:
                end = start.AddMinutes(1).AddTicks(-1);
                break;

            // Note: servers are allowed to ignore fractional seconds - I am choosing to do so.

            // YYYY-MM-DDThh:mm:ss
            case 19:
            // YYYY-MM-DDThh:mm:ssZ
            case 20:
            // YYYY-MM-DDThh:mm:ss+zz
            // YYYY-MM-DDThh:mm:ss.fZ
            case 22:
            // YYYY-MM-DDThh:mm:ss.ffZ
            case 23:
            // YYYY-MM-DDThh:mm:ss.fffZ
            case 24:
            // YYYY-MM-DDThh:mm:ss+zz:zz
            // YYYY-MM-DDThh:mm:ss.ffffZ
            case 25:
            // YYYY-MM-DDThh:mm:ss.f+zz:zz
            case 27:
            // YYYY-MM-DDThh:mm:ss.ff+zz:zz
            case 28:
            // YYYY-MM-DDThh:mm:ss.fff+zz:zz
            case 29:
            // YYYY-MM-DDThh:mm:ss.ffff+zz:zz
            case 30:
                end = start.AddSeconds(1).AddTicks(-1);
                break;

            default:
                Console.WriteLine($"Invalid date format: {dateString}");
                start = DateTimeOffset.MinValue;
                end = DateTimeOffset.MaxValue;
                return false;
        }

        return true;
    }

    public object Clone() => new ParsedSearchParameter(this);
}

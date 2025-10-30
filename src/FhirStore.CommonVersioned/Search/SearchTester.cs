// <copyright file="SearchTester.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using FhirCandle.Storage;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using System;
using System.ComponentModel;
using static FhirCandle.Search.SearchDefinitions;

namespace FhirCandle.Search;

/// <summary>Test parsed search parameters against resources.</summary>
public class SearchTester
{
    /// <summary>Gets or sets the store.</summary>
    public required VersionedFhirStore FhirStore { get; init; }

    /// <summary>
    /// Tests a resource against parsed search parameters for matching.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    /// <param name="rootNode">The resource.</param>
    /// <param name="searchParameters">Options for controlling the search.</param>
    /// <param name="fpContext">(Optional) The context.</param>
    /// <param name="reverseChainCache"></param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public bool TestForMatch(
        PocoNode rootNode,
        IEnumerable<ParsedSearchParameter> searchParameters,
        FhirEvaluationContext? fpContext = null,
        Dictionary<string, Resource[]>? reverseChainCache = null)
    {
        if (rootNode is null)
        {
            throw new ArgumentNullException(nameof(rootNode));
        }

        if (!searchParameters.Any())
        {
            return true;
        }

        fpContext ??= new FhirEvaluationContext()
        {
            Resource = rootNode,
            TerminologyService = FhirStore.Terminology,
            ElementResolver = FhirStore.Resolve,
        };


        string rootResourceType = rootNode.GetResourceTypeIndicator();

        foreach (ParsedSearchParameter sp in searchParameters)
        {
            // skip if this is ignored
            if (sp.IgnoredParameter)
            {
                continue;
            }

            // for reverse chaining, we nest the search instead of evaluating it here
            if ((sp.ReverseChainedParameterLink is not null) && (sp.ReverseChainedParameterFilter is not null))
            {
                reverseChainCache ??= [];

                string rcKey = sp.ReverseChainedParameterLink.ResourceType + "." + sp.ReverseChainedParameterLink.Name;

                if (!reverseChainCache.TryGetValue(rcKey, out Resource[]? reverseChainMatches))
                {
                    reverseChainMatches = FhirStore.DoNestedTypeSearch(sp.ReverseChainedParameterLink.ResourceType, [sp.ReverseChainedParameterFilter]);
                    reverseChainCache[rcKey] = reverseChainMatches;
                }

                // extract the ID from this node
                string id = rootNode.Child("id")?.FirstOrDefault()?.GetString() ?? string.Empty;

                ParsedSearchParameter qualifiedLink = new ParsedSearchParameter(sp.ReverseChainedParameterLink);
                qualifiedLink.Values = [ rootResourceType + "/" + id ];
                qualifiedLink.ValueReferences = [ new(rootResourceType, id, string.Empty, string.Empty, string.Empty) ];
                qualifiedLink.IgnoredValueFlags = [ false ];
                qualifiedLink.IgnoredParameter = false;

                //Console.WriteLine($"Reverse chaining testing" +
                //    $" resource {sp.ReverseChainedParameterLink.ResourceType}.{sp.ReverseChainedParameterLink.ParamType}={rootNode.InstanceType!}/{id} and" +
                //    $" {sp.ReverseChainedParameterFilter.ResourceType}.{sp.ReverseChainedParameterFilter.Name}={sp.ReverseChainedParameterFilter.Values.FirstOrDefault()}");

                foreach (Resource reverseChainResource in reverseChainMatches)
                {
                    // test to see if this matches
                    if (!TestForMatch(reverseChainResource.ToPocoNode(), [qualifiedLink]))
                    {
                        return false;
                    }
                }

                // matched all reverse chain resources
                continue;
            }

            if (sp.CompiledExpression is null)
            {
                // TODO: Handle non-trivial search parameters
                continue;
            }

            // nest into composite search parameters
            if (sp.ParamType == SearchParamType.Composite)
            {
                if (sp.CompositeComponents is null)
                {
                    continue;
                }

                IEnumerable<PocoNode> compositeRoots = sp.CompiledExpression.Invoke(rootNode, fpContext);

                // test for matches against all composite components
                foreach (PocoNode compositeRoot in compositeRoots)
                {
                    // test the composite component against the composite root
                    if (TestForMatch(compositeRoot, sp.CompositeComponents, fpContext))
                    {
                        return true;
                    }
                }

                // if we did not find a tree that matches all components, this is not a match
                return false;
            }

            // check for unsupported modifiers
            if ((!string.IsNullOrEmpty(sp.ModifierLiteral)) &&
                (!IsModifierValidForType(sp.Modifier, sp.ParamType)))
            {
                continue;
            }

            IEnumerable<PocoNode> extracted;

            // either use FHIRPath to extract, or override for known special extractions
            switch (sp.Name)
            {
                case "_id":
                    {
                        if (rootNode.Child("id")?.FirstOrDefault() is PocoNode epn)
                        {
                            extracted = [ epn ];
                        }
                        else
                        {
                            extracted = [];
                        }
                    }

                    break;

                case "_type":
                    extracted = [ new FhirString(rootResourceType).ToPocoNode() ];
                    break;

                default:
                    extracted = sp.CompiledExpression.Invoke(rootNode, fpContext);
                    break;
            }

            if (!extracted.Any())
            {
                // check if we are looking for missing values - successful match
                if ((sp.Modifier == SearchModifierCodes.Missing) &&
                    sp.Values.Any(v => v.StartsWith("t", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                return false;
            }

            bool found = false;

            // for chaining, we nest the search instead of evaluating it here
            if (sp.ChainedParameters?.Any() ?? false)
            {
                // loop over any extracted values and test them against the chained parameters
                foreach (PocoNode node in extracted)
                {
                    // TODO(ginoc): add support for chaining into canonical references (QuestionnaireResponse.questionnaire case)
                    if ((node.Poco is not ResourceReference r) ||
                        string.IsNullOrEmpty(r.Reference))
                    {
                        continue;
                    }

                    PocoNode? resolved = FhirStore.Resolve(r.Reference);

                    if (resolved is null)
                    {
                        continue;
                    }

                    FhirEvaluationContext chainedContext = new FhirEvaluationContext()
                    {
                        Resource = resolved,
                        TerminologyService = FhirStore.Terminology,
                        ElementResolver = FhirStore.Resolve,
                    };

                    string rt = resolved.GetResourceTypeIndicator() ?? "Resource";

                    if (sp.ChainedParameters.ContainsKey(rt))
                    {
                        found = TestForMatch(resolved, [ sp.ChainedParameters[rt] ], chainedContext);
                    }
                    else if (sp.ChainedParameters.ContainsKey("Resource"))
                    {
                        found = TestForMatch(resolved, [ sp.ChainedParameters["Resource"] ], chainedContext);
                    }

                    if (found)
                    {
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }

                continue;
            }

            // loop over all extracted nodes until we find a match
            foreach (PocoNode resultNode in extracted)
            {
                if (TestNode(sp, resultNode))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // no matches in any extracted value means a parameter did NOT match
                return false;
            }
        }

        // successfully matching all parameters means this resource is a match
        return true;
    }

    private bool TestNode(ParsedSearchParameter sp, PocoNode resultNode)
    {
        // all types evaluate missing the same way
        if (sp.Modifier == SearchModifierCodes.Missing)
        {
            if (SearchTestMissing(resultNode, sp))
            {
                return true;
            }

            return false;
        }

        string nodeType = resultNode.Poco.TypeName
            ?? resultNode.ToTypedElement().InstanceType
            ?? throw new Exception("Failed to resolve type for node");

        // TODO: should I convert this to tuples for the switch?
        // build a routing tuple: {search type}<-{modifier}>-{value type}
        string combined = sp.Modifier == SearchModifierCodes.None
            ? $"{sp.ParamType}-{nodeType}".ToLowerInvariant()
            : $"{sp.ParamType}-{sp.Modifier}-{nodeType}".ToLowerInvariant();

        // this switch is intentionally 'unrolled' for performance (instead of nesting by type)
        // the 'missing' modifier is handled earlier so never appears in this switch
        switch (combined)
        {
            case "date-date":
            case "date-datetime":
            case "date-instant":
            case "date-period":
            case "date-timing":
                return EvalDateSearch.TestDate(resultNode, sp);

            // note that the SDK keeps all 'integer' values in 64-bit format
            case "number-integer":
            case "number-unsignedint":
            case "number-positiveint":
            case "number-integer64":
                return EvalNumberSearch.TestNumberAgainstLong(resultNode, sp);

            case "number-decimal":
                return EvalNumberSearch.TestNumberAgainstDecimal(resultNode, sp);

            case "quantity-quantity":
                return EvalQuantitySearch.TestQuantity(resultNode, sp);

            case "reference-canonical":
            case "reference-uri":
            case "reference-url":
                return EvalReferenceSearch.TestReferenceAgainstPrimitive(resultNode, sp);

            case "reference-reference":
                return EvalReferenceSearch.TestReference(resultNode, sp);

            case "reference-oid":
                return EvalReferenceSearch.TestReferenceAgainstOid(resultNode, sp);

            case "reference-uuid":
                return EvalReferenceSearch.TestReferenceAgainstUuid(resultNode, sp);

            // note that mapping identifier to canonical is specifically disallowed
            // (see https://hl7.org/fhir/search.html#modifieridentifier)
            //case "reference-identifier-canonical":
            case "reference-identifier-reference":
            case "reference-identifier-oid":
            case "reference-identifier-uri":
            case "reference-identifier-url":
            case "reference-identifier-uuid":
                return EvalReferenceSearch.TestReferenceIdentifier(resultNode, sp);

            // note: the literals used are 'actual' resource types (e.g., patient)
            case "reference-resourcetype-canonical":
            case "reference-resourcetype-uri":
            case "reference-resourcetype-url":
                return EvalReferenceSearch.TestReferenceAgainstPrimitive(resultNode, sp, sp.ModifierLiteral!);

            // note: the literals used are 'actual' resource types (e.g., patient)
            case "reference-resourcetype-reference":
                return EvalReferenceSearch.TestReference(resultNode, sp, sp.ModifierLiteral!);

            // note: the literals used are 'actual' resource types (e.g., patient)
            case "reference-resourcetype-oid":
                return EvalReferenceSearch.TestReferenceAgainstOid(resultNode, sp, sp.ModifierLiteral!);

            // note: the literals used are 'actual' resource types (e.g., patient)
            case "reference-resourcetype-uuid":
                return EvalReferenceSearch.TestReferenceAgainstUuid(resultNode, sp, sp.ModifierLiteral!);

            case "string-id":
            case "string-string":
            case "string-markdown":
            case "string-xhtml":
                return EvalStringSearch.TestStringStartsWith(resultNode, sp);

            case "string-contains-id":
            case "string-contains-string":
            case "string-contains-markdown":
            case "string-contains-xhtml":
                return EvalStringSearch.TestStringContains(resultNode, sp);

            case "string-exact-id":
            case "string-exact-string":
            case "string-exact-markdown":
            case "string-exact-xhtml":
                return EvalStringSearch.TestStringExact(resultNode, sp);

            case "string-humanname":
                return EvalStringSearch.TestStringStartsWithAgainstHumanName(resultNode, sp);

            case "string-contains-humanname":
                return EvalStringSearch.TestStringContainsAgainstHumanName(resultNode, sp);

            case "string-exact-humanname":
                return EvalStringSearch.TestStringExactAgainstHumanName(resultNode, sp);

            case "string-address":
                return EvalStringSearch.TestStringStartsWithAgainstAddress(resultNode, sp);

            case "string-contains-address":
                return EvalStringSearch.TestStringContainsAgainstAddress(resultNode, sp);

            case "string-exact-address":
                return EvalStringSearch.TestStringExactAgainstAddress(resultNode, sp);

            case "token-canonical":
            case "token-id":
            case "token-oid":
            case "token-uri":
            case "token-url":
            case "token-uuid":
            case "token-string":
                return EvalTokenSearch.TestTokenAgainstStringValue(resultNode, sp);

            case "token-not-canonical":
            case "token-not-id":
            case "token-not-oid":
            case "token-not-uri":
            case "token-not-url":
            case "token-not-uuid":
            case "token-not-string":
                return EvalTokenSearch.TestTokenNotAgainstStringValue(resultNode, sp);

            case "token-boolean":
                return EvalTokenSearch.TestTokenAgainstBool(resultNode, sp);

            case "token-not-boolean":
                return EvalTokenSearch.TestTokenNotAgainstBool(resultNode, sp);

            case "token-code":
            case "token-coding":
            case "token-contactpoint":
            case "token-identifier":
                return EvalTokenSearch.TestTokenAgainstCoding(resultNode, sp);

            case "token-not-code":
            case "token-not-coding":
            case "token-not-contactpoint":
            case "token-not-identifier":
                return EvalTokenSearch.TestTokenNotAgainstCoding(resultNode, sp);

            case "token-codeableconcept":
                return EvalTokenSearch.TestTokenAgainstCodeableConcept(resultNode, sp);

            case "token-in-codeableconcept":
                return EvalTokenSearch.TestTokenInCodeableConcept(resultNode, sp, FhirStore);

            case "token-in-code":
            case "token-in-coding":
            case "token-in-contactpoint":
            case "token-in-identifier":
                return EvalTokenSearch.TestTokenInCoding(resultNode, sp, FhirStore);

            case "uri-canonical":
            case "uri-uri":
            case "uri-url":
                return EvalUriSearch.TestUriAgainstStringValue(resultNode, sp);

            case "uri-oid":
                return EvalUriSearch.TestUriAgainstOid(resultNode, sp);

            case "uri-uuid":
                return EvalUriSearch.TestUriAgainstUuid(resultNode, sp);

            case "token-oftype-identifier":
                return EvalTokenSearch.TestTokenOfTypeIdentifier(resultNode, sp);

            case "reference-above-canonical":
                return EvalReferenceSearch.TestReferenceAboveCanonical(resultNode, sp);
            case "reference-above-oid":
                return EvalReferenceSearch.TestReferenceAboveOid(resultNode, sp);
            case "reference-above-uri":
                return EvalReferenceSearch.TestReferenceAboveUri(resultNode, sp);
            case "reference-above-url":
                return EvalReferenceSearch.TestReferenceAboveUrl(resultNode, sp);
            case "reference-below-canonical":
                return EvalReferenceSearch.TestReferenceBelowCanonical(resultNode, sp);
            case "reference-below-oid":
                return EvalReferenceSearch.TestReferenceBelowOid(resultNode, sp);
            case "reference-below-uri":
                return EvalReferenceSearch.TestReferenceBelowUri(resultNode, sp);
            case "reference-below-url":
                return EvalReferenceSearch.TestReferenceBelowUrl(resultNode, sp);
            case "reference-codetext-canonical":
                return EvalReferenceSearch.TestReferenceCodeTextCanonical(resultNode, sp);
            case "reference-codetext-oid":
                return EvalReferenceSearch.TestReferenceCodeTextOid(resultNode, sp);
            case "reference-codetext-uri":
                return EvalReferenceSearch.TestReferenceCodeTextUri(resultNode, sp);
            case "reference-codetext-url":
                return EvalReferenceSearch.TestReferenceCodeTextUrl(resultNode, sp);
            case "reference-codetext-uuid":
                return EvalReferenceSearch.TestReferenceCodeTextUuid(resultNode, sp);
            case "reference-in-uri":
                return EvalReferenceSearch.TestReferenceInUri(resultNode, sp, FhirStore);
            case "reference-in-url":
                return EvalReferenceSearch.TestReferenceInUrl(resultNode, sp, FhirStore);
            case "reference-notin-uri":
                return EvalReferenceSearch.TestReferenceNotInUri(resultNode, sp, FhirStore);
            case "reference-notin-url":
                return EvalReferenceSearch.TestReferenceNotInUrl(resultNode, sp, FhirStore);
            case "token-above-oid":
                return EvalTokenSearch.TestTokenAboveOid(resultNode, sp, FhirStore);
            case "token-above-uri":
                return EvalTokenSearch.TestTokenAboveUri(resultNode, sp, FhirStore);
            case "token-above-url":
                return EvalTokenSearch.TestTokenAboveUrl(resultNode, sp, FhirStore);
            case "token-below-oid":
                return EvalTokenSearch.TestTokenBelowOid(resultNode, sp, FhirStore);
            case "token-below-uri":
                return EvalTokenSearch.TestTokenBelowUri(resultNode, sp, FhirStore);
            case "token-below-url":
                return EvalTokenSearch.TestTokenBelowUrl(resultNode, sp, FhirStore);
            case "token-codetext-code":
                return EvalTokenSearch.TestTokenCodeTextCode(resultNode, sp);
            case "token-codetext-coding":
                return EvalTokenSearch.TestTokenCodeTextCoding(resultNode, sp);
            case "token-codetext-codeableconcept":
                return EvalTokenSearch.TestTokenCodeTextCodeableConcept(resultNode, sp);
            case "token-codetext-identifier":
                return EvalTokenSearch.TestTokenCodeTextIdentifier(resultNode, sp);
            case "token-codetext-contactpoint":
                return EvalTokenSearch.TestTokenCodeTextContactPoint(resultNode, sp);
            case "token-codetext-canonical":
                return EvalTokenSearch.TestTokenCodeTextCanonical(resultNode, sp);
            case "token-codetext-oid":
                return EvalTokenSearch.TestTokenCodeTextOid(resultNode, sp);
            case "token-codetext-uri":
                return EvalTokenSearch.TestTokenCodeTextUri(resultNode, sp);
            case "token-codetext-url":
                return EvalTokenSearch.TestTokenCodeTextUrl(resultNode, sp);
            case "token-codetext-uuid":
                return EvalTokenSearch.TestTokenCodeTextUuid(resultNode, sp);
            case "token-codetext-string":
                return EvalTokenSearch.TestTokenCodeTextString(resultNode, sp);
            case "token-notin-code":
                return EvalTokenSearch.TestTokenNotInCode(resultNode, sp, FhirStore);
            case "token-notin-coding":
                return EvalTokenSearch.TestTokenNotInCoding(resultNode, sp, FhirStore);
            case "token-notin-codeableconcept":
                return EvalTokenSearch.TestTokenNotInCodeableConcept(resultNode, sp, FhirStore);
            case "token-notin-identifier":
                return EvalTokenSearch.TestTokenNotInIdentifier(resultNode, sp, FhirStore);
            case "token-notin-contactpoint":
                return EvalTokenSearch.TestTokenNotInContactPoint(resultNode, sp, FhirStore);
            case "token-notin-canonical":
                return EvalTokenSearch.TestTokenNotInCanonical(resultNode, sp, FhirStore);
            case "token-notin-oid":
                return EvalTokenSearch.TestTokenNotInOid(resultNode, sp, FhirStore);
            case "token-notin-uri":
                return EvalTokenSearch.TestTokenNotInUri(resultNode, sp, FhirStore);
            case "token-notin-url":
                return EvalTokenSearch.TestTokenNotInUrl(resultNode, sp, FhirStore);
            case "token-notin-uuid":
                return EvalTokenSearch.TestTokenNotInUuid(resultNode, sp, FhirStore);
            case "token-notin-string":
                return EvalTokenSearch.TestTokenNotInString(resultNode, sp, FhirStore);
            case "token-text-coding":
                return EvalTokenSearch.TestTokenTextCoding(resultNode, sp);
            case "token-text-codeableconcept":
                return EvalTokenSearch.TestTokenTextCodeableConcept(resultNode, sp);
            case "token-text-identifier":
                return EvalTokenSearch.TestTokenTextIdentifier(resultNode, sp);
            case "token-text-string":
                return EvalTokenSearch.TestTokenTextString(resultNode, sp);
            case "token-textadvanced-coding":
                return EvalTokenSearch.TestTokenTextAdvancedCoding(resultNode, sp);
            case "token-textadvanced-codeableconcept":
                return EvalTokenSearch.TestTokenTextAdvancedCodeableConcept(resultNode, sp);
            case "token-textadvanced-identifier":
                return EvalTokenSearch.TestTokenTextAdvancedIdentifier(resultNode, sp);
            case "token-textadvanced-string":
                return EvalTokenSearch.TestTokenTextAdvancedString(resultNode, sp);

            case "token-in-canonical":
            case "token-in-oid":
            case "token-in-uri":
            case "token-in-url":
            case "token-in-uuid":
            case "token-in-string":
            case "token-not-codeableconcept":
            case "uri-above-canonical":
                return EvalUriSearch.TestUriAboveCanonical(resultNode, sp);
            case "uri-above-uri":
                return EvalUriSearch.TestUriAboveUri(resultNode, sp);
            case "uri-above-url":
                return EvalUriSearch.TestUriAboveUrl(resultNode, sp);
            case "uri-below-canonical":
                return EvalUriSearch.TestUriBelowCanonical(resultNode, sp);
            case "uri-below-uri":
                return EvalUriSearch.TestUriBelowUri(resultNode, sp);
            case "uri-below-url":
                return EvalUriSearch.TestUriBelowUrl(resultNode, sp);
            case "uri-contains-canonical":
                return EvalUriSearch.TestUriContainsCanonical(resultNode, sp);
            case "uri-contains-oid":
                return EvalUriSearch.TestUriContainsOid(resultNode, sp);
            case "uri-contains-uri":
                return EvalUriSearch.TestUriContainsUri(resultNode, sp);
            case "uri-contains-url":
                return EvalUriSearch.TestUriContainsUrl(resultNode, sp);
            case "uri-contains-uuid":
                return EvalUriSearch.TestUriContainsUuid(resultNode, sp);
            case "uri-not-canonical":
                return EvalUriSearch.TestUriNotCanonical(resultNode, sp);
            case "uri-not-oid":
                return EvalUriSearch.TestUriNotOid(resultNode, sp);
            case "uri-not-uri":
                return EvalUriSearch.TestUriNotUri(resultNode, sp);
            case "uri-not-url":
                return EvalUriSearch.TestUriNotUrl(resultNode, sp);
            case "uri-not-uuid":
                return EvalUriSearch.TestUriNotUuid(resultNode, sp);

            // Not implemented
            case "token-above-code":
            case "token-above-coding":
            case "token-above-codeableconcept":
            case "token-above-identifier":
            case "token-above-contactpoint":
            case "token-above-canonical":

            case "token-above-uuid":
            case "token-above-string":
            case "token-below-code":
            case "token-below-coding":
            case "token-below-codeableconcept":
            case "token-below-identifier":
            case "token-below-contactpoint":
            case "token-below-canonical":

            case "token-below-uuid":
            case "token-below-string":

            case "token-text-code":
            case "token-textadvanced-code":

            case "token-textadvanced-contactpoint":
            case "token-textadvanced-canonical":
            case "token-textadvanced-oid":
            case "token-textadvanced-uri":
            case "token-textadvanced-url":
            case "token-textadvanced-uuid":

            case "uri-above-oid":
            case "uri-above-uuid":
            case "uri-below-oid":
            case "uri-below-uuid":

            case "uri-in-canonical":
            case "uri-in-oid":
            case "uri-in-uri":
            case "uri-in-url":
            case "uri-in-uuid":

            case "uri-notin-canonical":
            case "uri-notin-oid":
            case "uri-notin-uri":
            case "uri-notin-url":
            case "uri-notin-uuid":

            case "uri-oftype-canonical":
            case "uri-oftype-oid":
            case "uri-oftype-uri":
            case "uri-oftype-url":
            case "uri-oftype-uuid":

            case "uri-text-canonical":
            case "uri-text-oid":
            case "uri-text-uri":
            case "uri-text-url":
            case "uri-text-uuid":

            case "uri-textadvanced-canonical":
            case "uri-textadvanced-oid":
            case "uri-textadvanced-uri":
            case "uri-textadvanced-url":
            case "uri-textadvanced-uuid":

            case "token-text-contactpoint":
            case "token-text-canonical":
            case "token-text-oid":
            case "token-text-uri":
            case "token-text-url":
            case "token-text-uuid":

            case "reference-above-reference":
            case "reference-above-uuid":
            case "reference-below-reference":
            case "reference-below-uuid":
            case "reference-codetext-reference":
            case "reference-in-canonical":
            case "reference-in-reference":
            case "reference-in-oid":
            case "reference-in-uuid":
            case "reference-notin-canonical":
            case "reference-notin-reference":
            case "reference-notin-oid":
            case "reference-notin-uuid":
            case "reference-text-canonical":
            case "reference-text-reference":
            case "reference-text-oid":
            case "reference-text-uri":
            case "reference-text-url":
            case "reference-text-uuid":
            case "reference-textadvanced-canonical":
            case "reference-textadvanced-reference":
            case "reference-textadvanced-oid":
            case "reference-textadvanced-uri":
            case "reference-textadvanced-url":
            case "reference-textadvanced-uuid":

            // Note that there is no defined way to search for a time
            //case "date-time":
            default:
                return false;

        }
    }

    /// <summary>Searches for the first test missing.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public static bool SearchTestMissing(PocoNode? valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Poco is null)
        {
            return true;
        }

        bool positive = sp.Values.Any(v => v.StartsWith("t", StringComparison.OrdinalIgnoreCase));
        bool negative = sp.Values.Any(v => v.StartsWith("f", StringComparison.OrdinalIgnoreCase));

        // testing both missing and not missing is always true
        if (positive && negative)
        {
            return true;
        }

        // test for missing and a null value
        if (positive && (valueNode.Poco is null))
        {
            return true;
        }

        // test for not missing and not a null value
        if (negative && (valueNode?.GetValue() is not null))
        {
            return true;
        }

        // other combinations are search misses
        return false;
    }
}

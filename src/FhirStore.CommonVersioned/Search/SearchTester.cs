// <copyright file="SearchTester.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using FhirCandle.Storage;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
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
        ITypedElement rootNode,
        IEnumerable<ParsedSearchParameter> searchParameters,
        FhirEvaluationContext? fpContext = null,
        Dictionary<string, Resource[]>? reverseChainCache = null)
    {
        if (rootNode == null)
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

        foreach (ParsedSearchParameter sp in searchParameters)
        {
            // skip if this is ignored
            if (sp.IgnoredParameter)
            {
                continue;
            }

            // for reverse chaining, we nest the search instead of evaluating it here
            if ((sp.ReverseChainedParameterLink != null) && (sp.ReverseChainedParameterFilter != null))
            {
                reverseChainCache ??= [];

                string rcKey = sp.ReverseChainedParameterLink.ResourceType + "." + sp.ReverseChainedParameterLink.Name;

                if (!reverseChainCache.TryGetValue(rcKey, out Resource[]? reverseChainMatches))
                {
                    reverseChainMatches = FhirStore.DoNestedTypeSearch(sp.ReverseChainedParameterLink.ResourceType, [sp.ReverseChainedParameterFilter]);
                    reverseChainCache[rcKey] = reverseChainMatches;
                }

                // extract the ID from this node
                string id = rootNode.Children("id").FirstOrDefault()?.Value?.ToString() ?? string.Empty;

                ParsedSearchParameter qualifiedLink = new ParsedSearchParameter(sp.ReverseChainedParameterLink);
                qualifiedLink.Values = [ rootNode.InstanceType! + "/" + id ];
                qualifiedLink.ValueReferences = [ new(rootNode.InstanceType!, id, string.Empty, string.Empty, string.Empty) ];
                qualifiedLink.IgnoredValueFlags = [ false ];
                qualifiedLink.IgnoredParameter = false;

                //Console.WriteLine($"Reverse chaining testing" +
                //    $" resource {sp.ReverseChainedParameterLink.ResourceType}.{sp.ReverseChainedParameterLink.ParamType}={rootNode.InstanceType!}/{id} and" +
                //    $" {sp.ReverseChainedParameterFilter.ResourceType}.{sp.ReverseChainedParameterFilter.Name}={sp.ReverseChainedParameterFilter.Values.FirstOrDefault()}");

                foreach (Resource reverseChainResource in reverseChainMatches)
                {
                    // test to see if this matches
                    if (!TestForMatch(reverseChainResource.ToTypedElement(), [qualifiedLink]))
                    {
                        return false;
                    }
                }

                // matched all reverse chain resources
                continue;
            }

            if (sp.CompiledExpression == null)
            {
                // TODO: Handle non-trivial search parameters
                continue;
            }

            // nest into composite search parameters
            if (sp.ParamType == SearchParamType.Composite)
            {
                if (sp.CompositeComponents == null)
                {
                    continue;
                }

                IEnumerable<ITypedElement> compositeRoots = sp.CompiledExpression.Invoke(rootNode, fpContext);

                // test for matches against all composite components
                foreach (ITypedElement compositeRoot in compositeRoots)
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

            IEnumerable<ITypedElement> extracted;

            // either use FHIRPath to extract, or override for known special extractions
            switch (sp.Name)
            {
                case "_id":
                    {
                        if (rootNode.Children("id").FirstOrDefault() is ITypedElement ete)
                        {
                            extracted = [ ete ];
                        }
                        else
                        {
                            extracted = [];
                        }
                    }

                    break;

                case "_type":
                    extracted = [ new FhirString(rootNode.InstanceType).ToTypedElement() ];
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
                foreach (ITypedElement node in extracted)
                {
                    // TODO(ginoc): add support for chaining into canonical references (QuestionnaireResponse.questionnaire case)
                    if ((node == null) ||
                        (node.InstanceType != "Reference"))
                    {
                        continue;
                    }

                    ResourceReference r = node.ToPoco<ResourceReference>();

                    ITypedElement? resolved = FhirStore.Resolve(r.Reference);

                    if (resolved == null)
                    {
                        continue;
                    }

                    FhirEvaluationContext chainedContext = new FhirEvaluationContext()
                    {
                        Resource = resolved,
                        TerminologyService = FhirStore.Terminology,
                        ElementResolver = FhirStore.Resolve,
                    };

                    string rt = resolved.InstanceType ?? "Resource";

                    if (sp.ChainedParameters.ContainsKey(rt))
                    {
                        found = TestForMatch(resolved, new[] { sp.ChainedParameters[rt] }, chainedContext);
                    }
                    else if (sp.ChainedParameters.ContainsKey("Resource"))
                    {
                        found = TestForMatch(resolved, new[] { sp.ChainedParameters["Resource"] }, chainedContext);
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
            foreach (ITypedElement resultNode in extracted)
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

    private bool TestNode(ParsedSearchParameter sp, ITypedElement resultNode)
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

        // TODO: should I convert this to tuples for the switch?
        // build a routing tuple: {search type}<-{modifier}>-{value type}
        string combined = sp.Modifier == SearchModifierCodes.None
            ? $"{sp.ParamType}-{resultNode.InstanceType}".ToLowerInvariant()
            : $"{sp.ParamType}-{sp.Modifier}-{resultNode.InstanceType}".ToLowerInvariant();

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

            // note that the SDK keeps all ITypedElement 'integer' values in 64-bit format
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
                return EvalTokenSearch.TestTokenOfType(resultNode, sp);

            // Reference Above Modifier
            case "reference-above-canonical":
                return EvalReferenceSearch.TestReferenceAboveCanonical(resultNode, sp);
            case "reference-above-reference":
                return EvalReferenceSearch.TestReferenceAboveReference(resultNode, sp);
            case "reference-above-oid":
                return EvalReferenceSearch.TestReferenceAboveOid(resultNode, sp);
            case "reference-above-uri":
                return EvalReferenceSearch.TestReferenceAboveUri(resultNode, sp);
            case "reference-above-url":
                return EvalReferenceSearch.TestReferenceAboveUrl(resultNode, sp);
            case "reference-above-uuid":
                return EvalReferenceSearch.TestReferenceAboveUuid(resultNode, sp);
            // Reference Below Modifier
            case "reference-below-canonical":
                return EvalReferenceSearch.TestReferenceBelowCanonical(resultNode, sp);
            case "reference-below-reference":
                return EvalReferenceSearch.TestReferenceBelowReference(resultNode, sp);
            case "reference-below-oid":
                return EvalReferenceSearch.TestReferenceBelowOid(resultNode, sp);
            case "reference-below-uri":
                return EvalReferenceSearch.TestReferenceBelowUri(resultNode, sp);
            case "reference-below-url":
                return EvalReferenceSearch.TestReferenceBelowUrl(resultNode, sp);
            case "reference-below-uuid":
                return EvalReferenceSearch.TestReferenceBelowUuid(resultNode, sp);
            // Reference CodeText Modifier
            case "reference-codetext-canonical":
                return EvalReferenceSearch.TestReferenceCodeTextCanonical(resultNode, sp);
            case "reference-codetext-reference":
                return EvalReferenceSearch.TestReferenceCodeTextReference(resultNode, sp);
            case "reference-codetext-oid":
                return EvalReferenceSearch.TestReferenceCodeTextOid(resultNode, sp);
            case "reference-codetext-uri":
                return EvalReferenceSearch.TestReferenceCodeTextUri(resultNode, sp);
            case "reference-codetext-url":
                return EvalReferenceSearch.TestReferenceCodeTextUrl(resultNode, sp);
            case "reference-codetext-uuid":
                return EvalReferenceSearch.TestReferenceCodeTextUuid(resultNode, sp);
            // Reference In Modifier
            case "reference-in-canonical":
                return EvalReferenceSearch.TestReferenceInCanonical(resultNode, sp, FhirStore);
            case "reference-in-reference":
                return EvalReferenceSearch.TestReferenceInReference(resultNode, sp, FhirStore);
            case "reference-in-oid":
                return EvalReferenceSearch.TestReferenceInOid(resultNode, sp, FhirStore);
            case "reference-in-uri":
                return EvalReferenceSearch.TestReferenceInUri(resultNode, sp, FhirStore);
            case "reference-in-url":
                return EvalReferenceSearch.TestReferenceInUrl(resultNode, sp, FhirStore);
            case "reference-in-uuid":
                return EvalReferenceSearch.TestReferenceInUuid(resultNode, sp, FhirStore);
            // Reference NotIn Modifier
            case "reference-notin-canonical":
                return EvalReferenceSearch.TestReferenceNotInCanonical(resultNode, sp, FhirStore);
            case "reference-notin-reference":
                return EvalReferenceSearch.TestReferenceNotInReference(resultNode, sp, FhirStore);
            case "reference-notin-oid":
                return EvalReferenceSearch.TestReferenceNotInOid(resultNode, sp, FhirStore);
            case "reference-notin-uri":
                return EvalReferenceSearch.TestReferenceNotInUri(resultNode, sp, FhirStore);
            case "reference-notin-url":
                return EvalReferenceSearch.TestReferenceNotInUrl(resultNode, sp, FhirStore);
            case "reference-notin-uuid":
                return EvalReferenceSearch.TestReferenceNotInUuid(resultNode, sp, FhirStore);
            // Reference Text Modifier
            case "reference-text-canonical":
                return EvalReferenceSearch.TestReferenceTextCanonical(resultNode, sp);
            case "reference-text-reference":
                return EvalReferenceSearch.TestReferenceTextReference(resultNode, sp);
            case "reference-text-oid":
                return EvalReferenceSearch.TestReferenceTextOid(resultNode, sp);
            case "reference-text-uri":
                return EvalReferenceSearch.TestReferenceTextUri(resultNode, sp);
            case "reference-text-url":
                return EvalReferenceSearch.TestReferenceTextUrl(resultNode, sp);
            case "reference-text-uuid":
                return EvalReferenceSearch.TestReferenceTextUuid(resultNode, sp);
            // Reference TextAdvanced Modifier
            case "reference-textadvanced-canonical":
                return EvalReferenceSearch.TestReferenceTextAdvancedCanonical(resultNode, sp);
            case "reference-textadvanced-reference":
                return EvalReferenceSearch.TestReferenceTextAdvancedReference(resultNode, sp);
            case "reference-textadvanced-oid":
                return EvalReferenceSearch.TestReferenceTextAdvancedOid(resultNode, sp);
            case "reference-textadvanced-uri":
                return EvalReferenceSearch.TestReferenceTextAdvancedUri(resultNode, sp);
            case "reference-textadvanced-url":
                return EvalReferenceSearch.TestReferenceTextAdvancedUrl(resultNode, sp);
            case "reference-textadvanced-uuid":
                return EvalReferenceSearch.TestReferenceTextAdvancedUuid(resultNode, sp);
            // Token Above Modifier
            case "token-above-code":
                return EvalTokenSearch.TestTokenAboveCode(resultNode, sp, FhirStore);
            case "token-above-coding":
                return EvalTokenSearch.TestTokenAboveCoding(resultNode, sp, FhirStore);
            case "token-above-codeableconcept":
                return EvalTokenSearch.TestTokenAboveCodeableConcept(resultNode, sp, FhirStore);
            case "token-above-identifier":
                return EvalTokenSearch.TestTokenAboveIdentifier(resultNode, sp, FhirStore);
            case "token-above-contactpoint":
                return EvalTokenSearch.TestTokenAboveContactPoint(resultNode, sp, FhirStore);
            case "token-above-canonical":
                return EvalTokenSearch.TestTokenAboveCanonical(resultNode, sp, FhirStore);
            case "token-above-oid":
                return EvalTokenSearch.TestTokenAboveOid(resultNode, sp, FhirStore);
            case "token-above-uri":
                return EvalTokenSearch.TestTokenAboveUri(resultNode, sp, FhirStore);
            case "token-above-url":
                return EvalTokenSearch.TestTokenAboveUrl(resultNode, sp, FhirStore);
            case "token-above-uuid":
                return EvalTokenSearch.TestTokenAboveUuid(resultNode, sp, FhirStore);
            case "token-above-string":
                return EvalTokenSearch.TestTokenAboveString(resultNode, sp, FhirStore);
            // Token Below Modifier
            case "token-below-code":
                return EvalTokenSearch.TestTokenBelowCode(resultNode, sp, FhirStore);
            case "token-below-coding":
                return EvalTokenSearch.TestTokenBelowCoding(resultNode, sp, FhirStore);
            case "token-below-codeableconcept":
                return EvalTokenSearch.TestTokenBelowCodeableConcept(resultNode, sp, FhirStore);
            case "token-below-identifier":
                return EvalTokenSearch.TestTokenBelowIdentifier(resultNode, sp, FhirStore);
            case "token-below-contactpoint":
                return EvalTokenSearch.TestTokenBelowContactPoint(resultNode, sp, FhirStore);
            case "token-below-canonical":
                return EvalTokenSearch.TestTokenBelowCanonical(resultNode, sp, FhirStore);
            case "token-below-oid":
                return EvalTokenSearch.TestTokenBelowOid(resultNode, sp, FhirStore);
            case "token-below-uri":
                return EvalTokenSearch.TestTokenBelowUri(resultNode, sp, FhirStore);
            case "token-below-url":
                return EvalTokenSearch.TestTokenBelowUrl(resultNode, sp, FhirStore);
            case "token-below-uuid":
                return EvalTokenSearch.TestTokenBelowUuid(resultNode, sp, FhirStore);
            case "token-below-string":
                return EvalTokenSearch.TestTokenBelowString(resultNode, sp, FhirStore);
            // Token CodeText Modifier
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
            // Token NotIn Modifier
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
            // Token Text Modifier
            case "token-text-code":
                return EvalTokenSearch.TestTokenTextCode(resultNode, sp);
            case "token-text-coding":
                return EvalTokenSearch.TestTokenTextCoding(resultNode, sp);
            case "token-text-codeableconcept":
                return EvalTokenSearch.TestTokenTextCodeableConcept(resultNode, sp);
            case "token-text-identifier":
                return EvalTokenSearch.TestTokenTextIdentifier(resultNode, sp);
            case "token-text-contactpoint":
                return EvalTokenSearch.TestTokenTextContactPoint(resultNode, sp);
            case "token-text-canonical":
                return EvalTokenSearch.TestTokenTextCanonical(resultNode, sp);
            case "token-text-oid":
                return EvalTokenSearch.TestTokenTextOid(resultNode, sp);
            case "token-text-uri":
                return EvalTokenSearch.TestTokenTextUri(resultNode, sp);
            case "token-text-url":
                return EvalTokenSearch.TestTokenTextUrl(resultNode, sp);
            case "token-text-uuid":
                return EvalTokenSearch.TestTokenTextUuid(resultNode, sp);
            case "token-text-string":
                return EvalTokenSearch.TestTokenTextString(resultNode, sp);
            // Token TextAdvanced Modifier
            case "token-textadvanced-code":
                return EvalTokenSearch.TestTokenTextAdvancedCode(resultNode, sp);
            case "token-textadvanced-coding":
                return EvalTokenSearch.TestTokenTextAdvancedCoding(resultNode, sp);
            case "token-textadvanced-codeableconcept":
                return EvalTokenSearch.TestTokenTextAdvancedCodeableConcept(resultNode, sp);
            case "token-textadvanced-identifier":
                return EvalTokenSearch.TestTokenTextAdvancedIdentifier(resultNode, sp);
            case "token-textadvanced-contactpoint":
                return EvalTokenSearch.TestTokenTextAdvancedContactPoint(resultNode, sp);
            case "token-textadvanced-canonical":
                return EvalTokenSearch.TestTokenTextAdvancedCanonical(resultNode, sp);
            case "token-textadvanced-oid":
                return EvalTokenSearch.TestTokenTextAdvancedOid(resultNode, sp);
            case "token-textadvanced-uri":
                return EvalTokenSearch.TestTokenTextAdvancedUri(resultNode, sp);
            case "token-textadvanced-url":
                return EvalTokenSearch.TestTokenTextAdvancedUrl(resultNode, sp);
            case "token-textadvanced-uuid":
                return EvalTokenSearch.TestTokenTextAdvancedUuid(resultNode, sp);
            case "token-textadvanced-string":
                return EvalTokenSearch.TestTokenTextAdvancedString(resultNode, sp);
            case "token-in-canonical":
            case "token-in-oid":
            case "token-in-uri":
            case "token-in-url":
            case "token-in-uuid":
            case "token-in-string":
            case "token-not-codeableconcept":
            // URI Above Modifier
            case "uri-above-canonical":
                return EvalUriSearch.TestUriAboveCanonical(resultNode, sp);
            case "uri-above-oid":
                return EvalUriSearch.TestUriAboveOid(resultNode, sp);
            case "uri-above-uri":
                return EvalUriSearch.TestUriAboveUri(resultNode, sp);
            case "uri-above-url":
                return EvalUriSearch.TestUriAboveUrl(resultNode, sp);
            case "uri-above-uuid":
                return EvalUriSearch.TestUriAboveUuid(resultNode, sp);
            // URI Below Modifier
            case "uri-below-canonical":
                return EvalUriSearch.TestUriBelowCanonical(resultNode, sp);
            case "uri-below-oid":
                return EvalUriSearch.TestUriBelowOid(resultNode, sp);
            case "uri-below-uri":
                return EvalUriSearch.TestUriBelowUri(resultNode, sp);
            case "uri-below-url":
                return EvalUriSearch.TestUriBelowUrl(resultNode, sp);
            case "uri-below-uuid":
                return EvalUriSearch.TestUriBelowUuid(resultNode, sp);
            // URI Contains Modifier
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
            // URI In Modifier
            case "uri-in-canonical":
                return EvalUriSearch.TestUriInCanonical(resultNode, sp, FhirStore);
            case "uri-in-oid":
                return EvalUriSearch.TestUriInOid(resultNode, sp, FhirStore);
            case "uri-in-uri":
                return EvalUriSearch.TestUriInUri(resultNode, sp, FhirStore);
            case "uri-in-url":
                return EvalUriSearch.TestUriInUrl(resultNode, sp, FhirStore);
            case "uri-in-uuid":
                return EvalUriSearch.TestUriInUuid(resultNode, sp, FhirStore);
            // URI Not Modifier
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
            // URI NotIn Modifier
            case "uri-notin-canonical":
                return EvalUriSearch.TestUriNotInCanonical(resultNode, sp, FhirStore);
            case "uri-notin-oid":
                return EvalUriSearch.TestUriNotInOid(resultNode, sp, FhirStore);
            case "uri-notin-uri":
                return EvalUriSearch.TestUriNotInUri(resultNode, sp, FhirStore);
            case "uri-notin-url":
                return EvalUriSearch.TestUriNotInUrl(resultNode, sp, FhirStore);
            case "uri-notin-uuid":
                return EvalUriSearch.TestUriNotInUuid(resultNode, sp, FhirStore);
            // URI OfType Modifier
            case "uri-oftype-canonical":
                return EvalUriSearch.TestUriOfTypeCanonical(resultNode, sp);
            case "uri-oftype-oid":
                return EvalUriSearch.TestUriOfTypeOid(resultNode, sp);
            case "uri-oftype-uri":
                return EvalUriSearch.TestUriOfTypeUri(resultNode, sp);
            case "uri-oftype-url":
                return EvalUriSearch.TestUriOfTypeUrl(resultNode, sp);
            case "uri-oftype-uuid":
                return EvalUriSearch.TestUriOfTypeUuid(resultNode, sp);
            // URI Text Modifier
            case "uri-text-canonical":
                return EvalUriSearch.TestUriTextCanonical(resultNode, sp);
            case "uri-text-oid":
                return EvalUriSearch.TestUriTextOid(resultNode, sp);
            case "uri-text-uri":
                return EvalUriSearch.TestUriTextUri(resultNode, sp);
            case "uri-text-url":
                return EvalUriSearch.TestUriTextUrl(resultNode, sp);
            case "uri-text-uuid":
                return EvalUriSearch.TestUriTextUuid(resultNode, sp);
            // URI TextAdvanced Modifier
            case "uri-textadvanced-canonical":
                return EvalUriSearch.TestUriTextAdvancedCanonical(resultNode, sp);
            case "uri-textadvanced-oid":
                return EvalUriSearch.TestUriTextAdvancedOid(resultNode, sp);
            case "uri-textadvanced-uri":
                return EvalUriSearch.TestUriTextAdvancedUri(resultNode, sp);
            case "uri-textadvanced-url":
                return EvalUriSearch.TestUriTextAdvancedUrl(resultNode, sp);
            case "uri-textadvanced-uuid":
                return EvalUriSearch.TestUriTextAdvancedUuid(resultNode, sp);
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
    public static bool SearchTestMissing(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        bool positive = sp.Values.Any(v => v.StartsWith("t", StringComparison.OrdinalIgnoreCase));
        bool negative = sp.Values.Any(v => v.StartsWith("f", StringComparison.OrdinalIgnoreCase));

        // testing both missing and not missing is always true
        if (positive && negative)
        {
            return true;
        }

        // test for missing and a null value
        if (positive && (valueNode?.Value == null))
        {
            return true;
        }

        // test for not missing and not a null value
        if (negative && (valueNode?.Value != null))
        {
            return true;
        }

        // other combinations are search misses
        return false;
    }

    /// <summary>Performs a search test against a human name.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private static bool SearchTestHumanName(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        foreach (ITypedElement node in valueNode.Descendants())
        {
            if (node.InstanceType != "string")
            {
                continue;
            }
            string value = (string)(node?.Value ?? string.Empty);

            switch (sp.Modifier)
            {
                case SearchModifierCodes.None:
                    {
                        if (sp.Values.Any(v => value.StartsWith(v, StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                    break;

                case SearchModifierCodes.Contains:
                    {
                        if (sp.Values.Any(v => value.Contains(v, StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                    break;

                case SearchModifierCodes.Exact:
                    {
                        if (sp.Values.Any(v => value.Equals(v, StringComparison.Ordinal)))
                        {
                            return true;
                        }
                    }
                    break;

                case SearchModifierCodes.Missing:
                    {
                        if (sp.Values.Any(v =>
                            (v.StartsWith("t", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(value)) ||
                            (v.StartsWith("f", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(value))))
                        {
                            return true;
                        }
                    }
                    break;

                case SearchModifierCodes.Not:
                    {
                        if (sp.Values.Any(v => !value.Equals(v, StringComparison.Ordinal)))
                        {
                            return true;
                        }
                    }
                    break;

                case SearchModifierCodes.ResourceType:
                case SearchModifierCodes.Above:
                case SearchModifierCodes.Below:
                case SearchModifierCodes.CodeText:
                case SearchModifierCodes.Identifier:
                case SearchModifierCodes.In:
                case SearchModifierCodes.Iterate:
                case SearchModifierCodes.NotIn:
                case SearchModifierCodes.OfType:
                case SearchModifierCodes.Text:
                case SearchModifierCodes.TextAdvanced:
                default:
                    throw new Exception($"Invalid search modifier for HumanName: {sp.ModifierLiteral}");
            }
        }

        return false;
    }
}

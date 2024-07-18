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

    // Build a hashset of ids that pass reverse chaining criteria
    //public HashSet<string> BuildReverseChainIds(IEnumerable<ParsedSearchParameter> searchParameters)
    //{
    //    HashSet<string> reverseChainIds = new();

    //    // loop over search parameters to build reverse chain parameters
    //    foreach (ParsedSearchParameter sp in searchParameters)
    //    {
    //        // for reverse chaining, we nest the search instead of evaluating it here
    //        if (sp.ReverseChainedParameterLink == null)
    //        {
    //            continue;
    //        }

    //        // 
    //    }
    //}

    /// <summary>Tests a resource against parsed search parameters for matching.</summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    /// <param name="rootNode">         The resource.</param>
    /// <param name="searchParameters"> Options for controlling the search.</param>
    /// <param name="appliedParameters">[out] Options for controlling the applied.</param>
    /// <param name="ignoredParameters">[out] Options for controlling the ignored.</param>
    /// <param name="fpContext">        (Optional) The context.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public bool TestForMatch(
        ITypedElement rootNode,
        IEnumerable<ParsedSearchParameter> searchParameters,
        FhirEvaluationContext? fpContext = null)
    {
        if (rootNode == null)
        {
            throw new ArgumentNullException(nameof(rootNode));
        }

        if (!searchParameters.Any())
        {
            return true;
        }

        if (fpContext == null)
        {
            fpContext = new FhirEvaluationContext(rootNode.ToScopedNode())
            {
                TerminologyService = FhirStore.Terminology,
                ElementResolver = FhirStore.Resolve,
            };
        }

        foreach (ParsedSearchParameter sp in searchParameters)
        {
            if (sp.IgnoredParameter)
            {
                continue;
            }

            // TODO: finish reverse chaining
            //    // for reverse chaining, we nest the search instead of evaluating it here
            //    if (sp.ReverseChainedParameterLink != null)
            //    {
            //        continue;
            //        //// loop over any extracted values and check the resource store for matches
            //        //foreach (ITypedElement node in extracted)
            //        //{
            //        //    if ((node == null) ||
            //        //        (node.InstanceType != "Reference"))
            //        //    {
            //        //        continue;
            //        //    }

            //        //    ResourceReference r = node.ToPoco<ResourceReference>();

            //        //    ITypedElement? resolved = FhirStore.Resolve(r.Reference);

            //        //    if (resolved == null)
            //        //    {
            //        //        continue;
            //        //    }

            //        //    FhirEvaluationContext chainedContext = new FhirEvaluationContext(resolved.ToScopedNode());
            //        //    chainedContext.ElementResolver = FhirStore.Resolve;

            //        //    string rt = resolved.InstanceType.ToString();

            //        //    if (sp.ChainedParameters.ContainsKey(rt))
            //        //    {
            //        //        found = TestForMatch(resolved, new[] { sp.ChainedParameters[rt] }, chainedContext);
            //        //    }
            //        //    else if (sp.ChainedParameters.ContainsKey("Resource"))
            //        //    {
            //        //        found = TestForMatch(resolved, new[] { sp.ChainedParameters["Resource"] }, chainedContext);
            //        //    }

            //        //    if (found)
            //        //    {
            //        //        break;
            //        //    }

            //        //    //foreach (ParsedSearchParameter chained in sp.ChainedParameters)
            //        //    //{
            //        //    //    TestForMatch(resolved, new[] { chained }, chainedContext);
            //        //    //}
            //        //}

            //        //if (!found)
            //        //{
            //        //    return false;
            //        //}

            //        //continue;
            //    }

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

            IEnumerable<ITypedElement> extracted = sp.CompiledExpression.Invoke(rootNode, fpContext);

            if (!extracted.Any())
            {
                if ((sp.Modifier == SearchModifierCodes.Missing) &&
                    sp.Values.Any(v => v.StartsWith("t", StringComparison.OrdinalIgnoreCase)))
                {
                    // successful match
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

                    FhirEvaluationContext chainedContext = new FhirEvaluationContext(resolved.ToScopedNode())
                    {
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

            // TODO(ginoc): not yet implemented
            case "reference-above-canonical":
            case "reference-above-reference":
            case "reference-above-oid":
            case "reference-above-uri":
            case "reference-above-url":
            case "reference-above-uuid":
            case "reference-below-canonical":
            case "reference-below-reference":
            case "reference-below-oid":
            case "reference-below-uri":
            case "reference-below-url":
            case "reference-below-uuid":
            case "reference-codetext-canonical":
            case "reference-codetext-reference":
            case "reference-codetext-oid":
            case "reference-codetext-uri":
            case "reference-codetext-url":
            case "reference-codetext-uuid":
            case "reference-in-canonical":
            case "reference-in-reference":
            case "reference-in-oid":
            case "reference-in-uri":
            case "reference-in-url":
            case "reference-in-uuid":
            case "reference-notin-canonical":
            case "reference-notin-reference":
            case "reference-notin-oid":
            case "reference-notin-uri":
            case "reference-notin-url":
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
            case "token-above-code":
            case "token-above-coding":
            case "token-below-code":
            case "token-below-coding":
            case "token-codetext-code":
            case "token-codetext-coding":
            case "token-notin-code":
            case "token-notin-coding":
            case "token-text-code":
            case "token-text-coding":
            case "token-textadvanced-code":
            case "token-textadvanced-coding":
            case "token-above-codeableconcept":
            case "token-above-identifier":
            case "token-above-contactpoint":
            case "token-above-canonical":
            case "token-above-oid":
            case "token-above-uri":
            case "token-above-url":
            case "token-above-uuid":
            case "token-above-string":
            case "token-below-codeableconcept":
            case "token-below-identifier":
            case "token-below-contactpoint":
            case "token-below-canonical":
            case "token-below-oid":
            case "token-below-uri":
            case "token-below-url":
            case "token-below-uuid":
            case "token-below-string":
            case "token-codetext-codeableconcept":
            case "token-codetext-identifier":
            case "token-codetext-contactpoint":
            case "token-codetext-canonical":
            case "token-codetext-oid":
            case "token-codetext-uri":
            case "token-codetext-url":
            case "token-codetext-uuid":
            case "token-codetext-string":
            case "token-in-canonical":
            case "token-in-oid":
            case "token-in-uri":
            case "token-in-url":
            case "token-in-uuid":
            case "token-in-string":
            case "token-not-codeableconcept":
            case "token-notin-codeableconcept":
            case "token-notin-identifier":
            case "token-notin-contactpoint":
            case "token-notin-canonical":
            case "token-notin-oid":
            case "token-notin-uri":
            case "token-notin-url":
            case "token-notin-uuid":
            case "token-notin-string":
            case "token-text-codeableconcept":
            case "token-text-identifier":
            case "token-text-contactpoint":
            case "token-text-canonical":
            case "token-text-oid":
            case "token-text-uri":
            case "token-text-url":
            case "token-text-uuid":
            case "token-text-string":
            case "token-textadvanced-codeableconcept":
            case "token-textadvanced-identifier":
            case "token-textadvanced-contactpoint":
            case "token-textadvanced-canonical":
            case "token-textadvanced-oid":
            case "token-textadvanced-uri":
            case "token-textadvanced-url":
            case "token-textadvanced-uuid":
            case "token-textadvanced-string":
            case "uri-above-canonical":
            case "uri-above-oid":
            case "uri-above-uri":
            case "uri-above-url":
            case "uri-above-uuid":
            case "uri-below-canonical":
            case "uri-below-oid":
            case "uri-below-uri":
            case "uri-below-url":
            case "uri-below-uuid":
            case "uri-contains-canonical":
            case "uri-contains-oid":
            case "uri-contains-uri":
            case "uri-contains-url":
            case "uri-contains-uuid":
            case "uri-in-canonical":
            case "uri-in-oid":
            case "uri-in-uri":
            case "uri-in-url":
            case "uri-in-uuid":
            case "uri-not-canonical":
            case "uri-not-oid":
            case "uri-not-uri":
            case "uri-not-url":
            case "uri-not-uuid":
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

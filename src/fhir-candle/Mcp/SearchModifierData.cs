namespace fhir.candle.Mcp;


public static class SearchModifierData
{
    public readonly record struct FhirSearchModifierDescriptionRec(
        string Code,
        string TargetSearchType,
        string? TargetFhirType,
        string ParameterValueSearchType,
        string Definition,
        string Description,
        string Examples)
    {
        public override string ToString() => TargetFhirType is null
            ? $$$"""
                [{{{TargetSearchType}}}]:{{{Code}}}=[{{{ParameterValueSearchType}}}]
                * Definition:
                    {{{Definition}}}
                * Description:
                    {{{Description}}}
                * Examples:
                    {{{Examples}}}
                """
            : $$$"""
                [{{{TargetSearchType}}}({{{TargetFhirType}}})]:{{{Code}}}=[{{{ParameterValueSearchType}}}]
                * Definition:
                    {{{Definition}}}
                * Description:
                    {{{Description}}}
                * Examples:
                    {{{Examples}}}
                """;
    };

    //static SearchModifierData()
    //{

    //}

    public static readonly List<FhirSearchModifierDescriptionRec> SearchModifierDescriptionList = [
            new()
            {
                Code = "above",
                TargetSearchType = "reference",
                TargetFhirType = "Reference",
                ParameterValueSearchType = "reference",
                Definition = "Tests whether the value in a resource is or subsumes the supplied parameter value (is-a, or hierarchical relationships).",
                Description = """
                    When the above modifier is used with a reference type search parameter and the target is an element with the FHIR type Reference, the search is interpreted as a hierarchical search on linked resources of the same type, including exact matches and all children of those matches. The above modifier is only valid for circular references - that is, references that point to another instance of the same type of resource where those references establish a hierarchy. For example, the Location resource can define a hierarchy using the partOf element to track locations that are inside each other (e.g., Location/A101, a room, is partOf Location/A, a floor). Meanwhile, the Patient resource could refer to other patient resources via the link element, but no hierarchical meaning is intended. Further discussion about the requirements and uses for this type of search can be found in the section Searching Hierarchies.
                    """,
                Examples = """
                    GET [base]/Procedure?location:above=A101
                        matches any Procedure resources with locations:
                        - A101, Location/A101, https://example.org/Location/A101 - this location by ID
                        - A100, Location/A100, https://example.org/Location/A100 - parent of A101, representing the first floor (A101 - A199)
                        - BuildingA, Location/BuildingA, https://example.org/Location/BuildingA - parent of A100, representing the building 'A'
                    """,
            },
            new()
            {
                Code = "above",
                TargetSearchType = "reference",
                TargetFhirType = "canonical",
                ParameterValueSearchType = "token",
                Definition = "Tests whether the value in a resource is or subsumes the supplied parameter value (is-a, or hierarchical relationships).",
                Description = """
                    When the above modifier is used with a reference type search parameter and the target is an element with the FHIR type canonical, the search is interpreted as a version search against the canonical resource. The format of the parameter is either [url] or [url]|[version] (see Encoding Note). This search is only allowed if the version scheme for the resource is known (e.g., a known version-scheme extension or element). Version-related search criteria against resources with unknown versioning schemes SHALL be either ignored or rejected. The above modifier comparison is performed as a 'greater than' against the version-scheme defined by the resource.
                    """,
                Examples = """
                    GET [base]/CodeSystem?url:above=http://example.org/fhir/CodeSystem/ExampleCodeSystem
                        matches the highest version of ExampleCodeSystem
                    GET [base]/CodeSystem?url:above=http://example.org/fhir/CodeSystem/ExampleCodeSystem|1.0
                        matches the highest version of ExampleCodeSystem that is greater than 1.0
                    """,
                    },
            new ()
            {
                Code = "above",
                TargetSearchType = "token",
                ParameterValueSearchType = "token",
                Definition = "Tests whether the value in a resource is or subsumes the supplied parameter value (is-a, or hierarchical relationships).",
                Description = """
                    When the above modifier is used with a token type search parameter, the supplied token is a concept with the form [system]|[code] (see Encoding Note) and the intention is to test whether the coding in a resource subsumes the specified search code. Matches to the input token concept have an is-a relationship with the coding in the resource, and this includes the coding itself.
                    """,
                Examples = """
                    GET [base]/Observation?code:above=http://snomed.info/sct|3738000
                        for Observations with a code above SNOMED 'Viral hepatitis (disorder)' will match any Observation resources with codes:
                            - SNOMED 3738000 - Viral hepatitis (this code)
                            - SNOMED 235862008 - Hepatitis due to infection (parent of 'Viral hepatitis')
                            - SNOMED 128241005 - Inflammatory disease of liver (parent of 'Hepatitis due to infection')
                            - etc.
                            - SNOMED 312130009 - Viral infection by site (parent of 'Viral hepatitis')
                            - SNOMED 301810000 - Infection by site (parent of 'Viral infection by site')
                            - etc.
                        Note that there are two hierarchical parents to the requested code and parent matches traverse up each path.
                    """,
            },
            new ()
            {
                Code = "above",
                TargetSearchType = "uri",
                ParameterValueSearchType = "uri",
                Definition = "Tests whether the value in a resource is or subsumes the supplied parameter value (is-a, or hierarchical relationships).",
                Description = """
                    When the above modifier is used with a uri type search parameter, the value is used for partial matching based on URL path segments. Because of the hierarchical behavior of above, the modifier only applies to URIs that are URLs and cannot be used with URNs such as OIDs. Note that there are not many use cases where above is useful compared to a below search.
                    """,
                Examples = """
                    GET [base]/ValueSet?url:above=http://acme.org/fhir/ValueSet/123/_history/5
                        would match any ValueSet resources with a url of:
                            - http://acme.org/fhir/ValueSet/123/_history/5 - full match
                            - http://acme.org/fhir/ValueSet/123/_history - parent of requested URI
                            - http://acme.org/fhir/ValueSet/123 - ancestor of requested URI
                            - http://acme.org/fhir/ValueSet - ancestor of requested URI
                            - http://acme.org/fhir - ancestor of requested URI
                            - http://acme.org/ - ancestor of requested URI
                    """,
            },
            new()
            {
                Code = "below",
                TargetSearchType = "reference",
                TargetFhirType = "Reference",
                ParameterValueSearchType = "reference",
                Definition = "Tests whether the value in a resource is or is subsumed by the supplied parameter value (is-a, or hierarchical relationships).",
                Description = """
                    When the below modifier is used with a reference type search parameter and the target is an element with the FHIR type Reference, the search is interpreted as a hierarchical search on linked resources of the same type, including exact matches and all children of those matches. The below modifier is only valid for circular references - that is, references that point to another instance of the same type of resource where those references establish a hierarchy. For example, the Location resource can define a hierarchy using the partOf element to track locations that are inside each other (e.g., Location/A101, a room, is partOf Location/A, a floor). Meanwhile, the Patient resource could refer to other patient resources via the link element, but no hierarchical meaning is intended. Further discussion about the requirements and uses for this type of search can be found in the section Searching Hierarchies.
                    """,
                Examples = """
                    GET [base]/Procedure?location:below=BuildingA
                        for Procedures with a location below BuildingA would match any Procedure resources with locations:
                            - BuildingA, Location/BuildingA, https://example.org/Location/BuildingA - this location by ID
                            - A100, Location/A100, https://example.org/Location/A100 - child of BuildingA, representing the first floor
                            - A101, Location/A101, https://example.org/Location/A101 - child of A100, room 101
                            - A1.., etc. - child of A100, rooms on the first floor
                            - A200, Location/A200, https://example.org/Location/A200 - child of BuildingA, representing the second floor
                            - etc.
                    """,
            },
            new()
            {
                Code = "below",
                TargetSearchType = "reference",
                TargetFhirType = "canonical",
                ParameterValueSearchType = "reference",
                Definition = "Tests whether the value in a resource is or is subsumed by the supplied parameter value (is-a, or hierarchical relationships).",
                Description = """
                    When the below modifier is used with a reference type search parameter and the target is an element with the FHIR type canonical, the search is interpreted as a version search against the canonical resource. The format of the parameter is either [url] or [url]|[version] (see Encoding Note). This search is only allowed if the version scheme for the resource is known (e.g., a known version-scheme extension or element). Version-related search criteria against resources with unknown versioning schemes SHALL be either ignored or rejected. The below modifier comparison is performed as a 'less than' against the version-scheme defined by the resource.
                    """,
                Examples = """
                    GET [base]/CodeSystem?url:below=http://example.org/fhir/CodeSystem/ExampleCodeSystem
                        matches the lowest version of ExampleCodeSystem
                    GET [base]/CodeSystem?url:below=http://example.org/fhir/CodeSystem/ExampleCodeSystem|1.0
                        matches the lowest version of ExampleCodeSystem that is less than than 1.0
                    """,
            },
            new()
            {
                Code = "below",
                TargetSearchType = "token",
                ParameterValueSearchType = "token",
                Definition = "Tests whether the value in a resource is or is subsumed by the supplied parameter value (is-a, or hierarchical relationships).",
                Description = """
                    When the below modifier is used with a token type search parameter, the supplied token is a concept with the form [system]|[code] and the intention is to test whether the coding in a resource is subsumed by the specified search code. Matches include resources that have a coding that has an is-a relationship with the input concept, and this includes the coding itself.
                    """,
                Examples = """
                    GET [base]/Observation?code:below=http://snomed.info/sct|235862008
                        for Observations with a code below SNOMED 'Hepatitis due to infection' would match any Observation resources with codes:
                            - SNOMED 235862008 - Hepatitis due to infection (this code)
                            - SNOMED 773113008 - Acute infectious hepatitis (child)
                            - SNOMED 95897009 - Amebic hepatitis (child)
                            - etc.
                    """,
            },
            new()
            {
                Code = "below",
                TargetSearchType = "uri",
                ParameterValueSearchType = "uri",
                Definition = "Tests whether the value in a resource is or is subsumed by the supplied parameter value (is-a, or hierarchical relationships).",
                Description = """
                    When the below modifier is used with a uri type search parameter, the value is used for partial matching based on URL path segments. Because of the hierarchical behavior of below, the modifier only applies to URIs that are URLs and cannot be used with URNs such as OIDs.
                    """,
                Examples = """
                    GET [base]/ValueSet?url:below=http://acme.org/fhir
                        would match any ValueSet resources with a url of:
                            - http://acme.org/fhir - full match
                            - http://acme.org/fhir/ValueSet - child of requested URI
                            - http://acme.org/fhir/ValueSet/123 - descendant of requested URI
                            - etc.
                    """,
            },
            new()
            {
                Code = "code-text",
                TargetSearchType = "reference",
                ParameterValueSearchType = "string",
                Definition = "Tests whether a coded value (e.g., Coding.code) in a resource matches the supplied parameter value using basic string matching (starts with or is, case-insensitive).",
                Description = """
                    The code-text modifier allows clients to indicate that a supplied string input is used as a case-insensitive and combining-character insensitive test when matching against the start of target string. This modifier is used to do a 'standard' string search against code values.
                    """,
                Examples = """
                    GET [base]/Patient?language:code-text=en
                        would match any Patient resources with a communication language of:
                            - en - requested code text (case-insensitive)
                            - en-AU - starts with requested code text (case-insensitive)
                            - en-CA - starts with requested code text (case-insensitive)
                            - etc.
                    """,
            },
            new()
            {
                Code = "code-text",
                TargetSearchType = "token",
                ParameterValueSearchType = "string",
                Definition = "Tests whether a coded value (e.g., Coding.code) in a resource matches the supplied parameter value using basic string matching (starts with or is, case-insensitive).",
                Description = """
                    The code-text modifier allows clients to indicate that a supplied string input is used as a case-insensitive and combining-character insensitive test when matching against the start of target string. This modifier is used to do a 'standard' string search against code values.
                    """,
                Examples = """
                    GET [base]/Patient?language:code-text=en
                        would match any Patient resources with a communication language of:
                            - en - requested code text (case-insensitive)
                            - en-AU - starts with requested code text (case-insensitive)
                            - en-CA - starts with requested code text (case-insensitive)
                            - etc.
                    """,
            },
            new()
            {
                Code = "contains",
                TargetSearchType = "string",
                ParameterValueSearchType = "string",
                Definition = "Tests whether the value in a resource includes the supplied parameter value anywhere within the field being searched.",
                Description = """
                    The contains modifier allows clients to indicate that a supplied string input is used as a case-insensitive and combining-character insensitive test when matching anywhere in the target string.
                    """,
                Examples = """
                    GET [base]/Patient?family:contains=son
                        would match any Patient resources with a family names such as:
                            - Son - requested string (case-insensitive)
                            - Sonder - begins with requested string (case-insensitive)
                            - Erikson - ends with requested string (case-insensitive)
                            - Samsonite - contains requested string (case-insensitive)
                            - etc.
                    """,
            },
            new()
            {
                Code = "contains",
                TargetSearchType = "uri",
                ParameterValueSearchType = "string",
                Definition = "Tests whether the value in a resource includes the supplied parameter value anywhere within the field being searched.",
                Description = """
                    The contains modifier allows clients to indicate that a supplied string input is used as a case-insensitive and combining-character insensitive test when matching anywhere in the target string.
                    """,
                Examples = """
                    GET [base]/ValueSet?url:contains=example
                        would match any ValueSet resources with a url containing 'example' anywhere in the URI.
                    """,
            },
            new()
            {
                Code = "exact",
                TargetSearchType = "string",
                ParameterValueSearchType = "string",
                Definition = "Tests whether the value in a resource exactly matches the supplied parameter value (the whole string, including casing and accents).",
                Description = """
                    The exact modifier allows clients to indicate that a supplied string input is the complete and exact value that tested for a match, including casing and combining characters.
                    """,
                Examples = """
                    GET [base]/Patient?family:exact=Son
                        will only match Patient resources with a family name of:
                            - Son - requested string (case-sensitive)
                    """,
            },
            new()
            {
                Code = "identifier",
                TargetSearchType = "reference",
                TargetFhirType = "Reference",
                ParameterValueSearchType = "token",
                Definition = "Tests whether the Reference.identifier in a resource (rather than the Reference.reference) matches the supplied parameter value.",
                Description = """
                    The identifier modifier allows clients to indicate that a supplied token is used to match against the Reference.identifier element of a reference instead of the Reference.reference element. The format of the parameter is [system]|[code]. Note that chaining is not supported when using the identifier modifier and the modifier is not supported on canonical elements since they do not have an identifier separate from the reference itself.
                    """,
                Examples = """
                    GET [base]/Observation?subject:identifier=http://example.org/fhir/mrn|12345
                        for observations with a subject containing the identifier 'http://example.org/fhir/mrn|12345' would match Observation resources with subject.identifier matching the specified system and value.
                    """,
            },
            new()
            {
                Code = "in",
                TargetSearchType = "token",
                ParameterValueSearchType = "uri",
                Definition = "Tests whether the value in a resource is a member of the supplied parameter ValueSet.",
                Description = """
                    The in modifier is used to filter based on value membership of codes in Value Sets. When the in modifier is used with a token search parameter, the input is a uri (relative or absolute) that identifies a value set, and the search parameter tests whether the coding is in the specified value set.
                    """,
                Examples = """
                    GET [base]/Condition?code:in=ValueSet/123
                        would match any conditions that contain any code from 'ValueSet/123'.
                    GET [base]/Condition?code:in=http://snomed.info/sct?fhir_vs=isa/235862008
                        would match any conditions that contain any code from SNOMED CT that is a 235862008.
                    """,
            },
            new()
            {
                Code = "iterate",
                TargetSearchType = "n/a",
                ParameterValueSearchType = "n/a",
                Definition = "The search parameter indicates an inclusion directive (_include, _revinclude) that is applied to an included resource instead of the matching resource.",
                Description = """
                    The iterate modifier is used to indicate that an inclusion directive should be applied to included resources as well, rather than only the matching resources. Note that this modifier is not defined for any search parameter types and can only be applied to the search result parameters of _include and _revinclude.
                    """,
                Examples = """
                    GET [base]/Observation?code=http://snomed.info/sct|3738000&_include=Observation:patient&_include:iterate=Patient:link
                        would match any observations with the SNOMED code 3738000 (Viral hepatitis (disorder)). The results would include resources found by following the chained search reference Observation.patient, which are Patient resources linked via Observation.subject. Additionally, the server would iterate through the included patient records and follow the Patient.link FHIR references, including linked Patient or RelatedPerson resources.
                    """,
            },
            new()
            {
                Code = "missing",
                TargetSearchType = "date",
                ParameterValueSearchType = "boolean",
                Definition = "Tests whether the value in a resource is present (when the supplied parameter value is true) or absent (when the supplied parameter value is false).",
                Description = """
                    The missing modifier allows clients to filter based on whether resources contain values that can match a search parameter. Usually, this equates to testing if a resource has an element or not.
                    """,
                Examples = """
                    GET [base]/Patient?birthdate:missing=true
                        would match any Patient records that do not have any value in Patient.birthDate.
                    GET [base]/Patient?birthdate:missing=false
                        would match any Patient records that have any value in Patient.birthDate.
                    """,
            },
            new()
            {
                Code = "missing",
                TargetSearchType = "number",
                ParameterValueSearchType = "boolean",
                Definition = "Tests whether the value in a resource is present (when the supplied parameter value is true) or absent (when the supplied parameter value is false).",
                Description = """
                    The missing modifier allows clients to filter based on whether resources contain values that can match a search parameter. Usually, this equates to testing if a resource has an element or not.
                    """,
                Examples = """
                    GET [base]/Observation?value-quantity:missing=true
                        would match any Observation records that do not have any value in valueQuantity.
                    """,
            },
            new()
            {
                Code = "missing",
                TargetSearchType = "quantity",
                ParameterValueSearchType = "boolean",
                Definition = "Tests whether the value in a resource is present (when the supplied parameter value is true) or absent (when the supplied parameter value is false).",
                Description = """
                    The missing modifier allows clients to filter based on whether resources contain values that can match a search parameter. Usually, this equates to testing if a resource has an element or not.
                    """,
                Examples = """
                    GET [base]/Observation?value-quantity:missing=true
                        would match any Observation records that do not have any value in valueQuantity.
                    """,
            },
            new()
            {
                Code = "missing",
                TargetSearchType = "reference",
                ParameterValueSearchType = "boolean",
                Definition = "Tests whether the value in a resource is present (when the supplied parameter value is true) or absent (when the supplied parameter value is false).",
                Description = """
                    The missing modifier allows clients to filter based on whether resources contain values that can match a search parameter. Usually, this equates to testing if a resource has an element or not.
                    """,
                Examples = """
                    GET [base]/Observation?subject:missing=true
                        would match any Observation records that do not have any value in subject.
                    """,
            },
            new()
            {
                Code = "missing",
                TargetSearchType = "string",
                ParameterValueSearchType = "boolean",
                Definition = "Tests whether the value in a resource is present (when the supplied parameter value is true) or absent (when the supplied parameter value is false).",
                Description = """
                    The missing modifier allows clients to filter based on whether resources contain values that can match a search parameter. Usually, this equates to testing if a resource has an element or not.
                    """,
                Examples = """
                    GET [base]/Patient?given:missing=true
                        would match any Patient records that do not have any value in Patient.name that contains a value for given.
                    """,
            },
            new()
            {
                Code = "missing",
                TargetSearchType = "token",
                ParameterValueSearchType = "boolean",
                Definition = "Tests whether the value in a resource is present (when the supplied parameter value is true) or absent (when the supplied parameter value is false).",
                Description = """
                    The missing modifier allows clients to filter based on whether resources contain values that can match a search parameter. Usually, this equates to testing if a resource has an element or not.
                    """,
                Examples = """
                    GET [base]/Patient?gender:missing=true
                        would match any Patient records that do not have any value in Patient.gender.
                    """,
            },
            new()
            {
                Code = "missing",
                TargetSearchType = "uri",
                ParameterValueSearchType = "boolean",
                Definition = "Tests whether the value in a resource is present (when the supplied parameter value is true) or absent (when the supplied parameter value is false).",
                Description = """
                    The missing modifier allows clients to filter based on whether resources contain values that can match a search parameter. Usually, this equates to testing if a resource has an element or not.
                    """,
                Examples = """
                    GET [base]/ValueSet?url:missing=true
                        would match any ValueSet records that do not have any value in ValueSet.url.
                    """,
            },
            new()
            {
                Code = "not",
                TargetSearchType = "token",
                ParameterValueSearchType = "token",
                Definition = "Tests whether the value in a resource does not match the specified parameter value. Note that this includes resources that have no value for the parameter.",
                Description = """
                    The not modifier allows clients to filter based on whether resources do not contain a specified token based on the search parameter input.
                    """,
                Examples = """
                    GET [base]/Patient?gender:not=male
                        would match any Patient records that do not have male as the value in Patient.gender. This includes:
                            - female - Administrative Gender that is not 'male'
                            - other - Administrative Gender that is not 'male'
                            - unknown - Administrative Gender that is not 'male'
                            - records without a Patient.gender value
                    """,
            },
            new()
            {
                Code = "not-in",
                TargetSearchType = "token",
                ParameterValueSearchType = "uri",
                Definition = "Tests whether the value in a resource is not a member of the supplied parameter ValueSet.",
                Description = """
                    The not-in modifier is used to filter based on a value exclusion test for codes of Value Sets. When the not-in modifier is used with a token search parameter, the input is a uri (relative or absolute) that identifies a value set, and the search parameter tests whether the coding is not in the specified value set.
                    """,
                Examples = """
                    GET [base]/Condition?code:not-in=ValueSet/123
                        would match any conditions that do not contain any code from 'ValueSet/123'.
                    """,
            },
            new()
            {
                Code = "of-type",
                TargetSearchType = "token",
                TargetFhirType = "Identifier",
                ParameterValueSearchType = "token",
                Definition = "Tests whether the Identifier value in a resource matches the supplied parameter value.",
                Description = """
                    The of-type modifier allows clients to filter for resource Identifier, based on the Identifier.type.coding.system, Identifier.type.coding.code and Identifier.value. This allows searches for specific values only within a specific identifier code system. The format when using 'of-type' is [system]|[code]|[value], where [system] and [code] refer to the code and system in Identifier.type.coding; the system and code portion is considered a match if the the system|code token would match a given Identifier.type.coding. The [value] test is a string match against Identifier.value. All three parts must be present.
                    """,
                Examples = """
                    GET [base]/Patient?identifier:of-type=http://terminology.hl7.org/CodeSystem/v2-0203|MR|12345
                        for patients that contain an identifier that has a type coding of with a system of http://terminology.hl7.org/CodeSystem/v2-0203, a code of MR (which identifies Medical Record Numbers), and a value of 12345.
                    """,
            },
            new()
            {
                Code = "text",
                TargetSearchType = "reference",
                ParameterValueSearchType = "string",
                Definition = "Tests whether the textual value in a resource (e.g., CodeableConcept.text, Coding.display, Identifier.type.text, or Reference.display) matches the supplied parameter value using basic string matching (begins with or is, case-insensitive).",
                Description = """
                    The text modifier on search parameters of type reference allows clients to indicate that a supplied string is to be used to perform a string search against a textual value associated with a code or value. For example, CodeableConcept.text, Coding.display, Identifier.type.text, or Reference.display. Search matching is performed using basic string matching rules - begins with or is, case-insensitive.
                    """,
                Examples = """
                    GET [base]/Condition?code:text=headache
                        would match Condition resources containing any codes that start with or equal the string 'headache' (case-insensitive).
                    """,
            },
            new()
            {
                Code = "text",
                TargetSearchType = "token",
                ParameterValueSearchType = "string",
                Definition = "Tests whether the textual value in a resource (e.g., CodeableConcept.text, Coding.display, Identifier.type.text, or Reference.display) matches the supplied parameter value using basic string matching (begins with or is, case-insensitive).",
                Description = """
                    The text modifier on search parameters of type token allows clients to indicate that a supplied string is to be used to perform a string search against a textual value associated with a code or value. For example, CodeableConcept.text, Coding.display, Identifier.type.text, or Identifier.assigner.display. Search matching is performed using basic string matching rules - begins with or is, case-insensitive.
                    """,
                Examples = """
                    GET [base]/Condition?code:text=headache
                        would match Condition resources containing any codes that start with or equal the string 'headache' (case-insensitive).
                    """,
            },
            new()
            {
                Code = "text",
                TargetSearchType = "string",
                ParameterValueSearchType = "string",
                Definition = "The search parameter value is to be processed as input to a search with advanced text handling.",
                Description = """
                    The text modifier allows clients to request matching based on advanced string processing of the search parameter input. Implementers of the text modifier SHOULD support a sophisticated search functionality of the type offered by typical text indexing services. The value of the parameter is a text-based search, which may involve searching multiple words with thesaurus and proximity considerations, and logical operations such as AND, OR, etc.
                    """,
                Examples = """
                    GET [base]/Composition?section:text=(bone OR liver) and metastases
                        for compositions about metastases in the bones or liver of subjects will search for those literal values, but may also search for terms such as 'cancerous growth', 'tumor', etc.
                    """,
            },
            new()
            {
                Code = "text-advanced",
                TargetSearchType = "reference",
                ParameterValueSearchType = "string",
                Definition = "Tests whether the value in a resource matches the supplied parameter value using advanced text handling that searches text associated with the code/value - e.g., CodeableConcept.text, Coding.display, or Identifier.type.text.",
                Description = """
                    The text-advanced modifier allows clients to request matching based on advanced string processing of the search parameter input against the text associated with a code or value. For example, CodeableConcept.text, Coding.display, or Identifier.type.text. Implementers of the text-advanced modifier SHOULD support a sophisticated search functionality of the type offered by typical text indexing services, but MAY support only basic search with minor additions (e.g., word-boundary recognition). The value of the parameter is a text-based search, which may involve searching multiple words with thesaurus and proximity considerations, and logical operations such as AND, OR, etc.
                    """,
                Examples = """
                    GET [base]/Condition?code:text-advanced=headache
                        would match Condition resources containing codes with text that equals or begins with 'headache', case-insensitive, but would also match resources containing the word 'headache' such as 'Acute headache', 'Thunderclap headache', etc. A server may also return resources with synonymous text such as 'Migraine'.
                    """,
            },
            new()
            {
                Code = "text-advanced",
                TargetSearchType = "token",
                ParameterValueSearchType = "string",
                Definition = "Tests whether the value in a resource matches the supplied parameter value using advanced text handling that searches text associated with the code/value - e.g., CodeableConcept.text, Coding.display, or Identifier.type.text.",
                Description = """
                    The text-advanced modifier allows clients to request matching based on advanced string processing of the search parameter input against the text associated with a code or value. For example, CodeableConcept.text, Coding.display, or Identifier.type.text. Implementers of the text-advanced modifier SHOULD support a sophisticated search functionality of the type offered by typical text indexing services, but MAY support only basic search with minor additions (e.g., word-boundary recognition). The value of the parameter is a text-based search, which may involve searching multiple words with thesaurus and proximity considerations, and logical operations such as AND, OR, etc.
                    """,
                Examples = """
                    GET [base]/Condition?code:text-advanced=headache
                        would match Condition resources containing codes with text that equals or begins with 'headache', case-insensitive, but would also match resources containing the word 'headache' such as 'Acute headache', 'Thunderclap headache', etc. A server may also return resources with synonymous text such as 'Migraine'.
                    """,
            },
            new()
            {
                Code = "[type]",
                TargetSearchType = "reference",
                TargetFhirType = "Reference",
                ParameterValueSearchType = "reference",
                Definition = "Tests whether the value in a resource points to a resource of the supplied parameter type. Note: a concrete ResourceType is specified as the modifier (e.g., not the literal :[type], but a value such as :Patient).",
                Description = """
                    The [type] modifier allows clients to restrict the resource type when following a resource reference. The modifier does not use the literal '[type]' in any way, but rather the name of a resource - e.g., Patient, Encounter, etc. Note that the modifier cannot be used with a reference to a resource found on another server, since the server would not usually know what type that resource has. However, since external references are always absolute references, there can be no ambiguity about the type.
                    """,
                Examples = """
                    GET [base]/Observation?subject:Patient=23
                        for observations where the subject is 'Patient 23' is functionally equivalent to:
                    GET [base]/Observation?subject=Patient/23
                        as well as:
                    GET [base]/Observation?patient=23
                        However, the modifier becomes more useful when used with Chaining and Reverse Chaining of search parameters.
                    """,
            },
        ];
}

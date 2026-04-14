namespace fhir.candle.Mcp;

public static class SearchTypeData
{
    public readonly record struct FhirSearchTypeDescriptionRec(
        string Code,
        string? ArgumentFormat,
        bool AllowsPrefixes,
        string Definition,
        string Description,
        string Examples)
    {
        public override string ToString() =>
            $"{Code}: {ArgumentFormat}" +
            $"\n* Allows Prefixes: {AllowsPrefixes}" +
            $"\n* Definition: {Definition}" +
            $"\n* Description:\n{Description}" +
            $"\n* Examples:\n{Examples}";
    }

    static SearchTypeData()
    {
        SearchTypeDescriptions = SearchTypeDescriptionList.ToDictionary(r => r.Code);
    }

    public static readonly Dictionary<string, FhirSearchTypeDescriptionRec> SearchTypeDescriptions;
    public static readonly List<FhirSearchTypeDescriptionRec> SearchTypeDescriptionList = [
        new()
        {
            Code = "composite",
            ArgumentFormat = "[arg1]$[arg2]$...",
            AllowsPrefixes = false,
            Definition = "A composite search parameter that combines two or more other search parameters.",
            Description = """
                Composite search parameters allow joining multiple elements into distinct single values with a $. This is different from doing a simple intersection - the intersection rules apply at the resource level, so, for example, an Observation with multiple component repetitions may match because one repetition has a desired code and a different repetition matches a value filter.
                """,
            Examples = """
                GET [base]/DiagnosticReport?result.code-value-quantity=http://loinc.org|2823-3$gt5.4|http://unitsofmeasure.org|mmol/L
                    Search for all diagnostic reports that contain an observation with a potassium value of >5.4 mmol/L (UCUM)

                GET [base]/Observation?component-code-value-quantity=http://loinc.org|8480-6$lt60
                    Search for all the observations with a systolic blood pressure < 60. Note that in this case, the unit is assumed (everyone uses mmHg)

                GET [base]/Group?characteristic-value=gender$mixed
                    Search for all groups that have a characteristic "gender" with a text value of "mixed"

                GET [base]/Questionnaire?context-type-value=focus$http://snomed.info/sct|408934002
                    Search for all questionnaires that have a clinical focus = "Substance abuse prevention assessment (procedure)"
                """
        },
        new()
        {
            Code = "date",
            ArgumentFormat = "yyyy-mm-ddThh:mm:ss.ssss[Z|(+|-)hh:mm]",
            AllowsPrefixes = true,
            Definition = "Search parameter is on a date/time. The date format is the standard XML format, though other formats may be supported.",
            Description = """
                A date parameter searches on a date/time or period. As is usual for date/time related functionality, while the concepts are relatively straight-forward, there are a number of subtleties involved in ensuring consistent behavior.
                The date parameter format is yyyy-mm-ddThh:mm:ss.ssss[Z|(+|-)hh:mm] (the standard XML format). Note that fractional seconds MAY be ignored by servers.
                The date search type can be used to represent data from any of the date, dateTime, and instant datatypes. Any degree of precision can be provided, but it SHALL be populated from the left (e.g. can't specify a month without a year), except that the minutes SHALL be present if an hour is present, and you SHOULD provide a timezone if the time part is present. Note: Time can consist of hours and minutes with no seconds, unlike the XML Schema dateTime type. Some user agents may escape the : characters in the URL, and servers SHALL handle this correctly.
                Date searches are intrinsically matches against 'periods', regardless of the underlying element type
                """,
            Examples = """
                [parameter]=eq2013-01-14
                    2013-01-14T00:00 matches (obviously)
                    2013-01-14T10:00 matches
                    2013-01-15T00:00 does not match - it's not in the range
                [parameter]=ne2013-01-14
                    2013-01-15T00:00 matches - it's not in the range
                    2013-01-14T00:00 does not match - it's in the range
                    2013-01-14T10:00 does not match - it's in the range
                [parameter]=lt2013-01-14T10:00
                    2013-01-14 matches, because it includes the part of 14-Jan 2013 before 10am
                    "2013-01-13T12:00 to 2013-01-14T12:00" matches, because it includes the part of 14-Jan 2013 until noon
                    "2013-01-14T08:00 to 2013-01-15T08:00" matches, because it includes the part of 14-Jan 2013 before 10:00
                [parameter]=gt2013-01-14T10:00
                    2013-01-14 matches, because it includes the part of 14-Jan 2013 after 10am
                    "2013-01-13T12:00 to 2013-01-14T12:00" matches, because it includes the part of 14-Jan 2013 until noon
                    "2013-01-14T12:00 to 2013-01-15T12:00" matches, because it includes the part of 14-Jan 2013 after noon
                [parameter]=ge2013-03-14
                    "from 21-Jan 2013 onwards" is included because that period may include times after 14-Mar 2013
                [parameter]=le2013-03-14
                    "from 21-Jan 2013 onwards" is included because that period may include times before 14-Mar 2013
                [parameter]=sa2013-03-14
                    "from 15-Mar 2013 onwards" is included because that period starts after 14-Mar 2013
                    "from 21-Jan 2013 onwards" is not included because that period starts before 14-Mar 2013
                    "before and including 21-Jan 2013" is not included because that period starts (and ends) before 14-Mar 2013
                    "2013-01-13T12:00 to 2013-01-14T12:00" does not match, because it starts before 14-Jan 2013 begins
                    "2013-01-14T12:00 to 2013-01-15T12:00" does not match, because it starts before 14-Jan 2013 ends
                [parameter]=eb2013-03-14
                    "from 15-Mar 2013 onwards" is not included because that period starts after 14-Mar 2013
                    "from 21-Jan 2013 onwards" is not included because that period starts before 14-Mar 2013, but does not end before it
                    "before and including 21-Jan 2013" is included because that period ends before 14-Mar 2013
                    "2013-01-13T12:00 to 2013-01-14T12:00" does not match, because it ends after 14-Jan 2013 begins
                    "2013-01-14T12:00 to 2013-01-15T12:00" does not match, because it starts before 14-Jan 2013 ends
                [parameter]=ap2013-03-14
                    Note that actual comparison used for the ap prefix for date search types is an implementation detail. For these examples, the system will be based on a 10% difference between target and execution date, and execution on 01-Jan 2023.
                    14-Mar 2013 is included, because it exactly matches
                    21-Jan 2013 is included, because it is near 14-Mar 2013 (less than 10% difference between search date and current date)
                    15-Jun 2015 is not included, because it is not near 14-Mar 2013 (more than 10% difference between search date and current date)
                """,
        },
        new()
        {
            Code = "number",
            ArgumentFormat = null,
            AllowsPrefixes = true,
            Definition = "Search parameter SHALL be a number (a whole number, or a decimal).",
            Description = """
                A date parameter searches on a date/time or period. As is usual for date/time related functionality, while the concepts are relatively straight-forward, there are a number of subtleties involved in ensuring consistent behavior.
                The date parameter format is yyyy-mm-ddThh:mm:ss.ssss[Z|(+|-)hh:mm] (the standard XML format). Note that fractional seconds MAY be ignored by servers.
                The date search type can be used to represent data from any of the date, dateTime, and instant datatypes. Any degree of precision can be provided, but it SHALL be populated from the left (e.g. can't specify a month without a year), except that the minutes SHALL be present if an hour is present, and you SHOULD provide a timezone if the time part is present. Note: Time can consist of hours and minutes with no seconds, unlike the XML Schema dateTime type. Some user agents may escape the : characters in the URL, and servers SHALL handle this correctly.
                Date searches are intrinsically matches against 'periods', regardless of the underlying element type. For more information about how the different search prefixes work when comparing periods/ranges, refer to the Prefixes section.
                """,
            Examples = """
                [parameter]=100  Values that equal 100, to 3 significant figures precision, so this is actually searching for values in the range [99.5 ... 100.5)
                [parameter]=100.00	Values that equal 100, to 5 significant figures precision, so this is actually searching for values in the range [99.995 ... 100.005)
                [parameter]=1e2	Values that equal 100, to 1 significant figure precision, so this is actually searching for values in the range [50 ... 150)
                [parameter]=lt100	Values that are less than exactly 100
                [parameter]=le100	Values that are less or equal to exactly 100
                [parameter]=gt100	Values that are greater than exactly 100
                [parameter]=ge100	Values that are greater or equal to exactly 100
                [parameter]=ne100	Values that are not equal to 100 (actually, in the range 99.5 to 100.5)
                """,
        },
        new()
        {
            Code = "quantity",
            ArgumentFormat = "{[prefix]}[number]|[system]|[code]",
            AllowsPrefixes = true,
            Definition = "A search parameter that searches on a quantity.",
            Description = """
                A quantity parameter searches on the Quantity datatype. The syntax for the value follows the form (see Encoding Note):
                    [parameter]={[prefix]}[number] matches a quantity by value, with an optional prefix
                    [parameter]={[prefix]}[number]|[system]|[code] matches a quantity by value, system and code, with an optional prefix
                    [parameter]={[prefix]}[number]||[code] matches a quantity by value and code or unit, with an optional prefix
                The prefix is optional, and is as described in the section on Prefixes, both regarding how precision and comparator/range operators are interpreted. Like a number parameter, the number part of the search value can be a decimal in exponential format. The system and code follow the same pattern as token parameters are also optional. Note that when the [system] component has a value, it is implied that a precise (and potentially canonical) match is desired. In this case, it is inappropriate to search on the human display for the unit, which can be is uncontrolled and may unpredictable
                """,
            Examples = """
                GET [base]/Observation?value-quantity=5.4|http://unitsofmeasure.org|mg
                    Search for all the observations with a value of 5.4(+/-0.05) mg where mg is understood as a UCUM unit (system/code)

                GET [base]/Observation?value-quantity=5.40e-3|http://unitsofmeasure.org|g
                    Search for all the observations with a value of 0.0054(+/-0.00005) g where g is understood as a UCUM unit (system/code)

                GET [base]/Observation?value-quantity=5.4||mg
                    Search for all the observations with a value of 5.4(+/-0.05) mg where the unit - either the code (code) or the stated human unit (unit) are "mg"

                GET [base]/Observation?value-quantity=5.4
                    Search for all the observations with a value of 5.4(+/-0.05) irrespective of the unit

                GET [base]/Observation?value-quantity=le5.4|http://unitsofmeasure.org|mg
                    Search for all the observations where the value of is less than 5.4 mg exactly where mg is understood as a UCUM unit

                GET [base]/Observation?value-quantity=ap5.4|http://unitsofmeasure.org|mg
                    Search for all the observations where the value of is about 5.4 mg where mg is understood as a UCUM unit (typically, within 10% of the value - see above)
                """
        },
        new()
        {
            Code = "reference",
            ArgumentFormat = null,
            AllowsPrefixes = false,
            Definition = "A reference to another resource (Reference or canonical).",
            Description = """
                The reference search parameter type is used to search references between resources. For example, find all Conditions where the subject is a particular patient, where the patient is selected by name or identifier. The interpretation of a reference parameter is either (see Encoding Note):

                [parameter]=[id] the logical [id] of a resource using a local reference (i.e. a relative reference).
                [parameter]=[type]/[id] the logical [id] of a resource of a specified type using a local reference (i.e. a relative reference), for when the reference can point to different types of resources (e.g. Observation.subject).
                [parameter]=[type]/[id]/_history/[version] the logical [id] of a resource of a specified type using a local reference (i.e. a relative reference), for when the reference can point to different types of resources and a specific version is requested. Note that server implementations may return an error when using this syntax if resource versions are not supported. For more information, see References and Versions.
                [parameter]=[url] where the [url] is an absolute URL - a reference to a resource by its absolute location, or by its canonical URL
                [parameter]=[url]|[version] where the search element is a canonical reference, the [url] is an absolute URL, and a specific version or partial version is desired. For more information, see References and Versions.
                """,
            Examples = """
                GET http://example.org/fhir/Observation?subject=Patient/123
                    will match Observations with subject.reference values:
                        Patient/123 - exact match of search input
                        http://example.org/fhir/Patient/123 - search input with implicit resolution to the local server
                        Patient/123/_history/1 - reference to a specific version of the search input

                GET http://example.org/fhir/Observation?subject=http://example.org/fhir/Patient/123
                    will match Observations with subject.reference values:
                        http://example.org/fhir/Patient/123 - exact match of search input
                        Patient/123 - search input with implicit reference to the local server
                    Note that it will not match an Observation with Patient/123/_history/1, since the original reference was not a relative reference.

                GET http://example.org/fhir/Observation?subject=123
                    will match Observations with subject.reference values:
                        Patient/123 - search input of type Patient
                        http://example.org/fhir/Patient/123 - search input with implicit resolution to the local server, of type Patient
                        Practitioner/123 - search input of type Practitioner
                        http://example.org/fhir/Practitioner/123 - search input with implicit resolution to the local server, of type Practitioner
                        etc.

                GET [base]/Observation?subject:identifier=http://acme.org/fhir/identifier/mrn|123456
                    This is a search for all observations that reference a patient by a particular patient MRN. When the :identifier modifier is used, the search value works as a token search. The :identifier modifier is not supported on canonical elements since they do not have an identifier separate from the reference itself.
                """
        },
        new()
        {
            Code = "special",
            ArgumentFormat = null,
            AllowsPrefixes = false,
            Definition = "Special logic applies to this parameter per the description of the search parameter.",
            Description = """
                The way this parameter works is unique to the parameter and described with the parameter. The general modifiers and comparators do not apply, except as stated in the description.
                Implementers will generally need to do special implementations for these parameters.
                """,
            Examples = """
                GET [base]/Composition?section-text=june
                    Search on the section narrative of the resource for the word "june".
                """
        },
        new()
        {
            Code = "string",
            ArgumentFormat = null,
            AllowsPrefixes = false,
            Definition = "Search parameter is a simple string, like a name part. Search is case-insensitive and accent-insensitive. May match just the start of a string. String parameters may contain spaces.",
            Description = """
                For a simple string search, a string parameter serves as the input for a search against sequences of characters. This search is insensitive to casing and included combining characters, like accents or other diacritical marks. Punctuation and non-significant whitespace (e.g. repeated space characters, tab vs space) SHOULD also be ignored. Note that case-insensitive comparisons do not take locale into account, and will result in unsatisfactory results for certain locales. Character case definitions and conversions are out of scope for the FHIR standard, and the results of such operations are implementation dependent. By default, a field matches a string query if the value of the field equals or starts with the supplied parameter value, after both have been normalized by case and combining characters. Therefore, the default string search only operates on the base characters of the string parameter. The :contains modifier returns results that include the supplied parameter value anywhere within the field being searched. The :exact modifier returns results that match the entire supplied parameter, including casing and accents.
                """,
            Examples = """
                [parameter]=a	Values that start with 'a'
                [parameter]:exact=abc	Values that equal exactly 'abc'
                [parameter]:contains=bc	Values that contain 'bc'
                """,
        },
        new()
        {
            Code = "token",
            ArgumentFormat = "[system]|[code]",
            AllowsPrefixes = false,
            Definition = "Search parameter on a coded element or identifier. May be used to search through the text, display, code and code/codesystem (for codes) and label, system and key (for identifier). Its value is either a string or a pair of namespace and value, separated by a \"|\", depending on the modifier used.",
            Description = """
                A token type is a parameter that provides a close to exact match search on a string of characters, potentially scoped by a URI. It is mostly used against a code or identifier datatype where the value may have a URI that scopes its meaning, where the search is performed against the pair from a Coding or an Identifier. Tokens are also used against other fields where exact matches are required - uris, booleans, ContactPoints, and ids. In these cases the URI portion ([system]|) is not used (only the [code] portion).
                For tokens, matches are literal (e.g. not based on subsumption or other code system features). Match is case sensitive unless the underlying semantics for the context indicate that tokens are to be interpreted case-insensitively (see, e.g. CodeSystem.caseSensitive). Note that matches on _id are always case sensitive. If the underlying datatype is string then the search is not case sensitive.
                To use subsumption-based logic, use the modifiers below, or list all the codes in the hierarchy. The syntax for the value is one of the following (see Encoding Note):
                [parameter]=[code]: the value of [code] matches a Coding.code or Identifier.value irrespective of the value of the system property
                [parameter]=[system]|[code]: the value of [code] matches a Coding.code or Identifier.value, and the value of [system] matches the system property of the Identifier or Coding
                [parameter]=|[code]: the value of [code] matches a Coding.code or Identifier.value, and the Coding/Identifier has no system property
                [parameter]=[system]|: any element where the value of [system] matches the system property of the Identifier or Coding
                """,
            Examples = """
                GET [base]/Patient?identifier=http://acme.org/patient|2345
                Search for all the patients with an identifier with key = "2345" in the system "http://acme.org/patient"

                GET [base]/Patient?gender=male
                Search for any patient with a gender that has the code "male"

                GET [base]/Patient?gender:not=male
                Search for any patient with a gender that does not have the code "male", including those that do not have a code for gender at all.

                GET [base]/Composition?section=48765-2
                Search for any Composition that contains an Allergies and adverse reaction section

                GET [base]/Composition?section:not=48765-2
                Search for any Composition that does not contain an Allergies and adverse reaction section. Note that this search does not return "any document that has a section that is not an Allergies and adverse reaction section" (e.g. in the presence of multiple possible matches, the negation applies to the set, not each individual entry)

                GET [base]/Patient?active=true
                Search for any patients that are active

                GET [base]/Condition?code=http://acme.org/conditions/codes|ha125
                Search for any condition with a code "ha125" in the code system "http://acme.org/conditions/codes"

                GET [base]/Condition?code=ha125
                Search for any condition with a code "ha125". Note that there is not often any useful overlap in literal symbols between code systems, so the previous example is generally preferred

                GET [base]/Condition?code:text=headache
                Search for any Condition with a code that has a text "headache" associated with it (either in the text, or a display)

                GET [base]/Condition?code:in=http://snomed.info/sct?fhir_vs=isa/126851005
                Search for any condition in the SNOMED CT value set "http://snomed.info/sct?fhir_vs=isa/126851005" that includes all descendants of "Neoplasm of liver"

                GET [base]/Condition?code:below=126851005
                Search for any condition that is subsumed by the SNOMED CT Code "Neoplasm of liver". Note: This is the same outcome as the previous search

                GET [base]/Condition?code:in=http://acme.org/fhir/ValueSet/cardiac-conditions
                Search for any condition that is in the institutions list of cardiac conditions

                GET [base]/Patient?identifier:of-type=http://terminology.hl7.org/CodeSystem/v2-0203|MR|446053
                Search for the Medical Record Number 446053 - this is useful where the system id for the MRN is not known
                """,
        },
        new()
        {
            Code = "uri",
            ArgumentFormat = null,
            AllowsPrefixes = false,
            Definition = "A search parameter that searches on a URI (RFC 3986).",
            Description = """
                The uri search parameter type is used to search elements that contain a URI (RFC 3986 icon). By default, matches are precise, case and accent sensitive, and the entire URI must match. The modifier :above or :below can be used to indicate that partial matching is used.
                Note that the :above and :below modifiers only apply to URLs, and not URNS such as OIDs
                """,
            Examples = """
                GET [base]/ValueSet?url=http://acme.org/fhir/ValueSet/123
                    Match any value set with the exact url "http://acme.org/fhir/ValueSet/123"

                GET [base]/ValueSet?url:below=http://acme.org/fhir/
                    Match any value sets that have a URL that starts with "http://acme.org/fhir/"

                GET [base]/ValueSet?url:above=http://acme.org/fhir/ValueSet/123/_history/5
                    Match any value set above a given specific URL. This will match on any value set with the specified URL, but also on http://acme.org/ValueSet/123. Note that there are not many use cases where :above is useful as compared to the :below search

                GET [base]/ValueSet?url=urn:oid:1.2.3.4.5
                    Search by an OID.
                """
        },
    ];

}

using System.Text.Json;

namespace fhir.candle.McpTools;

public class McpData
{
    static McpData()
    {
        ResourceDescriptions = JsonSerializer.Deserialize<List<ResourceDescriptionRec>>(resourceDescriptionJson)!
            .ToDictionary(rd => rd.Name);
    }

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

    public static readonly Dictionary<string, FhirSearchTypeDescriptionRec> SearchTypeDescriptions = new()
    {
        {
            "number",
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
            }
        },
        {
            "date",
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
            }
        },
        {
            "string",
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
            }
        },
        {
            "token",
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
            }
        },
        {
            "reference",
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
            }
        },
        {
            "composite",
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
            }
        },
        {
            "quantity",
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
            }
        },
        {
            "uri",
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
            }
        },
        {
            "special",
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
            }
        }
    };

    public readonly record struct ResourceDescriptionRec(
        string FhirVersionShortName,
        string Name,
        string Title,
        string Description,
        string Comment
    )
    {
        public override string ToString() => $"{Name}: {Title} {Description} {Comment}";
    };

    public static readonly Dictionary<string, ResourceDescriptionRec> ResourceDescriptions;

    private const string resourceDescriptionJson = """
    [
        {
            "FhirVersionShortName": "R2",
            "Name": "Conformance",
            "Title": "A conformance statement",
            "Description": "Base StructureDefinition for Conformance Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R2",
            "Name": "DeviceUseRequest",
            "Title": "A request for a patient to use or be given a medical device",
            "Description": "Base StructureDefinition for DeviceUseRequest Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R2",
            "Name": "DiagnosticOrder",
            "Title": "A request for a diagnostic service",
            "Description": "Base StructureDefinition for DiagnosticOrder Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R2",
            "Name": "ImagingObjectSelection",
            "Title": "Key Object Selection",
            "Description": "Base StructureDefinition for ImagingObjectSelection Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R2",
            "Name": "MedicationOrder",
            "Title": "Prescription of medication to for patient",
            "Description": "Base StructureDefinition for MedicationOrder Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R2",
            "Name": "Order",
            "Title": "A request to perform an action",
            "Description": "Base StructureDefinition for Order Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R2",
            "Name": "OrderResponse",
            "Title": "A response to an order",
            "Description": "Base StructureDefinition for OrderResponse Resource",
            "Comment": "There might be more than one response to an order."
        },
        {
            "FhirVersionShortName": "R3",
            "Name": "BodySite",
            "Title": "Specific and identified anatomical location",
            "Description": "Base StructureDefinition for BodySite Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R3",
            "Name": "DataElement",
            "Title": "Resource data element",
            "Description": "Base StructureDefinition for DataElement Resource",
            "Comment": "Often called a clinical template."
        },
        {
            "FhirVersionShortName": "R3",
            "Name": "DeviceComponent",
            "Title": "An instance of a medical-related component of a medical device",
            "Description": "Base StructureDefinition for DeviceComponent Resource",
            "Comment": "For the initial scope, this DeviceComponent resource is only applicable to describe a single node in the containment tree that is produced by the context scanner in any medical device that implements or derives from the ISO/IEEE 11073 standard and that does not represent a metric. Examples for such a node are MDS, VMD, or Channel."
        },
        {
            "FhirVersionShortName": "R3",
            "Name": "EligibilityRequest",
            "Title": "Determine insurance validity and scope of coverage",
            "Description": "Base StructureDefinition for EligibilityRequest Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R3",
            "Name": "EligibilityResponse",
            "Title": "EligibilityResponse resource",
            "Description": "Base StructureDefinition for EligibilityResponse Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R3",
            "Name": "ExpansionProfile",
            "Title": "Defines behaviour and contraints on the ValueSet Expansion operation",
            "Description": "Base StructureDefinition for ExpansionProfile Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R3",
            "Name": "ImagingManifest",
            "Title": "Key Object Selection",
            "Description": "Base StructureDefinition for ImagingManifest Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R3",
            "Name": "ProcedureRequest",
            "Title": "A request for a procedure or diagnostic to be performed",
            "Description": "Base StructureDefinition for ProcedureRequest Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R3",
            "Name": "ProcessRequest",
            "Title": "Request to perform some action on or in regards to an existing resource",
            "Description": "Base StructureDefinition for ProcessRequest Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R3",
            "Name": "ProcessResponse",
            "Title": "ProcessResponse resource",
            "Description": "Base StructureDefinition for ProcessResponse Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R3",
            "Name": "ReferralRequest",
            "Title": "A request for referral or transfer of care",
            "Description": "Base StructureDefinition for ReferralRequest Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R3",
            "Name": "Sequence",
            "Title": "Information about a biological sequence",
            "Description": "Base StructureDefinition for Sequence Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R3",
            "Name": "ServiceDefinition",
            "Title": "A description of decision support service functionality",
            "Description": "Base StructureDefinition for ServiceDefinition Resource",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4",
            "Name": "EffectEvidenceSynthesis",
            "Title": "A quantified estimate of effect based on a body of evidence",
            "Description": "The EffectEvidenceSynthesis resource describes the difference in an outcome between exposures states in a population where the effect estimate is derived from a combination of research studies.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4",
            "Name": "MedicinalProduct",
            "Title": "Detailed definition of a medicinal product, typically for uses other than direct patient care (e.g. regulatory use)",
            "Description": "Detailed definition of a medicinal product, typically for uses other than direct patient care (e.g. regulatory use).",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4",
            "Name": "MedicinalProductAuthorization",
            "Title": "The regulatory authorization of a medicinal product",
            "Description": "The regulatory authorization of a medicinal product.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4",
            "Name": "MedicinalProductContraindication",
            "Title": "MedicinalProductContraindication",
            "Description": "The clinical particulars - indications, contraindications etc. of a medicinal product, including for regulatory purposes.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4",
            "Name": "MedicinalProductIndication",
            "Title": "MedicinalProductIndication",
            "Description": "Indication for the Medicinal Product.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4",
            "Name": "MedicinalProductIngredient",
            "Title": "An ingredient of a manufactured item or pharmaceutical product",
            "Description": "An ingredient of a manufactured item or pharmaceutical product.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4",
            "Name": "MedicinalProductInteraction",
            "Title": "MedicinalProductInteraction",
            "Description": "The interactions of the medicinal product with other medicinal products, or other forms of interactions.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4",
            "Name": "MedicinalProductManufactured",
            "Title": "The manufactured item as contained in the packaged medicinal product",
            "Description": "The manufactured item as contained in the packaged medicinal product.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4",
            "Name": "MedicinalProductPackaged",
            "Title": "A medicinal product in a container or package",
            "Description": "A medicinal product in a container or package.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4",
            "Name": "MedicinalProductPharmaceutical",
            "Title": "A pharmaceutical product described in terms of its composition and dose form",
            "Description": "A pharmaceutical product described in terms of its composition and dose form.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4",
            "Name": "MedicinalProductUndesirableEffect",
            "Title": "MedicinalProductUndesirableEffect",
            "Description": "Describe the undesirable effects of the medicinal product.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4",
            "Name": "RiskEvidenceSynthesis",
            "Title": "A quantified estimate of risk based on a body of evidence",
            "Description": "The RiskEvidenceSynthesis resource describes the likelihood of an outcome in a population plus exposure state where the risk estimate is derived from a combination of research studies.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4",
            "Name": "SubstanceSpecification",
            "Title": "The detailed description of a substance, typically at a level beyond what is used for prescribing",
            "Description": "The detailed description of a substance, typically at a level beyond what is used for prescribing.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4B",
            "Name": "CatalogEntry",
            "Title": "An entry in a catalog",
            "Description": "Catalog entries are wrappers that contextualize items included in a catalog.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4B",
            "Name": "DeviceUseStatement",
            "Title": "Record of use of a device",
            "Description": "A record of a device being used by a patient where the record is the result of a report from the patient or another clinician.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4B",
            "Name": "DocumentManifest",
            "Title": "A list that defines a set of documents",
            "Description": "A collection of documents compiled for a purpose together with metadata that applies to the collection.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4B",
            "Name": "Media",
            "Title": "A photo, video, or audio recording acquired or used in healthcare. The actual content may be inline or provided by direct reference",
            "Description": "A photo, video, or audio recording acquired or used in healthcare. The actual content may be inline or provided by direct reference.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4B",
            "Name": "RequestGroup",
            "Title": "A group of related requests",
            "Description": "A group of related requests that can be used to capture intended activities that have inter-dependencies such as \"give this medication after that one\".",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4B",
            "Name": "ResearchDefinition",
            "Title": "A research context or question",
            "Description": "The ResearchDefinition resource describes the conditional state (population and any exposures being compared within the population) and outcome (if specified) that the knowledge (evidence, assertion, recommendation) is about.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R4B",
            "Name": "ResearchElementDefinition",
            "Title": "A population, intervention, or exposure definition",
            "Description": "The ResearchElementDefinition resource describes a \"PICO\" element that knowledge (evidence, assertion, recommendation) is about.",
            "Comment": "PICO stands for Population (the population within which exposures are being compared), Intervention (the conditional state or exposure state being described for its effect on outcomes), Comparison (the alternative conditional state or alternative exposure state being compared against), and Outcome (the result or effect of the intervention in the population)."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Account",
            "Title": "Tracks balance, charges, for patient or cost center",
            "Description": "A financial tool for tracking value accrued for a particular purpose.  In the healthcare field, used to track charges for a patient, cost centers, etc.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ActivityDefinition",
            "Title": "The definition of a specific activity to be taken, independent of any particular patient or context",
            "Description": "This resource allows for the definition of some activity to be performed, independent of a particular patient, practitioner, or other performance context.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ActorDefinition",
            "Title": "An application that exchanges data",
            "Description": "The ActorDefinition resource is used to describe an actor - a human or an application that plays a role in data exchange, and that may have obligations associated with the role the actor plays.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "AdministrableProductDefinition",
            "Title": "A medicinal product in the final form, suitable for administration - after any mixing of multiple components",
            "Description": "A medicinal product in the final form which is suitable for administering to a patient (after any mixing of multiple components, dissolution etc. has been performed).",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "AdverseEvent",
            "Title": "An event that may be related to unintended effects on a patient or research participant",
            "Description": "An event (i.e. any change to current patient status) that may be related to unintended effects on a patient or research participant. The unintended effects may require additional monitoring, treatment, hospitalization, or may result in death. The AdverseEvent resource also extends to potential or avoided events that could have had such effects. There are two major domains where the AdverseEvent resource is expected to be used. One is in clinical care reported adverse events and the other is in reporting adverse events in clinical  research trial management.  Adverse events can be reported by healthcare providers, patients, caregivers or by medical products manufacturers.  Given the differences between these two concepts, we recommend consulting the domain specific implementation guides when implementing the AdverseEvent Resource. The implementation guides include specific extensions, value sets and constraints.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "AllergyIntolerance",
            "Title": "Allergy or Intolerance (generally: Risk of adverse reaction to a substance)",
            "Description": "Risk of harmful or undesirable, physiological response which is unique to an individual and associated with exposure to a substance.",
            "Comment": "Substances include, but are not limited to: a therapeutic substance administered correctly at an appropriate dosage for the individual; food; material derived from plants or animals; or venom from insect stings."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Appointment",
            "Title": "A booking of a healthcare event among patient(s), practitioner(s), related person(s) and/or device(s) for a specific date/time. This may result in one or more Encounter(s)",
            "Description": "A booking of a healthcare event among patient(s), practitioner(s), related person(s) and/or device(s) for a specific date/time. This may result in one or more Encounter(s).",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "AppointmentResponse",
            "Title": "A reply to an appointment request for a patient and/or practitioner(s), such as a confirmation or rejection",
            "Description": "A reply to an appointment request for a patient and/or practitioner(s), such as a confirmation or rejection.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ArtifactAssessment",
            "Title": "Adds metadata-supported comments, classifiers or ratings related to a Resource",
            "Description": "This Resource provides one or more comments, classifiers or ratings about a Resource and supports attribution and rights management metadata for the added content.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "AuditEvent",
            "Title": "Record of an event",
            "Description": "A record of an event relevant for purposes such as operations, privacy, security, maintenance, and performance analysis.",
            "Comment": "Based on IHE-ATNA."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Basic",
            "Title": "Resource for non-supported content",
            "Description": "Basic is used for handling concepts not yet defined in FHIR, narrative-only resources that don't map to an existing resource, and custom resources not appropriate for inclusion in the FHIR specification.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Binary",
            "Title": "Pure binary content defined by a format other than FHIR",
            "Description": "A resource that represents the data of a single raw artifact as digital content accessible in its native format.  A Binary resource can contain any content, whether text, image, pdf, zip archive, etc.",
            "Comment": "Typically, Binary resources are used for handling content such as:  \n\n* CDA Documents (i.e. with XDS) \n* PDF Documents \n* Images."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "BiologicallyDerivedProduct",
            "Title": "This resource reflects an instance of a biologically derived product",
            "Description": "A biological material originating from a biological entity intended to be transplanted or infused into another (possibly the same) biological entity.",
            "Comment": "Substances include, but are not limited to: whole blood, bone marrow, organs, and manipulated blood cells."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "BiologicallyDerivedProductDispense",
            "Title": "A record of dispensation of a biologically derived product",
            "Description": "A record of dispensation of a biologically derived product.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "BodyStructure",
            "Title": "Specific and identified anatomical structure",
            "Description": "Record details about an anatomical structure.  This resource may be used when a coded concept does not provide the necessary detail needed for the use case.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Bundle",
            "Title": "Contains a collection of resources",
            "Description": "A container for a collection of resources.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "CapabilityStatement",
            "Title": "A statement of system capabilities",
            "Description": "A Capability Statement documents a set of capabilities (behaviors) of a FHIR Server or Client for a particular version of FHIR that may be used as a statement of actual server functionality or a statement of required or desired server implementation.",
            "Comment": "Applications may implement multiple versions (see [Managing Multiple Versions](versioning.html), and the [$versions](capabilitystatement-operation-versions.html) operation). If they do, then a CapabilityStatement describes the system's support for a particular version of FHIR, and the server will have multiple statements, one for each version."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "CarePlan",
            "Title": "Healthcare plan for patient or group",
            "Description": "Describes the intention of how one or more practitioners intend to deliver care for a particular patient, group or community for a period of time, possibly limited to care for a specific condition or set of conditions.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "CareTeam",
            "Title": "Planned participants in the coordination and delivery of care",
            "Description": "The Care Team includes all the people and organizations who plan to participate in the coordination and delivery of care.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ChargeItem",
            "Title": "Item containing charge code(s) associated with the provision of healthcare provider products",
            "Description": "The resource ChargeItem describes the provision of healthcare provider products for a certain patient, therefore referring not only to the product, but containing in addition details of the provision, like date, time, amounts and participating organizations and persons. Main Usage of the ChargeItem is to enable the billing process and internal cost allocation.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ChargeItemDefinition",
            "Title": "Definition of properties and rules about how the price and the applicability of a ChargeItem can be determined",
            "Description": "The ChargeItemDefinition resource provides the properties that apply to the (billing) codes necessary to calculate costs and prices. The properties may differ largely depending on type and realm, therefore this resource gives only a rough structure and requires profiling for each type of billing code system.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Citation",
            "Title": "A description of identification, location, or contributorship of a publication (article or artifact)",
            "Description": "The Citation Resource enables reference to any knowledge artifact for purposes of identification and attribution. The Citation Resource supports existing reference structures and developing publication practices such as versioning, expressing complex contributorship roles, and referencing computable resources.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Claim",
            "Title": "Claim, Pre-determination or Pre-authorization",
            "Description": "A provider issued list of professional services and products which have been provided, or are to be provided, to a patient which is sent to an insurer for reimbursement.",
            "Comment": "The Claim resource fulfills three information request requirements: Claim - a request for adjudication for reimbursement for products and/or services provided; Preauthorization - a request to authorize the future provision of products and/or services including an anticipated adjudication; and, Predetermination - a request for a non-bind adjudication of possible future products and/or services."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ClaimResponse",
            "Title": "Response to a claim predetermination or preauthorization",
            "Description": "This resource provides the adjudication details from the processing of a Claim resource.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ClinicalImpression",
            "Title": "A clinical assessment performed when planning treatments and management strategies for a patient",
            "Description": "A record of a clinical assessment performed to determine what problem(s) may affect the patient and before planning the treatments or management strategies that are best to manage a patient's condition. Assessments are often 1:1 with a clinical consultation / encounter,  but this varies greatly depending on the clinical workflow. This resource is called \"ClinicalImpression\" rather than \"ClinicalAssessment\" to avoid confusion with the recording of assessment tools such as Apgar score.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ClinicalUseDefinition",
            "Title": "A single issue - either an indication, contraindication, interaction or an undesirable effect for a medicinal product, medication, device or procedure",
            "Description": "A single issue - either an indication, contraindication, interaction or an undesirable effect for a medicinal product, medication, device or procedure.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "CodeSystem",
            "Title": "Declares the existence of and describes a code system or code system supplement",
            "Description": "The CodeSystem resource is used to declare the existence of and describe a code system or code system supplement and its key properties, and optionally define a part or all of its content.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Communication",
            "Title": "A clinical or business level record of information being transmitted or shared",
            "Description": "A clinical or business level record of information being transmitted or shared; e.g. an alert that was sent to a responsible provider, a public health agency communication to a provider/reporter in response to a case report for a reportable condition.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "CommunicationRequest",
            "Title": "A request for information to be sent to a receiver",
            "Description": "A request to convey information; e.g. the CDS system proposes that an alert be sent to a responsible provider, the CDS system proposes that the public health agency be notified about a reportable condition.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "CompartmentDefinition",
            "Title": "Compartment Definition for a resource",
            "Description": "A compartment definition that defines how resources are accessed on a server.",
            "Comment": "In FHIR, search is not performed directly on a resource (by XML or JSON path), but on a named parameter that maps into the resource content."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Composition",
            "Title": "A set of resources composed into a single coherent clinical statement with clinical attestation",
            "Description": "A set of healthcare-related information that is assembled together into a single logical package that provides a single coherent statement of meaning, establishes its own context and that has clinical attestation with regard to who is making the statement. A Composition defines the structure and narrative content necessary for a document. However, a Composition alone does not constitute a document. Rather, the Composition must be the first entry in a Bundle where Bundle.type=document, and any other resources referenced from Composition must be included as subsequent entries in the Bundle (for example Patient, Practitioner, Encounter, etc.).",
            "Comment": "While the focus of this specification is on patient-specific clinical statements, this resource can also apply to other healthcare-related statements such as study protocol designs, healthcare invoices and other activities that are not necessarily patient-specific or clinical."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ConceptMap",
            "Title": "A map from one set of concepts to one or more other concepts",
            "Description": "A statement of relationships from one set of concepts to one or more other concepts - either concepts in code systems, or data element/data element concepts, or classes in class models.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Condition",
            "Title": "Detailed information about conditions, problems or diagnoses",
            "Description": "A clinical condition, problem, diagnosis, or other event, situation, issue, or clinical concept that has risen to a level of concern.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ConditionDefinition",
            "Title": "A definition of a condition",
            "Description": "A definition of a condition and information relevant to managing it.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Consent",
            "Title": "A healthcare consumer's  or third party's choices to permit or deny recipients or roles to perform actions for specific purposes and periods of time",
            "Description": "A record of a healthcare consumer’s  choices  or choices made on their behalf by a third party, which permits or denies identified recipient(s) or recipient role(s) to perform one or more actions within a given policy context, for specific purposes and periods of time.",
            "Comment": "Broadly, there are 3 key areas of consent for patients: Consent around sharing information (aka Privacy Consent Directive - Authorization to Collect, Use, or Disclose information), consent for specific treatment, or kinds of treatment and consent for research participation and data sharing."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Contract",
            "Title": "Legal Agreement",
            "Description": "Legally enforceable, formally recorded unilateral or bilateral directive i.e., a policy or agreement.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Coverage",
            "Title": "Insurance or medical plan or a payment agreement",
            "Description": "Financial instrument which may be used to reimburse or pay for health care products and services. Includes both insurance and self-payment.",
            "Comment": "The Coverage resource contains the insurance card level information, which is customary to provide on claims and other communications between providers and insurers."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "CoverageEligibilityRequest",
            "Title": "CoverageEligibilityRequest resource",
            "Description": "The CoverageEligibilityRequest provides patient and insurance coverage information to an insurer for them to respond, in the form of an CoverageEligibilityResponse, with information regarding whether the stated coverage is valid and in-force and optionally to provide the insurance details of the policy.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "CoverageEligibilityResponse",
            "Title": "CoverageEligibilityResponse resource",
            "Description": "This resource provides eligibility and plan details from the processing of an CoverageEligibilityRequest resource.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "DetectedIssue",
            "Title": "Clinical issue with action",
            "Description": "Indicates an actual or potential clinical issue with or between one or more active or proposed clinical actions for a patient; e.g. Drug-drug interaction, Ineffective treatment frequency, Procedure-condition conflict, gaps in care, etc.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Device",
            "Title": "Item used in healthcare",
            "Description": "This resource describes the properties (regulated, has real time clock, etc.), adminstrative (manufacturer name, model number, serial number, firmware, etc.), and type (knee replacement, blood pressure cuff, MRI, etc.) of a physical unit (these values do not change much within a given module, for example the serail number, manufacturer name, and model number). An actual unit may consist of several modules in a distinct hierarchy and these are represented by multiple Device resources and bound through the 'parent' element.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "DeviceAssociation",
            "Title": "A record of association or dissociation of a device with a patient",
            "Description": "A record of association of a device.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "DeviceDefinition",
            "Title": "An instance of a medical-related component of a medical device",
            "Description": "This is a specialized resource that defines the characteristics and capabilities of a device.",
            "Comment": "For the initial scope, this DeviceDefinition resource is only applicable to describe a single node in the containment tree that is produced by the context scanner in any medical device that implements or derives from the ISO/IEEE 11073 standard and that does not represent a metric. Examples for such a node are MDS, VMD, or Channel."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "DeviceDispense",
            "Title": "A record of dispensation of a device",
            "Description": "Indicates that a device is to be or has been dispensed for a named person/patient.  This includes a description of the product (supply) provided and the instructions for using the device.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "DeviceMetric",
            "Title": "Measurement, calculation or setting capability of a medical device",
            "Description": "Describes a measurement, calculation or setting capability of a device.  The DeviceMetric resource is derived from the ISO/IEEE 11073-10201 Domain Information Model standard, but is more widely applicable. ",
            "Comment": "The DeviceMetric resource is derived from the ISO/IEEE 11073-10201 Domain Information Model standard, but is more widely applicable."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "DeviceRequest",
            "Title": "Medical device request",
            "Description": "Represents a request a device to be provided to a specific patient. The device may be an implantable device to be subsequently implanted, or an external assistive device, such as a walker, to be delivered and subsequently be used.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "DeviceUsage",
            "Title": "Record of use of a device",
            "Description": "A record of a device being used by a patient where the record is the result of a report from the patient or a clinician.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "DiagnosticReport",
            "Title": "A Diagnostic report - a combination of request information, atomic results, images, interpretation, as well as formatted reports",
            "Description": "The findings and interpretation of diagnostic tests performed on patients, groups of patients, products, substances, devices, and locations, and/or specimens derived from these. The report includes clinical context such as requesting provider information, and some mix of atomic results, images, textual and coded interpretations, and formatted representation of diagnostic reports. The report also includes non-clinical context such as batch analysis and stability reporting of products and substances.",
            "Comment": "This is intended to capture a single report and is not suitable for use in displaying summary information that covers multiple reports.  For example, this resource has not been designed for laboratory cumulative reporting formats nor detailed structured reports for sequencing."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "DocumentReference",
            "Title": "A reference to a document",
            "Description": "A reference to a document of any kind for any purpose. While the term “document” implies a more narrow focus, for this resource this “document” encompasses *any* serialized object with a mime-type, it includes formal patient-centric documents (CDA), clinical notes, scanned paper, non-patient specific documents like policy text, as well as a photo, video, or audio recording acquired or used in healthcare.  The DocumentReference resource provides metadata about the document so that the document can be discovered and managed.  The actual content may be inline base64 encoded data or provided by direct reference.",
            "Comment": "Usually, this is used for documents other than those defined by FHIR."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "DomainResource",
            "Title": "A resource with narrative, extensions, and contained resources",
            "Description": "A resource that includes narrative, extensions, and contained resources.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Encounter",
            "Title": "An interaction during which services are provided to the patient",
            "Description": "An interaction between healthcare provider(s), and/or patient(s) for the purpose of providing healthcare service(s) or assessing the health status of patient(s).",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "EncounterHistory",
            "Title": "A record of significant events/milestones key data throughout the history of an Encounter",
            "Description": "A record of significant events/milestones key data throughout the history of an Encounter",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Endpoint",
            "Title": "The technical details of an endpoint that can be used for electronic services",
            "Description": "The technical details of an endpoint that can be used for electronic services, such as for web services providing XDS.b, a REST endpoint for another FHIR server, or a s/Mime email address. This may include any security context information.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "EnrollmentRequest",
            "Title": "Enroll in coverage",
            "Description": "This resource provides the insurance enrollment details to the insurer regarding a specified coverage.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "EnrollmentResponse",
            "Title": "EnrollmentResponse resource",
            "Description": "This resource provides enrollment and plan details from the processing of an EnrollmentRequest resource.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "EpisodeOfCare",
            "Title": "An association of a Patient with an Organization and  Healthcare Provider(s) for a period of time that the Organization assumes some level of responsibility",
            "Description": "An association between a patient and an organization / healthcare provider(s) during which time encounters may occur. The managing organization assumes a level of responsibility for the patient during this time.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "EventDefinition",
            "Title": "A description of when an event can occur",
            "Description": "The EventDefinition resource provides a reusable description of when a particular event can occur.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Evidence",
            "Title": "Single evidence bit",
            "Description": "The Evidence Resource provides a machine-interpretable expression of an evidence concept including the evidence variables (e.g., population, exposures/interventions, comparators, outcomes, measured variables, confounding variables), the statistics, and the certainty of this evidence.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "EvidenceReport",
            "Title": "A EvidenceReport",
            "Description": "The EvidenceReport Resource is a specialized container for a collection of resources and codeable concepts, adapted to support compositions of Evidence, EvidenceVariable, and Citation resources and related concepts.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "EvidenceVariable",
            "Title": "A definition of an exposure, outcome, or other variable",
            "Description": "The EvidenceVariable resource describes an element that knowledge (Evidence) is about.",
            "Comment": "The EvidenceVariable may be an exposure variable (intervention, condition, or state), a measured variable (outcome or observed parameter), or other variable (such as confounding factor)."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ExampleScenario",
            "Title": "Example of workflow instance",
            "Description": "A walkthrough of a workflow showing the interaction between systems and the instances shared, possibly including the evolution of instances over time.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ExplanationOfBenefit",
            "Title": "Explanation of Benefit resource",
            "Description": "This resource provides: the claim details; adjudication details from the processing of a Claim; and optionally account balance information, for informing the subscriber of the benefits provided.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "FamilyMemberHistory",
            "Title": "Information about patient's relatives, relevant for patient",
            "Description": "Significant health conditions for a person related to the patient relevant in the context of care for the patient.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Flag",
            "Title": "Key information to flag to healthcare providers",
            "Description": "Prospective warnings of potential issues when providing care to the patient.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "FormularyItem",
            "Title": "Definition of a FormularyItem",
            "Description": "This resource describes a product or service that is available through a program and includes the conditions and constraints of availability.  All of the information in this resource is specific to the inclusion of the item in the formulary and is not inherent to the item itself.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "GenomicStudy",
            "Title": "Genomic Study",
            "Description": "A set of analyses performed to analyze and generate genomic data.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Goal",
            "Title": "Describes the intended objective(s) for a patient, group or organization",
            "Description": "Describes the intended objective(s) for a patient, group or organization care, for example, weight loss, restoring an activity of daily living, obtaining herd immunity via immunization, meeting a process improvement objective, etc.",
            "Comment": "Goal can be achieving a particular change or merely maintaining a current state or even slowing a decline."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "GraphDefinition",
            "Title": "Definition of a graph of resources",
            "Description": "A formal computable definition of a graph of resources - that is, a coherent set of resources that form a graph by following references. The Graph Definition resource defines a set and makes rules about the set.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Group",
            "Title": "Group of multiple entities",
            "Description": "Represents a defined collection of entities that may be discussed or acted upon collectively but which are not expected to act collectively, and are not formally or legally recognized; i.e. a collection of entities that isn't an Organization.",
            "Comment": "If both Group.characteristic and Group.member are present, then the members are the individuals who were found who met the characteristic.  It's possible that there might be other candidate members who meet the characteristic and aren't (yet) in the list.  All members SHALL have the listed characteristics."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "GuidanceResponse",
            "Title": "The formal response to a guidance request",
            "Description": "A guidance response is the formal response to a guidance request, including any output parameters returned by the evaluation, as well as the description of any proposed actions to be taken.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "HealthcareService",
            "Title": "The details of a healthcare service available at a location",
            "Description": "The details of a healthcare service available at a location or in a catalog.  In the case where there is a hierarchy of services (for example, Lab -> Pathology -> Wound Cultures), this can be represented using a set of linked HealthcareServices.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ImagingSelection",
            "Title": "A selection of DICOM SOP instances and/or frames",
            "Description": "A selection of DICOM SOP instances and/or frames within a single Study and Series. This might include additional specifics such as an image region, an Observation UID or a Segmentation Number, allowing linkage to an Observation Resource or transferring this information along with the ImagingStudy Resource.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ImagingStudy",
            "Title": "A set of images produced in single study (one or more series of references images)",
            "Description": "Representation of the content produced in a DICOM imaging study. A study comprises a set of series, each of which includes a set of Service-Object Pair Instances (SOP Instances - images or other data) acquired or produced in a common context.  A series is of only one modality (e.g. X-ray, CT, MR, ultrasound), but a study may have multiple series of different modalities.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Immunization",
            "Title": "Immunization event information",
            "Description": "Describes the event of a patient being administered a vaccine or a record of an immunization as reported by a patient, a clinician or another party.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ImmunizationEvaluation",
            "Title": "Immunization evaluation information",
            "Description": "Describes a comparison of an immunization event against published recommendations to determine if the administration is \"valid\" in relation to those  recommendations.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ImmunizationRecommendation",
            "Title": "Guidance or advice relating to an immunization",
            "Description": "A patient's point-in-time set of recommendations (i.e. forecasting) according to a published schedule with optional supporting justification.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ImplementationGuide",
            "Title": "A set of rules about how FHIR is used",
            "Description": "A set of rules of how a particular interoperability or standards problem is solved - typically through the use of FHIR resources. This resource is used to gather all the parts of an implementation guide into a logical whole and to publish a computable definition of all the parts.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Ingredient",
            "Title": "An ingredient of a manufactured item or pharmaceutical product",
            "Description": "An ingredient of a manufactured item or pharmaceutical product.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "InsurancePlan",
            "Title": "Details of a Health Insurance product/plan provided by an organization",
            "Description": "Details of a Health Insurance product/plan provided by an organization.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "InventoryItem",
            "Title": "A functional description of an inventory item used in inventory and supply-related workflows",
            "Description": "functional description of an inventory item used in inventory and supply-related workflows.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "InventoryReport",
            "Title": "A report of inventory or stock items",
            "Description": "A report of inventory or stock items.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Invoice",
            "Title": "Invoice containing ChargeItems from an Account",
            "Description": "Invoice containing collected ChargeItems from an Account with calculated individual and total price for Billing purpose.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Library",
            "Title": "Represents a library of quality improvement components",
            "Description": "The Library resource is a general-purpose container for knowledge asset definitions. It can be used to describe and expose existing knowledge assets such as logic libraries and information model descriptions, as well as to describe a collection of knowledge assets.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Linkage",
            "Title": "Links records for 'same' item",
            "Description": "Identifies two or more records (resource instances) that refer to the same real-world \"occurrence\".",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "List",
            "Title": "A list is a curated collection of resources",
            "Description": "A List is a curated collection of resources, for things such as problem lists, allergy lists, facility list, organization list, etc.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Location",
            "Title": "Details and position information for a place",
            "Description": "Details and position information for a place where services are provided and resources and participants may be stored, found, contained, or accommodated.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ManufacturedItemDefinition",
            "Title": "The definition and characteristics of a medicinal manufactured item, such as a tablet or capsule, as contained in a packaged medicinal product",
            "Description": "The definition and characteristics of a medicinal manufactured item, such as a tablet or capsule, as contained in a packaged medicinal product.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Measure",
            "Title": "A quality measure definition",
            "Description": "The Measure resource provides the definition of a quality measure.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "MeasureReport",
            "Title": "Results of a measure evaluation",
            "Description": "The MeasureReport resource contains the results of the calculation of a measure; and optionally a reference to the resources involved in that calculation.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Medication",
            "Title": "Definition of a Medication",
            "Description": "This resource is primarily used for the identification and definition of a medication, including ingredients, for the purposes of prescribing, dispensing, and administering a medication as well as for making statements about medication use.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "MedicationAdministration",
            "Title": "Administration of medication to a patient",
            "Description": "Describes the event of a patient consuming or otherwise being administered a medication.  This may be as simple as swallowing a tablet or it may be a long running infusion. Related resources tie this event to the authorizing prescription, and the specific encounter between patient and health care practitioner. This event can also be used to record waste using a status of not-done and the appropriate statusReason.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "MedicationDispense",
            "Title": "Dispensing a medication to a named patient",
            "Description": "Indicates that a medication product is to be or has been dispensed for a named person/patient.  This includes a description of the medication product (supply) provided and the instructions for administering the medication.  The medication dispense is the result of a pharmacy system responding to a medication order.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "MedicationKnowledge",
            "Title": "Definition of Medication Knowledge",
            "Description": "Information about a medication that is used to support knowledge.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "MedicationRequest",
            "Title": "Ordering of medication for patient or group",
            "Description": "An order or request for both supply of the medication and the instructions for administration of the medication to a patient. The resource is called \"MedicationRequest\" rather than \"MedicationPrescription\" or \"MedicationOrder\" to generalize the use across inpatient and outpatient settings, including care plans, etc., and to harmonize with workflow patterns.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "MedicationStatement",
            "Title": "Record of medication being taken by a patient",
            "Description": "A record of a medication that is being consumed by a patient.   A MedicationStatement may indicate that the patient may be taking the medication now or has taken the medication in the past or will be taking the medication in the future.  The source of this information can be the patient, significant other (such as a family member or spouse), or a clinician.  A common scenario where this information is captured is during the history taking process during a patient visit or stay.   The medication information may come from sources such as the patient's memory, from a prescription bottle,  or from a list of medications the patient, clinician or other party maintains. \n\nThe primary difference between a medicationstatement and a medicationadministration is that the medication administration has complete administration information and is based on actual administration information from the person who administered the medication.  A medicationstatement is often, if not always, less specific.  There is no required date/time when the medication was administered, in fact we only know that a source has reported the patient is taking this medication, where details such as time, quantity, or rate or even medication product may be incomplete or missing or less precise.  As stated earlier, the Medication Statement information may come from the patient's memory, from a prescription bottle or from a list of medications the patient, clinician or other party maintains.  Medication administration is more formal and is not missing detailed information.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "MedicinalProductDefinition",
            "Title": "Detailed definition of a medicinal product",
            "Description": "Detailed definition of a medicinal product, typically for uses other than direct patient care (e.g. regulatory use, drug catalogs, to support prescribing, adverse events management etc.).",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "MessageDefinition",
            "Title": "A resource that defines a type of message that can be exchanged between systems",
            "Description": "Defines the characteristics of a message that can be shared between systems, including the type of event that initiates the message, the content to be transmitted and what response(s), if any, are permitted.",
            "Comment": "This would be a MIF-level artifact."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "MessageHeader",
            "Title": "A resource that describes a message that is exchanged between systems",
            "Description": "The header for a message exchange that is either requesting or responding to an action.  The reference(s) that are the subject of the action as well as other information related to the action are typically transmitted in a bundle in which the MessageHeader resource instance is the first resource in the bundle.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "MolecularSequence",
            "Title": "Representation of a molecular sequence",
            "Description": "Representation of a molecular sequence.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "NamingSystem",
            "Title": "System of unique identification",
            "Description": "A curated namespace that issues unique symbols within that namespace for the identification of concepts, people, devices, etc.  Represents a \"System\" used within the Identifier and Coding data types.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "NutritionIntake",
            "Title": "Record of food or fluid being taken by a patient",
            "Description": "A record of food or fluid that is being consumed by a patient.  A NutritionIntake may indicate that the patient may be consuming the food or fluid now or has consumed the food or fluid in the past.  The source of this information can be the patient, significant other (such as a family member or spouse), or a clinician.  A common scenario where this information is captured is during the history taking process during a patient visit or stay or through an app that tracks food or fluids consumed.   The consumption information may come from sources such as the patient's memory, from a nutrition label,  or from a clinician documenting observed intake.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "NutritionOrder",
            "Title": "Diet, formula or nutritional supplement request",
            "Description": "A request to supply a diet, formula feeding (enteral) or oral nutritional supplement to a patient/resident.",
            "Comment": "Referenced by an Order Request (workflow)."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "NutritionProduct",
            "Title": "A product used for nutritional purposes (i.e. food or supplement)",
            "Description": "A food or supplement that is consumed by patients.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Observation",
            "Title": "Measurements and simple assertions",
            "Description": "Measurements and simple assertions made about a patient, device or other subject.",
            "Comment": "Used for simple observations such as device measurements, laboratory atomic results, vital signs, height, weight, smoking status, comments, etc.  Other resources are used to provide context for observations such as laboratory reports, etc."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ObservationDefinition",
            "Title": "Definition of an observation",
            "Description": "Set of definitional characteristics for a kind of observation or measurement produced or consumed by an orderable health care service.",
            "Comment": "An instance of this resource informs the consumer of a health-related service (such as a lab diagnostic test or panel) about how the observations used or produced by this service will look like."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "OperationDefinition",
            "Title": "Definition of an operation or a named query",
            "Description": "A formal computable definition of an operation (on the RESTful interface) or a named query (using the search interaction).",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "OperationOutcome",
            "Title": "Information about the success/failure of an action",
            "Description": "A collection of error, warning, or information messages that result from a system action.",
            "Comment": "Can result from the failure of a REST call or be part of the response message returned from a request message."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Organization",
            "Title": "A grouping of people or organizations with a common purpose",
            "Description": "A formally or informally recognized grouping of people or organizations formed for the purpose of achieving some form of collective action.  Includes companies, institutions, corporations, departments, community groups, healthcare practice groups, payer/insurer, etc.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "OrganizationAffiliation",
            "Title": "Defines an affiliation/association/relationship between 2 distinct organizations, that is not a part-of relationship/sub-division relationship",
            "Description": "Defines an affiliation/assotiation/relationship between 2 distinct organizations, that is not a part-of relationship/sub-division relationship.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "PackagedProductDefinition",
            "Title": "A medically related item or items, in a container or package",
            "Description": "A medically related item or items, in a container or package.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Parameters",
            "Title": "Operation Request or Response",
            "Description": "This resource is used to pass information into and back from an operation (whether invoked directly from REST or within a messaging environment).  It is not persisted or allowed to be referenced by other resources except as described in the definition of the Parameters resource.",
            "Comment": "The parameters that may be used are defined by the OperationDefinition resource."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Patient",
            "Title": "Information about an individual or animal receiving health care services",
            "Description": "Demographics and other administrative information about an individual or animal receiving care or other health-related services.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "PaymentNotice",
            "Title": "PaymentNotice request",
            "Description": "This resource provides the status of the payment for goods and services rendered, and the request and response resource references.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "PaymentReconciliation",
            "Title": "PaymentReconciliation resource",
            "Description": "This resource provides the details including amount of a payment and allocates the payment items being paid.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Permission",
            "Title": "Access Rules",
            "Description": "Permission resource holds access rules for a given data and context.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Person",
            "Title": "A generic person record",
            "Description": "Demographics and administrative information about a person independent of a specific health-related context.",
            "Comment": "The Person resource does justice to person registries that keep track of persons regardless of their role. The Person resource is also a primary resource to point to for people acting in a particular role such as SubjectofCare, Practitioner, and Agent. Very few attributes are specific to any role and so Person is kept lean. Most attributes are expected to be tied to the role the Person plays rather than the Person himself. Examples of that are Guardian (SubjectofCare), ContactParty (SubjectOfCare, Practitioner), and multipleBirthInd (SubjectofCare)."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "PlanDefinition",
            "Title": "The definition of a plan for a series of actions, independent of any specific patient or context",
            "Description": "This resource allows for the definition of various types of plans as a sharable, consumable, and executable artifact. The resource is general enough to support the description of a broad range of clinical and non-clinical artifacts such as clinical decision support rules, order sets, protocols, and drug quality specifications.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Practitioner",
            "Title": "A person with a  formal responsibility in the provisioning of healthcare or related services",
            "Description": "A person who is directly or indirectly involved in the provisioning of healthcare or related services.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "PractitionerRole",
            "Title": "Roles/organizations the practitioner is associated with",
            "Description": "A specific set of Roles/Locations/specialties/services that a practitioner may perform, or has performed at an organization during a period of time.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Procedure",
            "Title": "An action that is being or was performed on an individual or entity",
            "Description": "An action that is or was performed on or for a patient, practitioner, device, organization, or location. For example, this can be a physical intervention on a patient like an operation, or less invasive like long term services, counseling, or hypnotherapy.  This can be a quality or safety inspection for a location, organization, or device.  This can be an accreditation procedure on a practitioner for licensing.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Provenance",
            "Title": "Who, What, When for a set of resources",
            "Description": "Provenance of a resource is a record that describes entities and processes involved in producing and delivering or otherwise influencing that resource. Provenance provides a critical foundation for assessing authenticity, enabling trust, and allowing reproducibility. Provenance assertions are a form of contextual metadata and can themselves become important records with their own provenance. Provenance statement indicates clinical significance in terms of confidence in authenticity, reliability, and trustworthiness, integrity, and stage in lifecycle (e.g. Document Completion - has the artifact been legally authenticated), all of which may impact security, privacy, and trust policies.",
            "Comment": "Some parties may be duplicated between the target resource and its provenance.  For instance, the prescriber is usually (but not always) the author of the prescription resource. This resource is defined with close consideration for W3C Provenance."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Questionnaire",
            "Title": "A structured set of questions",
            "Description": "A structured set of questions intended to guide the collection of answers from end-users. Questionnaires provide detailed control over order, presentation, phraseology and grouping to allow coherent, consistent data collection.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "QuestionnaireResponse",
            "Title": "A structured set of questions and their answers",
            "Description": "A structured set of questions and their answers. The questions are ordered and grouped into coherent subsets, corresponding to the structure of the grouping of the questionnaire being responded to.",
            "Comment": "The QuestionnaireResponse contains enough information about the questions asked and their organization that it can be interpreted somewhat independently from the Questionnaire it is based on.  I.e. You don't need access to the Questionnaire in order to extract basic information from a QuestionnaireResponse."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "RegulatedAuthorization",
            "Title": "Regulatory approval, clearance or licencing related to a regulated product, treatment, facility or activity e.g. Market Authorization for a Medicinal Product",
            "Description": "Regulatory approval, clearance or licencing related to a regulated product, treatment, facility or activity that is cited in a guidance, regulation, rule or legislative act. An example is Market Authorization relating to a Medicinal Product.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "RelatedPerson",
            "Title": "A person that is related to a patient, but who is not a direct target of care",
            "Description": "Information about a person that is involved in a patient's health or the care for a patient, but who is not the target of healthcare, nor has a formal responsibility in the care process.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "RequestOrchestration",
            "Title": "A set of related requests",
            "Description": "A set of related requests that can be used to capture intended activities that have inter-dependencies such as \"give this medication after that one\".",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Requirements",
            "Title": "A set of requirements - features of systems that are necessary",
            "Description": "The Requirements resource is used to describe an actor - a human or an application that plays a role in data exchange, and that may have obligations associated with the role the actor plays.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ResearchStudy",
            "Title": "Investigation to increase healthcare-related patient-independent knowledge",
            "Description": "A scientific study of nature that sometimes includes processes involved in health and disease. For example, clinical trials are research studies that involve people. These studies may be related to new ways to screen, prevent, diagnose, and treat disease. They may also study certain outcomes and certain groups of people by looking at data collected in the past or future.",
            "Comment": "Need to make sure we encompass public health studies."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ResearchSubject",
            "Title": "Participant or object which is the recipient of investigative activities in a study",
            "Description": "A ResearchSubject is a participant or object which is the recipient of investigative activities in a research study.",
            "Comment": "Need to make sure we encompass public health studies."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Resource",
            "Title": "Base Resource",
            "Description": "This is the base resource type for everything.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "RiskAssessment",
            "Title": "Potential outcomes for a subject with likelihood",
            "Description": "An assessment of the likely outcome(s) for a patient or other subject as well as the likelihood of each outcome.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Schedule",
            "Title": "A container for slots of time that may be available for booking appointments",
            "Description": "A container for slots of time that may be available for booking appointments.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "SearchParameter",
            "Title": "Search parameter for a resource",
            "Description": "A search parameter that defines a named search item that can be used to search/filter on a resource.",
            "Comment": "In FHIR, search is not performed directly on a resource (by XML or JSON path), but on a named parameter that maps into the resource content."
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ServiceRequest",
            "Title": "A request for a service to be performed",
            "Description": "A record of a request for service such as diagnostic investigations, treatments, or operations to be performed.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Slot",
            "Title": "A slot of time on a schedule that may be available for booking appointments",
            "Description": "A slot of time on a schedule that may be available for booking appointments.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Specimen",
            "Title": "Sample for analysis",
            "Description": "A sample to be used for analysis.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "SpecimenDefinition",
            "Title": "Kind of specimen",
            "Description": "A kind of specimen with associated set of requirements.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "StructureDefinition",
            "Title": "Structural Definition",
            "Description": "A definition of a FHIR structure. This resource is used to describe the underlying resources, data types defined in FHIR, and also for describing extensions and constraints on resources and data types.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "StructureMap",
            "Title": "A Map of relationships between 2 structures that can be used to transform data",
            "Description": "A Map of relationships between 2 structures that can be used to transform data.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Subscription",
            "Title": "Notification about a SubscriptionTopic",
            "Description": "The subscription resource describes a particular client's request to be notified about a SubscriptionTopic.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "SubscriptionStatus",
            "Title": "Status information about a Subscription provided during event notification",
            "Description": "The SubscriptionStatus resource describes the state of a Subscription during notifications. It is not persisted.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "SubscriptionTopic",
            "Title": "The definition of a specific topic for triggering events within the Subscriptions framework",
            "Description": "Describes a stream of resource state changes identified by trigger criteria and annotated with labels useful to filter projections from this topic.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Substance",
            "Title": "A homogeneous material with a definite composition",
            "Description": "A homogeneous material with a definite composition.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "SubstanceDefinition",
            "Title": "The detailed description of a substance, typically at a level beyond what is used for prescribing",
            "Description": "The detailed description of a substance, typically at a level beyond what is used for prescribing.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "SubstanceNucleicAcid",
            "Title": "Nucleic acids are defined by three distinct elements: the base, sugar and linkage. Individual substance/moiety IDs will be created for each of these elements. The nucleotide sequence will be always entered in the 5’-3’ direction",
            "Description": "Nucleic acids are defined by three distinct elements: the base, sugar and linkage. Individual substance/moiety IDs will be created for each of these elements. The nucleotide sequence will be always entered in the 5’-3’ direction.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "SubstancePolymer",
            "Title": "Properties of a substance specific to it being a polymer",
            "Description": "Properties of a substance specific to it being a polymer.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "SubstanceProtein",
            "Title": "A SubstanceProtein is defined as a single unit of a linear amino acid sequence, or a combination of subunits that are either covalently linked or have a defined invariant stoichiometric relationship. This includes all synthetic, recombinant and purified SubstanceProteins of defined sequence, whether the use is therapeutic or prophylactic. This set of elements will be used to describe albumins, coagulation factors, cytokines, growth factors, peptide/SubstanceProtein hormones, enzymes, toxins, toxoids, recombinant vaccines, and immunomodulators",
            "Description": "A SubstanceProtein is defined as a single unit of a linear amino acid sequence, or a combination of subunits that are either covalently linked or have a defined invariant stoichiometric relationship. This includes all synthetic, recombinant and purified SubstanceProteins of defined sequence, whether the use is therapeutic or prophylactic. This set of elements will be used to describe albumins, coagulation factors, cytokines, growth factors, peptide/SubstanceProtein hormones, enzymes, toxins, toxoids, recombinant vaccines, and immunomodulators.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "SubstanceReferenceInformation",
            "Title": "Todo",
            "Description": "Todo.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "SubstanceSourceMaterial",
            "Title": "Source material shall capture information on the taxonomic and anatomical origins as well as the fraction of a material that can result in or can be modified to form a substance. This set of data elements shall be used to define polymer substances isolated from biological matrices. Taxonomic and anatomical origins shall be described using a controlled vocabulary as required. This information is captured for naturally derived polymers ( . starch) and structurally diverse substances. For Organisms belonging to the Kingdom Plantae the Substance level defines the fresh material of a single species or infraspecies, the Herbal Drug and the Herbal preparation. For Herbal preparations, the fraction information will be captured at the Substance information level and additional information for herbal extracts will be captured at the Specified Substance Group 1 information level. See for further explanation the Substance Class: Structurally Diverse and the herbal annex",
            "Description": "Source material shall capture information on the taxonomic and anatomical origins as well as the fraction of a material that can result in or can be modified to form a substance. This set of data elements shall be used to define polymer substances isolated from biological matrices. Taxonomic and anatomical origins shall be described using a controlled vocabulary as required. This information is captured for naturally derived polymers ( . starch) and structurally diverse substances. For Organisms belonging to the Kingdom Plantae the Substance level defines the fresh material of a single species or infraspecies, the Herbal Drug and the Herbal preparation. For Herbal preparations, the fraction information will be captured at the Substance information level and additional information for herbal extracts will be captured at the Specified Substance Group 1 information level. See for further explanation the Substance Class: Structurally Diverse and the herbal annex.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "SupplyDelivery",
            "Title": "Delivery of bulk Supplies",
            "Description": "Record of delivery of what is supplied.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "SupplyRequest",
            "Title": "Request for a medication, substance or device",
            "Description": "A record of a non-patient specific request for a medication, substance, device, certain types of biologically derived product, and nutrition product used in the healthcare setting.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Task",
            "Title": "A task to be performed",
            "Description": "A task to be performed.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "TerminologyCapabilities",
            "Title": "A statement of system capabilities",
            "Description": "A TerminologyCapabilities resource documents a set of capabilities (behaviors) of a FHIR Terminology Server that may be used as a statement of actual server functionality or a statement of required or desired server implementation.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "TestPlan",
            "Title": "Description of intented testing",
            "Description": "A plan for executing testing on an artifact or specifications",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "TestReport",
            "Title": "Describes the results of a TestScript execution",
            "Description": "A summary of information based on the results of executing a TestScript.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "TestScript",
            "Title": "Describes a set of tests",
            "Description": "A structured set of tests against a FHIR server or client implementation to determine compliance against the FHIR specification.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "Transport",
            "Title": "Delivery of item",
            "Description": "Record of transport.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "ValueSet",
            "Title": "A set of codes drawn from one or more code systems",
            "Description": "A ValueSet resource instance specifies a set of codes drawn from one or more code systems, intended for use in a particular context. Value sets link between [[[CodeSystem]]] definitions and their use in [coded elements](terminologies.html).",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "VerificationResult",
            "Title": "Describes validation requirements, source(s), status and dates for one or more elements",
            "Description": "Describes validation requirements, source(s), status and dates for one or more elements.",
            "Comment": null
        },
        {
            "FhirVersionShortName": "R5",
            "Name": "VisionPrescription",
            "Title": "Prescription for vision correction products for a patient",
            "Description": "An authorization for the provision of glasses and/or contact lenses to a patient.",
            "Comment": null
        }
    ]
    """;
}

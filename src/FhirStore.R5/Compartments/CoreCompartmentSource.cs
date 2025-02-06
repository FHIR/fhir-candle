// <copyright file="CoreCompartmentSource.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Serialization;
using Hl7.Fhir.Model;

namespace FhirCandle.Compartments;

public class CoreCompartmentSource
{
    public static Hl7.Fhir.Model.CompartmentDefinition[] GetCompartments() => new CoreCompartmentSource().getCompartments();

    private Hl7.Fhir.Model.CompartmentDefinition[] getCompartments() =>
        _compartmentDefinitions
            .Select(json => SerializationUtils.DeserializeFhir<CompartmentDefinition>(json, "application/fhir+json"))
            .ToArray();

    private string[] _compartmentDefinitions = [
        _compartmentDefinitionDevice,
        _compartmentDefinitionEncounter,
        _compartmentDefinitionPatient,
        _compartmentDefinitionPractitioner,
        _compartmentDefinitionRelatedPerson,
        ];

    private const string _compartmentDefinitionDevice = """
        {
            "resourceType": "CompartmentDefinition",
            "id": "device",
            "meta": {
                "lastUpdated": "2023-03-26T15:21:02.749+11:00"
            },
            "url": "http://hl7.org/fhir/CompartmentDefinition/device",
            "version": "5.0.0",
            "name": "Base FHIR compartment definition for Device",
            "status": "draft",
            "experimental": true,
            "date": "2023-03-26T15:21:02+11:00",
            "publisher": "FHIR Project Team",
            "contact": [
                {
                    "telecom": [
                        {
                            "system": "url",
                            "value": "http://hl7.org/fhir"
                        }
                    ]
                }
            ],
            "description": "There is an instance of the device compartment for each Device resource, and the identity of the compartment is the same as the Device. The set of resources associated with a particular device",
            "code": "Device",
            "search": true,
            "resource": [
                {
                    "code": "Account",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "ActivityDefinition"
                },
                {
                    "code": "ActorDefinition"
                },
                {
                    "code": "AdministrableProductDefinition"
                },
                {
                    "code": "AdverseEvent"
                },
                {
                    "code": "AllergyIntolerance"
                },
                {
                    "code": "Appointment",
                    "param": [
                        "actor"
                    ]
                },
                {
                    "code": "AppointmentResponse",
                    "param": [
                        "actor"
                    ]
                },
                {
                    "code": "ArtifactAssessment"
                },
                {
                    "code": "AuditEvent",
                    "param": [
                        "agent"
                    ]
                },
                {
                    "code": "Basic"
                },
                {
                    "code": "Binary"
                },
                {
                    "code": "BiologicallyDerivedProduct"
                },
                {
                    "code": "BiologicallyDerivedProductDispense"
                },
                {
                    "code": "BodyStructure"
                },
                {
                    "code": "Bundle"
                },
                {
                    "code": "CapabilityStatement"
                },
                {
                    "code": "CarePlan"
                },
                {
                    "code": "CareTeam"
                },
                {
                    "code": "ChargeItem",
                    "param": [
                        "enterer",
                        "performer-actor"
                    ]
                },
                {
                    "code": "ChargeItemDefinition"
                },
                {
                    "code": "Citation"
                },
                {
                    "code": "Claim",
                    "param": [
                        "procedure-udi",
                        "item-udi",
                        "detail-udi",
                        "subdetail-udi"
                    ]
                },
                {
                    "code": "ClaimResponse"
                },
                {
                    "code": "ClinicalImpression"
                },
                {
                    "code": "ClinicalUseDefinition"
                },
                {
                    "code": "CodeSystem"
                },
                {
                    "code": "Communication",
                    "param": [
                        "sender",
                        "recipient"
                    ]
                },
                {
                    "code": "CommunicationRequest",
                    "param": [
                        "information-provider",
                        "recipient"
                    ]
                },
                {
                    "code": "CompartmentDefinition"
                },
                {
                    "code": "Composition",
                    "param": [
                        "author"
                    ]
                },
                {
                    "code": "ConceptMap"
                },
                {
                    "code": "Condition"
                },
                {
                    "code": "ConditionDefinition"
                },
                {
                    "code": "Consent"
                },
                {
                    "code": "Contract"
                },
                {
                    "code": "Coverage"
                },
                {
                    "code": "CoverageEligibilityRequest"
                },
                {
                    "code": "CoverageEligibilityResponse"
                },
                {
                    "code": "DetectedIssue",
                    "param": [
                        "author"
                    ]
                },
                {
                    "code": "Device"
                },
                {
                    "code": "DeviceAssociation",
                    "param": [
                        "device"
                    ]
                },
                {
                    "code": "DeviceDefinition"
                },
                {
                    "code": "DeviceDispense"
                },
                {
                    "code": "DeviceMetric"
                },
                {
                    "code": "DeviceRequest",
                    "param": [
                        "subject",
                        "requester",
                        "performer"
                    ]
                },
                {
                    "code": "DeviceUsage"
                },
                {
                    "code": "DiagnosticReport",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "DocumentReference",
                    "param": [
                        "subject",
                        "author"
                    ]
                },
                {
                    "code": "Encounter"
                },
                {
                    "code": "EncounterHistory"
                },
                {
                    "code": "Endpoint"
                },
                {
                    "code": "EnrollmentRequest"
                },
                {
                    "code": "EnrollmentResponse"
                },
                {
                    "code": "EpisodeOfCare"
                },
                {
                    "code": "EventDefinition"
                },
                {
                    "code": "Evidence"
                },
                {
                    "code": "EvidenceReport"
                },
                {
                    "code": "EvidenceVariable"
                },
                {
                    "code": "ExampleScenario"
                },
                {
                    "code": "ExplanationOfBenefit",
                    "param": [
                        "procedure-udi",
                        "item-udi",
                        "detail-udi",
                        "subdetail-udi"
                    ]
                },
                {
                    "code": "FamilyMemberHistory"
                },
                {
                    "code": "Flag",
                    "param": [
                        "author"
                    ]
                },
                {
                    "code": "FormularyItem"
                },
                {
                    "code": "GenomicStudy"
                },
                {
                    "code": "Goal"
                },
                {
                    "code": "GraphDefinition"
                },
                {
                    "code": "Group",
                    "param": [
                        "member"
                    ]
                },
                {
                    "code": "GuidanceResponse"
                },
                {
                    "code": "HealthcareService"
                },
                {
                    "code": "ImagingSelection"
                },
                {
                    "code": "ImagingStudy"
                },
                {
                    "code": "Immunization"
                },
                {
                    "code": "ImmunizationEvaluation"
                },
                {
                    "code": "ImmunizationRecommendation"
                },
                {
                    "code": "ImplementationGuide"
                },
                {
                    "code": "Ingredient"
                },
                {
                    "code": "InsurancePlan"
                },
                {
                    "code": "InventoryItem"
                },
                {
                    "code": "InventoryReport"
                },
                {
                    "code": "Invoice",
                    "param": [
                        "participant"
                    ]
                },
                {
                    "code": "Library"
                },
                {
                    "code": "Linkage"
                },
                {
                    "code": "List",
                    "param": [
                        "subject",
                        "source"
                    ]
                },
                {
                    "code": "Location"
                },
                {
                    "code": "ManufacturedItemDefinition"
                },
                {
                    "code": "Measure"
                },
                {
                    "code": "MeasureReport"
                },
                {
                    "code": "Medication"
                },
                {
                    "code": "MedicationAdministration"
                },
                {
                    "code": "MedicationDispense"
                },
                {
                    "code": "MedicationKnowledge"
                },
                {
                    "code": "MedicationRequest"
                },
                {
                    "code": "MedicationStatement"
                },
                {
                    "code": "MedicinalProductDefinition"
                },
                {
                    "code": "MessageDefinition"
                },
                {
                    "code": "MessageHeader",
                    "param": [
                        "target"
                    ]
                },
                {
                    "code": "MolecularSequence"
                },
                {
                    "code": "NamingSystem"
                },
                {
                    "code": "NutritionIntake"
                },
                {
                    "code": "NutritionOrder"
                },
                {
                    "code": "NutritionProduct"
                },
                {
                    "code": "Observation",
                    "param": [
                        "subject",
                        "device"
                    ]
                },
                {
                    "code": "ObservationDefinition"
                },
                {
                    "code": "OperationDefinition"
                },
                {
                    "code": "OperationOutcome"
                },
                {
                    "code": "Organization"
                },
                {
                    "code": "OrganizationAffiliation"
                },
                {
                    "code": "PackagedProductDefinition"
                },
                {
                    "code": "Patient"
                },
                {
                    "code": "PaymentNotice"
                },
                {
                    "code": "PaymentReconciliation"
                },
                {
                    "code": "Permission"
                },
                {
                    "code": "Person"
                },
                {
                    "code": "PlanDefinition"
                },
                {
                    "code": "Practitioner"
                },
                {
                    "code": "PractitionerRole"
                },
                {
                    "code": "Procedure"
                },
                {
                    "code": "Provenance",
                    "param": [
                        "agent"
                    ]
                },
                {
                    "code": "Questionnaire"
                },
                {
                    "code": "QuestionnaireResponse",
                    "param": [
                        "author"
                    ]
                },
                {
                    "code": "RegulatedAuthorization"
                },
                {
                    "code": "RelatedPerson"
                },
                {
                    "code": "RequestOrchestration",
                    "param": [
                        "author"
                    ]
                },
                {
                    "code": "Requirements"
                },
                {
                    "code": "ResearchStudy"
                },
                {
                    "code": "ResearchSubject",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "RiskAssessment",
                    "param": [
                        "performer"
                    ]
                },
                {
                    "code": "Schedule",
                    "param": [
                        "actor"
                    ]
                },
                {
                    "code": "SearchParameter"
                },
                {
                    "code": "ServiceRequest",
                    "param": [
                        "performer",
                        "requester"
                    ]
                },
                {
                    "code": "Slot"
                },
                {
                    "code": "Specimen",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "SpecimenDefinition"
                },
                {
                    "code": "StructureDefinition"
                },
                {
                    "code": "StructureMap"
                },
                {
                    "code": "Subscription"
                },
                {
                    "code": "SubscriptionStatus"
                },
                {
                    "code": "SubscriptionTopic"
                },
                {
                    "code": "Substance"
                },
                {
                    "code": "SubstanceDefinition"
                },
                {
                    "code": "SubstanceNucleicAcid"
                },
                {
                    "code": "SubstancePolymer"
                },
                {
                    "code": "SubstanceProtein"
                },
                {
                    "code": "SubstanceReferenceInformation"
                },
                {
                    "code": "SubstanceSourceMaterial"
                },
                {
                    "code": "SupplyDelivery"
                },
                {
                    "code": "SupplyRequest",
                    "param": [
                        "requester"
                    ]
                },
                {
                    "code": "Task"
                },
                {
                    "code": "TerminologyCapabilities"
                },
                {
                    "code": "TestPlan"
                },
                {
                    "code": "TestReport"
                },
                {
                    "code": "TestScript"
                },
                {
                    "code": "Transport"
                },
                {
                    "code": "ValueSet"
                },
                {
                    "code": "VerificationResult"
                },
                {
                    "code": "VisionPrescription"
                }
            ]
        }
        """;

    private const string _compartmentDefinitionEncounter = """
        {
            "resourceType": "CompartmentDefinition",
            "id": "encounter",
            "meta": {
                "lastUpdated": "2023-03-26T15:21:02.749+11:00"
            },
            "url": "http://hl7.org/fhir/CompartmentDefinition/encounter",
            "version": "5.0.0",
            "name": "Base FHIR compartment definition for Encounter",
            "status": "draft",
            "experimental": true,
            "date": "2023-03-26T15:21:02+11:00",
            "publisher": "FHIR Project Team",
            "contact": [
                {
                    "telecom": [
                        {
                            "system": "url",
                            "value": "http://hl7.org/fhir"
                        }
                    ]
                }
            ],
            "description": "There is an instance of the encounter compartment for each encounter resource, and the identity of the compartment is the same as the encounter. The set of resources associated with a particular encounter",
            "code": "Encounter",
            "search": true,
            "resource": [
                {
                    "code": "Account"
                },
                {
                    "code": "ActivityDefinition"
                },
                {
                    "code": "ActorDefinition"
                },
                {
                    "code": "AdministrableProductDefinition"
                },
                {
                    "code": "AdverseEvent"
                },
                {
                    "code": "AllergyIntolerance"
                },
                {
                    "code": "Appointment"
                },
                {
                    "code": "AppointmentResponse"
                },
                {
                    "code": "ArtifactAssessment"
                },
                {
                    "code": "AuditEvent"
                },
                {
                    "code": "Basic"
                },
                {
                    "code": "Binary"
                },
                {
                    "code": "BiologicallyDerivedProduct"
                },
                {
                    "code": "BiologicallyDerivedProductDispense"
                },
                {
                    "code": "BodyStructure"
                },
                {
                    "code": "Bundle"
                },
                {
                    "code": "CapabilityStatement"
                },
                {
                    "code": "CarePlan",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "CareTeam"
                },
                {
                    "code": "ChargeItem",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "ChargeItemDefinition"
                },
                {
                    "code": "Citation"
                },
                {
                    "code": "Claim",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "ClaimResponse"
                },
                {
                    "code": "ClinicalImpression",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "ClinicalUseDefinition"
                },
                {
                    "code": "CodeSystem"
                },
                {
                    "code": "Communication",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "CommunicationRequest",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "CompartmentDefinition"
                },
                {
                    "code": "Composition",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "ConceptMap"
                },
                {
                    "code": "Condition",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "ConditionDefinition"
                },
                {
                    "code": "Consent"
                },
                {
                    "code": "Contract"
                },
                {
                    "code": "Coverage"
                },
                {
                    "code": "CoverageEligibilityRequest"
                },
                {
                    "code": "CoverageEligibilityResponse"
                },
                {
                    "code": "DetectedIssue"
                },
                {
                    "code": "Device"
                },
                {
                    "code": "DeviceAssociation"
                },
                {
                    "code": "DeviceDefinition"
                },
                {
                    "code": "DeviceDispense"
                },
                {
                    "code": "DeviceMetric"
                },
                {
                    "code": "DeviceRequest",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "DeviceUsage"
                },
                {
                    "code": "DiagnosticReport",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "DocumentReference",
                    "param": [
                        "context"
                    ]
                },
                {
                    "code": "Encounter",
                    "param": [
                        "{def}"
                    ]
                },
                {
                    "code": "EncounterHistory",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "Endpoint"
                },
                {
                    "code": "EnrollmentRequest"
                },
                {
                    "code": "EnrollmentResponse"
                },
                {
                    "code": "EpisodeOfCare"
                },
                {
                    "code": "EventDefinition"
                },
                {
                    "code": "Evidence"
                },
                {
                    "code": "EvidenceReport"
                },
                {
                    "code": "EvidenceVariable"
                },
                {
                    "code": "ExampleScenario"
                },
                {
                    "code": "ExplanationOfBenefit",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "FamilyMemberHistory"
                },
                {
                    "code": "Flag"
                },
                {
                    "code": "FormularyItem"
                },
                {
                    "code": "GenomicStudy"
                },
                {
                    "code": "Goal"
                },
                {
                    "code": "GraphDefinition"
                },
                {
                    "code": "Group"
                },
                {
                    "code": "GuidanceResponse"
                },
                {
                    "code": "HealthcareService"
                },
                {
                    "code": "ImagingSelection"
                },
                {
                    "code": "ImagingStudy"
                },
                {
                    "code": "Immunization"
                },
                {
                    "code": "ImmunizationEvaluation"
                },
                {
                    "code": "ImmunizationRecommendation"
                },
                {
                    "code": "ImplementationGuide"
                },
                {
                    "code": "Ingredient"
                },
                {
                    "code": "InsurancePlan"
                },
                {
                    "code": "InventoryItem"
                },
                {
                    "code": "InventoryReport"
                },
                {
                    "code": "Invoice"
                },
                {
                    "code": "Library"
                },
                {
                    "code": "Linkage"
                },
                {
                    "code": "List"
                },
                {
                    "code": "Location"
                },
                {
                    "code": "ManufacturedItemDefinition"
                },
                {
                    "code": "Measure"
                },
                {
                    "code": "MeasureReport"
                },
                {
                    "code": "Medication"
                },
                {
                    "code": "MedicationAdministration",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "MedicationDispense",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "MedicationKnowledge"
                },
                {
                    "code": "MedicationRequest",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "MedicationStatement",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "MedicinalProductDefinition"
                },
                {
                    "code": "MessageDefinition"
                },
                {
                    "code": "MessageHeader"
                },
                {
                    "code": "MolecularSequence"
                },
                {
                    "code": "NamingSystem"
                },
                {
                    "code": "NutritionIntake",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "NutritionOrder",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "NutritionProduct"
                },
                {
                    "code": "Observation",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "ObservationDefinition"
                },
                {
                    "code": "OperationDefinition"
                },
                {
                    "code": "OperationOutcome"
                },
                {
                    "code": "Organization"
                },
                {
                    "code": "OrganizationAffiliation"
                },
                {
                    "code": "PackagedProductDefinition"
                },
                {
                    "code": "Patient"
                },
                {
                    "code": "PaymentNotice"
                },
                {
                    "code": "PaymentReconciliation"
                },
                {
                    "code": "Permission"
                },
                {
                    "code": "Person"
                },
                {
                    "code": "PlanDefinition"
                },
                {
                    "code": "Practitioner"
                },
                {
                    "code": "PractitionerRole"
                },
                {
                    "code": "Procedure",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "Provenance"
                },
                {
                    "code": "Questionnaire"
                },
                {
                    "code": "QuestionnaireResponse",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "RegulatedAuthorization"
                },
                {
                    "code": "RelatedPerson"
                },
                {
                    "code": "RequestOrchestration",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "Requirements"
                },
                {
                    "code": "ResearchStudy"
                },
                {
                    "code": "ResearchSubject"
                },
                {
                    "code": "RiskAssessment"
                },
                {
                    "code": "Schedule"
                },
                {
                    "code": "SearchParameter"
                },
                {
                    "code": "ServiceRequest",
                    "param": [
                        "encounter"
                    ]
                },
                {
                    "code": "Slot"
                },
                {
                    "code": "Specimen"
                },
                {
                    "code": "SpecimenDefinition"
                },
                {
                    "code": "StructureDefinition"
                },
                {
                    "code": "StructureMap"
                },
                {
                    "code": "Subscription"
                },
                {
                    "code": "SubscriptionStatus"
                },
                {
                    "code": "SubscriptionTopic"
                },
                {
                    "code": "Substance"
                },
                {
                    "code": "SubstanceDefinition"
                },
                {
                    "code": "SubstanceNucleicAcid"
                },
                {
                    "code": "SubstancePolymer"
                },
                {
                    "code": "SubstanceProtein"
                },
                {
                    "code": "SubstanceReferenceInformation"
                },
                {
                    "code": "SubstanceSourceMaterial"
                },
                {
                    "code": "SupplyDelivery"
                },
                {
                    "code": "SupplyRequest"
                },
                {
                    "code": "Task"
                },
                {
                    "code": "TerminologyCapabilities"
                },
                {
                    "code": "TestPlan"
                },
                {
                    "code": "TestReport"
                },
                {
                    "code": "TestScript"
                },
                {
                    "code": "Transport"
                },
                {
                    "code": "ValueSet"
                },
                {
                    "code": "VerificationResult"
                },
                {
                    "code": "VisionPrescription",
                    "param": [
                        "encounter"
                    ]
                }
            ]
        }
        """;

    private const string _compartmentDefinitionPatient = """
        {
            "resourceType": "CompartmentDefinition",
            "id": "patient",
            "meta": {
                "lastUpdated": "2023-03-26T15:21:02.749+11:00"
            },
            "url": "http://hl7.org/fhir/CompartmentDefinition/patient",
            "version": "5.0.0",
            "name": "Base FHIR compartment definition for Patient",
            "status": "draft",
            "experimental": true,
            "date": "2023-03-26T15:21:02+11:00",
            "publisher": "FHIR Project Team",
            "contact": [
                {
                    "telecom": [
                        {
                            "system": "url",
                            "value": "http://hl7.org/fhir"
                        }
                    ]
                }
            ],
            "description": "There is an instance of the patient compartment for each patient resource, and the identity of the compartment is the same as the patient. When a patient is linked to another patient resource, the records associated with the linked patient resource will not be returned as part of the compartment search. Those records will be returned only with another compartment search using the \"id\" for the linked patient resource.\n \nIn cases where two patients have been merged rather than linked, associated resources should be moved to the target patient as part of the merge process, so the patient compartment for the target patient would include all relevant data, and the patient compartment for the source patient would include only the linked Patient and possibly remnant resources like AuditEvent.. The set of resources associated with a particular patient",
            "code": "Patient",
            "search": true,
            "resource": [
                {
                    "code": "Account",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "ActivityDefinition"
                },
                {
                    "code": "ActorDefinition"
                },
                {
                    "code": "AdministrableProductDefinition"
                },
                {
                    "code": "AdverseEvent",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "AllergyIntolerance",
                    "param": [
                        "patient",
                        "participant"
                    ]
                },
                {
                    "code": "Appointment",
                    "param": [
                        "actor"
                    ]
                },
                {
                    "code": "AppointmentResponse",
                    "param": [
                        "actor"
                    ]
                },
                {
                    "code": "ArtifactAssessment"
                },
                {
                    "code": "AuditEvent",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "Basic",
                    "param": [
                        "patient",
                        "author"
                    ]
                },
                {
                    "code": "Binary"
                },
                {
                    "code": "BiologicallyDerivedProduct"
                },
                {
                    "code": "BiologicallyDerivedProductDispense",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "BodyStructure",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "Bundle"
                },
                {
                    "code": "CapabilityStatement"
                },
                {
                    "code": "CarePlan",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "CareTeam",
                    "param": [
                        "patient",
                        "participant"
                    ]
                },
                {
                    "code": "ChargeItem",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "ChargeItemDefinition"
                },
                {
                    "code": "Citation"
                },
                {
                    "code": "Claim",
                    "param": [
                        "patient",
                        "payee"
                    ]
                },
                {
                    "code": "ClaimResponse",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "ClinicalImpression",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "ClinicalUseDefinition"
                },
                {
                    "code": "CodeSystem"
                },
                {
                    "code": "Communication",
                    "param": [
                        "subject",
                        "sender",
                        "recipient"
                    ]
                },
                {
                    "code": "CommunicationRequest",
                    "param": [
                        "subject",
                        "information-provider",
                        "recipient",
                        "requester"
                    ]
                },
                {
                    "code": "CompartmentDefinition"
                },
                {
                    "code": "Composition",
                    "param": [
                        "subject",
                        "author",
                        "attester"
                    ]
                },
                {
                    "code": "ConceptMap"
                },
                {
                    "code": "Condition",
                    "param": [
                        "patient",
                        "participant-actor"
                    ]
                },
                {
                    "code": "ConditionDefinition"
                },
                {
                    "code": "Consent",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "Contract",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "Coverage",
                    "param": [
                        "policy-holder",
                        "subscriber",
                        "beneficiary",
                        "paymentby-party"
                    ]
                },
                {
                    "code": "CoverageEligibilityRequest",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "CoverageEligibilityResponse",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "DetectedIssue",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "Device"
                },
                {
                    "code": "DeviceAssociation",
                    "param": [
                        "subject",
                        "operator"
                    ]
                },
                {
                    "code": "DeviceDefinition"
                },
                {
                    "code": "DeviceDispense"
                },
                {
                    "code": "DeviceMetric"
                },
                {
                    "code": "DeviceRequest",
                    "param": [
                        "subject",
                        "performer"
                    ]
                },
                {
                    "code": "DeviceUsage",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "DiagnosticReport",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "DocumentReference",
                    "param": [
                        "subject",
                        "author"
                    ]
                },
                {
                    "code": "Encounter",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "EncounterHistory",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "Endpoint"
                },
                {
                    "code": "EnrollmentRequest",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "EnrollmentResponse"
                },
                {
                    "code": "EpisodeOfCare",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "EventDefinition"
                },
                {
                    "code": "Evidence"
                },
                {
                    "code": "EvidenceReport"
                },
                {
                    "code": "EvidenceVariable"
                },
                {
                    "code": "ExampleScenario"
                },
                {
                    "code": "ExplanationOfBenefit",
                    "param": [
                        "patient",
                        "payee"
                    ]
                },
                {
                    "code": "FamilyMemberHistory",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "Flag",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "FormularyItem"
                },
                {
                    "code": "GenomicStudy",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "Goal",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "GraphDefinition"
                },
                {
                    "code": "Group",
                    "param": [
                        "member"
                    ]
                },
                {
                    "code": "GuidanceResponse",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "HealthcareService"
                },
                {
                    "code": "ImagingSelection",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "ImagingStudy",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "Immunization",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "ImmunizationEvaluation",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "ImmunizationRecommendation",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "ImplementationGuide"
                },
                {
                    "code": "Ingredient"
                },
                {
                    "code": "InsurancePlan"
                },
                {
                    "code": "InventoryItem"
                },
                {
                    "code": "InventoryReport"
                },
                {
                    "code": "Invoice",
                    "param": [
                        "subject",
                        "patient",
                        "recipient"
                    ]
                },
                {
                    "code": "Library"
                },
                {
                    "code": "Linkage"
                },
                {
                    "code": "List",
                    "param": [
                        "subject",
                        "source"
                    ]
                },
                {
                    "code": "Location"
                },
                {
                    "code": "ManufacturedItemDefinition"
                },
                {
                    "code": "Measure"
                },
                {
                    "code": "MeasureReport",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "Medication"
                },
                {
                    "code": "MedicationAdministration",
                    "param": [
                        "patient",
                        "subject"
                    ]
                },
                {
                    "code": "MedicationDispense",
                    "param": [
                        "subject",
                        "patient",
                        "receiver"
                    ]
                },
                {
                    "code": "MedicationKnowledge"
                },
                {
                    "code": "MedicationRequest",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "MedicationStatement",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "MedicinalProductDefinition"
                },
                {
                    "code": "MessageDefinition"
                },
                {
                    "code": "MessageHeader"
                },
                {
                    "code": "MolecularSequence",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "NamingSystem"
                },
                {
                    "code": "NutritionIntake",
                    "param": [
                        "subject",
                        "source"
                    ]
                },
                {
                    "code": "NutritionOrder",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "NutritionProduct"
                },
                {
                    "code": "Observation",
                    "param": [
                        "subject",
                        "performer"
                    ]
                },
                {
                    "code": "ObservationDefinition"
                },
                {
                    "code": "OperationDefinition"
                },
                {
                    "code": "OperationOutcome"
                },
                {
                    "code": "Organization"
                },
                {
                    "code": "OrganizationAffiliation"
                },
                {
                    "code": "PackagedProductDefinition"
                },
                {
                    "code": "Patient",
                    "param": [
                        "{def}",
                        "link"
                    ]
                },
                {
                    "code": "PaymentNotice"
                },
                {
                    "code": "PaymentReconciliation"
                },
                {
                    "code": "Permission"
                },
                {
                    "code": "Person",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "PlanDefinition"
                },
                {
                    "code": "Practitioner"
                },
                {
                    "code": "PractitionerRole"
                },
                {
                    "code": "Procedure",
                    "param": [
                        "patient",
                        "performer"
                    ]
                },
                {
                    "code": "Provenance",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "Questionnaire"
                },
                {
                    "code": "QuestionnaireResponse",
                    "param": [
                        "subject",
                        "author"
                    ]
                },
                {
                    "code": "RegulatedAuthorization"
                },
                {
                    "code": "RelatedPerson",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "RequestOrchestration",
                    "param": [
                        "subject",
                        "participant"
                    ]
                },
                {
                    "code": "Requirements"
                },
                {
                    "code": "ResearchStudy"
                },
                {
                    "code": "ResearchSubject",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "RiskAssessment",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "Schedule",
                    "param": [
                        "actor"
                    ]
                },
                {
                    "code": "SearchParameter"
                },
                {
                    "code": "ServiceRequest",
                    "param": [
                        "subject",
                        "performer"
                    ]
                },
                {
                    "code": "Slot"
                },
                {
                    "code": "Specimen",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "SpecimenDefinition"
                },
                {
                    "code": "StructureDefinition"
                },
                {
                    "code": "StructureMap"
                },
                {
                    "code": "Subscription"
                },
                {
                    "code": "SubscriptionStatus"
                },
                {
                    "code": "SubscriptionTopic"
                },
                {
                    "code": "Substance"
                },
                {
                    "code": "SubstanceDefinition"
                },
                {
                    "code": "SubstanceNucleicAcid"
                },
                {
                    "code": "SubstancePolymer"
                },
                {
                    "code": "SubstanceProtein"
                },
                {
                    "code": "SubstanceReferenceInformation"
                },
                {
                    "code": "SubstanceSourceMaterial"
                },
                {
                    "code": "SupplyDelivery",
                    "param": [
                        "patient"
                    ]
                },
                {
                    "code": "SupplyRequest",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "Task",
                    "param": [
                        "patient",
                        "focus"
                    ]
                },
                {
                    "code": "TerminologyCapabilities"
                },
                {
                    "code": "TestPlan"
                },
                {
                    "code": "TestReport"
                },
                {
                    "code": "TestScript"
                },
                {
                    "code": "Transport"
                },
                {
                    "code": "ValueSet"
                },
                {
                    "code": "VerificationResult"
                },
                {
                    "code": "VisionPrescription",
                    "param": [
                        "patient"
                    ]
                }
            ]
        }
        """;

    private const string _compartmentDefinitionPractitioner = """
        {
            "resourceType": "CompartmentDefinition",
            "id": "practitioner",
            "meta": {
                "lastUpdated": "2023-03-26T15:21:02.749+11:00"
            },
            "url": "http://hl7.org/fhir/CompartmentDefinition/practitioner",
            "version": "5.0.0",
            "name": "Base FHIR compartment definition for Practitioner",
            "status": "draft",
            "experimental": true,
            "date": "2023-03-26T15:21:02+11:00",
            "publisher": "FHIR Project Team",
            "contact": [
                {
                    "telecom": [
                        {
                            "system": "url",
                            "value": "http://hl7.org/fhir"
                        }
                    ]
                }
            ],
            "description": "There is an instance of the practitioner compartment for each Practitioner resource, and the identity of the compartment is the same as the Practitioner. The set of resources associated with a particular practitioner",
            "code": "Practitioner",
            "search": true,
            "resource": [
                {
                    "code": "Account",
                    "param": [
                        "subject"
                    ]
                },
                {
                    "code": "ActivityDefinition"
                },
                {
                    "code": "ActorDefinition"
                },
                {
                    "code": "AdministrableProductDefinition"
                },
                {
                    "code": "AdverseEvent",
                    "param": [
                        "recorder"
                    ]
                },
                {
                    "code": "AllergyIntolerance",
                    "param": [
                        "participant"
                    ]
                },
                {
                    "code": "Appointment",
                    "param": [
                        "actor"
                    ]
                },
                {
                    "code": "AppointmentResponse",
                    "param": [
                        "actor"
                    ]
                },
                {
                    "code": "ArtifactAssessment"
                },
                {
                    "code": "AuditEvent",
                    "param": [
                        "agent"
                    ]
                },
                {
                    "code": "Basic",
                    "param": [
                        "author"
                    ]
                },
                {
                    "code": "Binary"
                },
                {
                    "code": "BiologicallyDerivedProduct"
                },
                {
                    "code": "BiologicallyDerivedProductDispense",
                    "param": [
                        "performer"
                    ]
                },
                {
                    "code": "BodyStructure"
                },
                {
                    "code": "Bundle"
                },
                {
                    "code": "CapabilityStatement"
                },
                {
                    "code": "CarePlan"
                },
                {
                    "code": "CareTeam",
                    "param": [
                        "participant"
                    ]
                },
                {
                    "code": "ChargeItem",
                    "param": [
                        "enterer",
                        "performer-actor"
                    ]
                },
                {
                    "code": "ChargeItemDefinition"
                },
                {
                    "code": "Citation"
                },
                {
                    "code": "Claim",
                    "param": [
                        "enterer",
                        "provider",
                        "payee",
                        "care-team"
                    ]
                },
                {
                    "code": "ClaimResponse",
                    "param": [
                        "requestor"
                    ]
                },
                {
                    "code": "ClinicalImpression",
                    "param": [
                        "performer"
                    ]
                },
                {
                    "code": "ClinicalUseDefinition"
                },
                {
                    "code": "CodeSystem"
                },
                {
                    "code": "Communication",
                    "param": [
                        "sender",
                        "recipient"
                    ]
                },
                {
                    "code": "CommunicationRequest",
                    "param": [
                        "information-provider",
                        "recipient",
                        "requester"
                    ]
                },
                {
                    "code": "CompartmentDefinition"
                },
                {
                    "code": "Composition",
                    "param": [
                        "subject",
                        "author",
                        "attester"
                    ]
                },
                {
                    "code": "ConceptMap"
                },
                {
                    "code": "Condition",
                    "param": [
                        "participant-actor"
                    ]
                },
                {
                    "code": "ConditionDefinition"
                },
                {
                    "code": "Consent"
                },
                {
                    "code": "Contract"
                },
                {
                    "code": "Coverage"
                },
                {
                    "code": "CoverageEligibilityRequest",
                    "param": [
                        "enterer",
                        "provider"
                    ]
                },
                {
                    "code": "CoverageEligibilityResponse",
                    "param": [
                        "requestor"
                    ]
                },
                {
                    "code": "DetectedIssue",
                    "param": [
                        "author"
                    ]
                },
                {
                    "code": "Device"
                },
                {
                    "code": "DeviceAssociation",
                    "param": [
                        "operator"
                    ]
                },
                {
                    "code": "DeviceDefinition"
                },
                {
                    "code": "DeviceDispense"
                },
                {
                    "code": "DeviceMetric"
                },
                {
                    "code": "DeviceRequest",
                    "param": [
                        "requester",
                        "performer"
                    ]
                },
                {
                    "code": "DeviceUsage"
                },
                {
                    "code": "DiagnosticReport",
                    "param": [
                        "performer"
                    ]
                },
                {
                    "code": "DocumentReference",
                    "param": [
                        "subject",
                        "author",
                        "attester"
                    ]
                },
                {
                    "code": "Encounter",
                    "param": [
                        "practitioner",
                        "participant"
                    ]
                },
                {
                    "code": "EncounterHistory"
                },
                {
                    "code": "Endpoint"
                },
                {
                    "code": "EnrollmentRequest"
                },
                {
                    "code": "EnrollmentResponse"
                },
                {
                    "code": "EpisodeOfCare",
                    "param": [
                        "care-manager"
                    ]
                },
                {
                    "code": "EventDefinition"
                },
                {
                    "code": "Evidence"
                },
                {
                    "code": "EvidenceReport"
                },
                {
                    "code": "EvidenceVariable"
                },
                {
                    "code": "ExampleScenario"
                },
                {
                    "code": "ExplanationOfBenefit",
                    "param": [
                        "enterer",
                        "provider",
                        "payee",
                        "care-team"
                    ]
                },
                {
                    "code": "FamilyMemberHistory"
                },
                {
                    "code": "Flag",
                    "param": [
                        "author"
                    ]
                },
                {
                    "code": "FormularyItem"
                },
                {
                    "code": "GenomicStudy"
                },
                {
                    "code": "Goal"
                },
                {
                    "code": "GraphDefinition"
                },
                {
                    "code": "Group",
                    "param": [
                        "member"
                    ]
                },
                {
                    "code": "GuidanceResponse"
                },
                {
                    "code": "HealthcareService"
                },
                {
                    "code": "ImagingSelection"
                },
                {
                    "code": "ImagingStudy"
                },
                {
                    "code": "Immunization",
                    "param": [
                        "performer"
                    ]
                },
                {
                    "code": "ImmunizationEvaluation"
                },
                {
                    "code": "ImmunizationRecommendation"
                },
                {
                    "code": "ImplementationGuide"
                },
                {
                    "code": "Ingredient"
                },
                {
                    "code": "InsurancePlan"
                },
                {
                    "code": "InventoryItem"
                },
                {
                    "code": "InventoryReport"
                },
                {
                    "code": "Invoice",
                    "param": [
                        "participant"
                    ]
                },
                {
                    "code": "Library"
                },
                {
                    "code": "Linkage",
                    "param": [
                        "author"
                    ]
                },
                {
                    "code": "List",
                    "param": [
                        "source"
                    ]
                },
                {
                    "code": "Location"
                },
                {
                    "code": "ManufacturedItemDefinition"
                },
                {
                    "code": "Measure"
                },
                {
                    "code": "MeasureReport"
                },
                {
                    "code": "Medication"
                },
                {
                    "code": "MedicationAdministration"
                },
                {
                    "code": "MedicationDispense",
                    "param": [
                        "performer",
                        "receiver"
                    ]
                },
                {
                    "code": "MedicationKnowledge"
                },
                {
                    "code": "MedicationRequest",
                    "param": [
                        "requester"
                    ]
                },
                {
                    "code": "MedicationStatement",
                    "param": [
                        "source"
                    ]
                },
                {
                    "code": "MedicinalProductDefinition"
                },
                {
                    "code": "MessageDefinition"
                },
                {
                    "code": "MessageHeader",
                    "param": [
                        "receiver",
                        "author",
                        "responsible"
                    ]
                },
                {
                    "code": "MolecularSequence"
                },
                {
                    "code": "NamingSystem"
                },
                {
                    "code": "NutritionIntake",
                    "param": [
                        "source"
                    ]
                },
                {
                    "code": "NutritionOrder",
                    "param": [
                        "provider"
                    ]
                },
                {
                    "code": "NutritionProduct"
                },
                {
                    "code": "Observation",
                    "param": [
                        "performer"
                    ]
                },
                {
                    "code": "ObservationDefinition"
                },
                {
                    "code": "OperationDefinition"
                },
                {
                    "code": "OperationOutcome"
                },
                {
                    "code": "Organization"
                },
                {
                    "code": "OrganizationAffiliation"
                },
                {
                    "code": "PackagedProductDefinition"
                },
                {
                    "code": "Patient",
                    "param": [
                        "general-practitioner"
                    ]
                },
                {
                    "code": "PaymentNotice",
                    "param": [
                        "reporter"
                    ]
                },
                {
                    "code": "PaymentReconciliation",
                    "param": [
                        "requestor"
                    ]
                },
                {
                    "code": "Permission"
                },
                {
                    "code": "Person",
                    "param": [
                        "practitioner"
                    ]
                },
                {
                    "code": "PlanDefinition"
                },
                {
                    "code": "Practitioner",
                    "param": [
                        "{def}"
                    ]
                },
                {
                    "code": "PractitionerRole",
                    "param": [
                        "practitioner"
                    ]
                },
                {
                    "code": "Procedure",
                    "param": [
                        "performer"
                    ]
                },
                {
                    "code": "Provenance",
                    "param": [
                        "agent"
                    ]
                },
                {
                    "code": "Questionnaire"
                },
                {
                    "code": "QuestionnaireResponse",
                    "param": [
                        "author",
                        "source"
                    ]
                },
                {
                    "code": "RegulatedAuthorization"
                },
                {
                    "code": "RelatedPerson"
                },
                {
                    "code": "RequestOrchestration",
                    "param": [
                        "participant",
                        "author"
                    ]
                },
                {
                    "code": "Requirements"
                },
                {
                    "code": "ResearchStudy"
                },
                {
                    "code": "ResearchSubject"
                },
                {
                    "code": "RiskAssessment",
                    "param": [
                        "performer"
                    ]
                },
                {
                    "code": "Schedule",
                    "param": [
                        "actor"
                    ]
                },
                {
                    "code": "SearchParameter"
                },
                {
                    "code": "ServiceRequest",
                    "param": [
                        "performer",
                        "requester"
                    ]
                },
                {
                    "code": "Slot"
                },
                {
                    "code": "Specimen",
                    "param": [
                        "collector"
                    ]
                },
                {
                    "code": "SpecimenDefinition"
                },
                {
                    "code": "StructureDefinition"
                },
                {
                    "code": "StructureMap"
                },
                {
                    "code": "Subscription"
                },
                {
                    "code": "SubscriptionStatus"
                },
                {
                    "code": "SubscriptionTopic"
                },
                {
                    "code": "Substance"
                },
                {
                    "code": "SubstanceDefinition"
                },
                {
                    "code": "SubstanceNucleicAcid"
                },
                {
                    "code": "SubstancePolymer"
                },
                {
                    "code": "SubstanceProtein"
                },
                {
                    "code": "SubstanceReferenceInformation"
                },
                {
                    "code": "SubstanceSourceMaterial"
                },
                {
                    "code": "SupplyDelivery",
                    "param": [
                        "supplier",
                        "receiver"
                    ]
                },
                {
                    "code": "SupplyRequest",
                    "param": [
                        "requester"
                    ]
                },
                {
                    "code": "Task"
                },
                {
                    "code": "TerminologyCapabilities"
                },
                {
                    "code": "TestPlan"
                },
                {
                    "code": "TestReport"
                },
                {
                    "code": "TestScript"
                },
                {
                    "code": "Transport"
                },
                {
                    "code": "ValueSet"
                },
                {
                    "code": "VerificationResult"
                },
                {
                    "code": "VisionPrescription",
                    "param": [
                        "prescriber"
                    ]
                }
            ]
        }
        """;

    private const string _compartmentDefinitionRelatedPerson = """
        {
            "resourceType": "CompartmentDefinition",
            "id": "relatedPerson",
            "meta": {
                "lastUpdated": "2023-03-26T15:21:02.749+11:00"
            },
            "url": "http://hl7.org/fhir/CompartmentDefinition/relatedPerson",
            "version": "5.0.0",
            "name": "Base FHIR compartment definition for RelatedPerson",
            "status": "draft",
            "experimental": true,
            "date": "2023-03-26T15:21:02+11:00",
            "publisher": "FHIR Project Team",
            "contact": [
                {
                    "telecom": [
                        {
                            "system": "url",
                            "value": "http://hl7.org/fhir"
                        }
                    ]
                }
            ],
            "description": "There is an instance of the relatedPerson compartment for each relatedPerson resource, and the identity of the compartment is the same as the relatedPerson. The set of resources associated with a particular 'related person'",
            "code": "RelatedPerson",
            "search": true,
            "resource": [
                {
                    "code": "Account"
                },
                {
                    "code": "ActivityDefinition"
                },
                {
                    "code": "ActorDefinition"
                },
                {
                    "code": "AdministrableProductDefinition"
                },
                {
                    "code": "AdverseEvent",
                    "param": [
                        "recorder"
                    ]
                },
                {
                    "code": "AllergyIntolerance",
                    "param": [
                        "participant"
                    ]
                },
                {
                    "code": "Appointment",
                    "param": [
                        "actor"
                    ]
                },
                {
                    "code": "AppointmentResponse",
                    "param": [
                        "actor"
                    ]
                },
                {
                    "code": "ArtifactAssessment"
                },
                {
                    "code": "AuditEvent"
                },
                {
                    "code": "Basic",
                    "param": [
                        "author"
                    ]
                },
                {
                    "code": "Binary"
                },
                {
                    "code": "BiologicallyDerivedProduct"
                },
                {
                    "code": "BiologicallyDerivedProductDispense"
                },
                {
                    "code": "BodyStructure"
                },
                {
                    "code": "Bundle"
                },
                {
                    "code": "CapabilityStatement"
                },
                {
                    "code": "CarePlan"
                },
                {
                    "code": "CareTeam",
                    "param": [
                        "participant"
                    ]
                },
                {
                    "code": "ChargeItem",
                    "param": [
                        "enterer",
                        "performer-actor"
                    ]
                },
                {
                    "code": "ChargeItemDefinition"
                },
                {
                    "code": "Citation"
                },
                {
                    "code": "Claim",
                    "param": [
                        "payee"
                    ]
                },
                {
                    "code": "ClaimResponse"
                },
                {
                    "code": "ClinicalImpression"
                },
                {
                    "code": "ClinicalUseDefinition"
                },
                {
                    "code": "CodeSystem"
                },
                {
                    "code": "Communication",
                    "param": [
                        "sender",
                        "recipient"
                    ]
                },
                {
                    "code": "CommunicationRequest",
                    "param": [
                        "information-provider",
                        "recipient",
                        "requester"
                    ]
                },
                {
                    "code": "CompartmentDefinition"
                },
                {
                    "code": "Composition",
                    "param": [
                        "author"
                    ]
                },
                {
                    "code": "ConceptMap"
                },
                {
                    "code": "Condition",
                    "param": [
                        "participant-actor"
                    ]
                },
                {
                    "code": "ConditionDefinition"
                },
                {
                    "code": "Consent"
                },
                {
                    "code": "Contract"
                },
                {
                    "code": "Coverage",
                    "param": [
                        "policy-holder",
                        "subscriber",
                        "paymentby-party"
                    ]
                },
                {
                    "code": "CoverageEligibilityRequest"
                },
                {
                    "code": "CoverageEligibilityResponse"
                },
                {
                    "code": "DetectedIssue"
                },
                {
                    "code": "Device"
                },
                {
                    "code": "DeviceAssociation"
                },
                {
                    "code": "DeviceDefinition"
                },
                {
                    "code": "DeviceDispense"
                },
                {
                    "code": "DeviceMetric"
                },
                {
                    "code": "DeviceRequest"
                },
                {
                    "code": "DeviceUsage"
                },
                {
                    "code": "DiagnosticReport"
                },
                {
                    "code": "DocumentReference",
                    "param": [
                        "author"
                    ]
                },
                {
                    "code": "Encounter",
                    "param": [
                        "participant"
                    ]
                },
                {
                    "code": "EncounterHistory"
                },
                {
                    "code": "Endpoint"
                },
                {
                    "code": "EnrollmentRequest"
                },
                {
                    "code": "EnrollmentResponse"
                },
                {
                    "code": "EpisodeOfCare"
                },
                {
                    "code": "EventDefinition"
                },
                {
                    "code": "Evidence"
                },
                {
                    "code": "EvidenceReport"
                },
                {
                    "code": "EvidenceVariable"
                },
                {
                    "code": "ExampleScenario"
                },
                {
                    "code": "ExplanationOfBenefit",
                    "param": [
                        "payee"
                    ]
                },
                {
                    "code": "FamilyMemberHistory"
                },
                {
                    "code": "Flag"
                },
                {
                    "code": "FormularyItem"
                },
                {
                    "code": "GenomicStudy"
                },
                {
                    "code": "Goal"
                },
                {
                    "code": "GraphDefinition"
                },
                {
                    "code": "Group"
                },
                {
                    "code": "GuidanceResponse"
                },
                {
                    "code": "HealthcareService"
                },
                {
                    "code": "ImagingSelection"
                },
                {
                    "code": "ImagingStudy"
                },
                {
                    "code": "Immunization"
                },
                {
                    "code": "ImmunizationEvaluation"
                },
                {
                    "code": "ImmunizationRecommendation"
                },
                {
                    "code": "ImplementationGuide"
                },
                {
                    "code": "Ingredient"
                },
                {
                    "code": "InsurancePlan"
                },
                {
                    "code": "InventoryItem"
                },
                {
                    "code": "InventoryReport"
                },
                {
                    "code": "Invoice",
                    "param": [
                        "recipient"
                    ]
                },
                {
                    "code": "Library"
                },
                {
                    "code": "Linkage"
                },
                {
                    "code": "List"
                },
                {
                    "code": "Location"
                },
                {
                    "code": "ManufacturedItemDefinition"
                },
                {
                    "code": "Measure"
                },
                {
                    "code": "MeasureReport"
                },
                {
                    "code": "Medication"
                },
                {
                    "code": "MedicationAdministration"
                },
                {
                    "code": "MedicationDispense"
                },
                {
                    "code": "MedicationKnowledge"
                },
                {
                    "code": "MedicationRequest"
                },
                {
                    "code": "MedicationStatement",
                    "param": [
                        "source"
                    ]
                },
                {
                    "code": "MedicinalProductDefinition"
                },
                {
                    "code": "MessageDefinition"
                },
                {
                    "code": "MessageHeader"
                },
                {
                    "code": "MolecularSequence"
                },
                {
                    "code": "NamingSystem"
                },
                {
                    "code": "NutritionIntake",
                    "param": [
                        "source"
                    ]
                },
                {
                    "code": "NutritionOrder"
                },
                {
                    "code": "NutritionProduct"
                },
                {
                    "code": "Observation",
                    "param": [
                        "performer"
                    ]
                },
                {
                    "code": "ObservationDefinition"
                },
                {
                    "code": "OperationDefinition"
                },
                {
                    "code": "OperationOutcome"
                },
                {
                    "code": "Organization"
                },
                {
                    "code": "OrganizationAffiliation"
                },
                {
                    "code": "PackagedProductDefinition"
                },
                {
                    "code": "Patient",
                    "param": [
                        "link"
                    ]
                },
                {
                    "code": "PaymentNotice"
                },
                {
                    "code": "PaymentReconciliation"
                },
                {
                    "code": "Permission"
                },
                {
                    "code": "Person",
                    "param": [
                        "link"
                    ]
                },
                {
                    "code": "PlanDefinition"
                },
                {
                    "code": "Practitioner"
                },
                {
                    "code": "PractitionerRole"
                },
                {
                    "code": "Procedure",
                    "param": [
                        "performer"
                    ]
                },
                {
                    "code": "Provenance",
                    "param": [
                        "agent"
                    ]
                },
                {
                    "code": "Questionnaire"
                },
                {
                    "code": "QuestionnaireResponse",
                    "param": [
                        "author",
                        "source"
                    ]
                },
                {
                    "code": "RegulatedAuthorization"
                },
                {
                    "code": "RelatedPerson",
                    "param": [
                        "{def}"
                    ]
                },
                {
                    "code": "RequestOrchestration",
                    "param": [
                        "participant"
                    ]
                },
                {
                    "code": "Requirements"
                },
                {
                    "code": "ResearchStudy"
                },
                {
                    "code": "ResearchSubject"
                },
                {
                    "code": "RiskAssessment"
                },
                {
                    "code": "Schedule",
                    "param": [
                        "actor"
                    ]
                },
                {
                    "code": "SearchParameter"
                },
                {
                    "code": "ServiceRequest",
                    "param": [
                        "performer"
                    ]
                },
                {
                    "code": "Slot"
                },
                {
                    "code": "Specimen"
                },
                {
                    "code": "SpecimenDefinition"
                },
                {
                    "code": "StructureDefinition"
                },
                {
                    "code": "StructureMap"
                },
                {
                    "code": "Subscription"
                },
                {
                    "code": "SubscriptionStatus"
                },
                {
                    "code": "SubscriptionTopic"
                },
                {
                    "code": "Substance"
                },
                {
                    "code": "SubstanceDefinition"
                },
                {
                    "code": "SubstanceNucleicAcid"
                },
                {
                    "code": "SubstancePolymer"
                },
                {
                    "code": "SubstanceProtein"
                },
                {
                    "code": "SubstanceReferenceInformation"
                },
                {
                    "code": "SubstanceSourceMaterial"
                },
                {
                    "code": "SupplyDelivery"
                },
                {
                    "code": "SupplyRequest",
                    "param": [
                        "requester"
                    ]
                },
                {
                    "code": "Task"
                },
                {
                    "code": "TerminologyCapabilities"
                },
                {
                    "code": "TestPlan"
                },
                {
                    "code": "TestReport"
                },
                {
                    "code": "TestScript"
                },
                {
                    "code": "Transport"
                },
                {
                    "code": "ValueSet"
                },
                {
                    "code": "VerificationResult"
                },
                {
                    "code": "VisionPrescription"
                }
            ]
        }
        """;
}

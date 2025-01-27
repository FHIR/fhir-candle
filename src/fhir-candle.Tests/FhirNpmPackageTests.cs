// <copyright file="FhirNpmPackageTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using fhir.candle.Tests.Extensions;
using FhirCandle.Models;
using Shouldly;

namespace fhir.candle.Tests;

public class FhirNpmPackageTests
{
    internal string _hl7_fhir_us_core_4_0_0 = """
        {
          "name": "hl7.fhir.us.core",
          "version": "4.0.0",
          "tools-version": 3,
          "type": "fhir.ig",
          "date": "20210628190945",
          "license": "CC0-1.0",
          "canonical": "http://hl7.org/fhir/us/core",
          "url": "http://hl7.org/fhir/us/core/STU4.0.0",
          "title": "US Core Implementation Guide",
          "description": "The US Core Implementation Guide is based on FHIR Version R4 and defines the minimum conformance requirements for accessing patient data. The Argonaut pilot implementations, ONC 2015 Edition Common Clinical Data Set (CCDS), and ONC U.S. Core Data for Interoperability (USCDI) v1 provided the requirements for this guide. The prior Argonaut search and vocabulary requirements, based on FHIR DSTU2, are updated in this guide to support FHIR Version R4. This guide was used as the basis for further testing and guidance by the Argonaut Project Team to provide additional content and guidance specific to Data Query Access for purpose of ONC Certification testing. These profiles are the foundation for future US Realm FHIR implementation guides. In addition to Argonaut, they are used by DAF-Research, QI-Core, and CIMI. Under the guidance of HL7 and the HL7 US Realm Steering Committee, the content will expand in future versions to meet the needs specific to the US Realm.\nThese requirements were originally developed, balloted, and published in FHIR DSTU2 as part of the Office of the National Coordinator for Health Information Technology (ONC) sponsored Data Access Framework (DAF) project. For more information on how DAF became US Core see the US Core change notes. (built Mon, Jun 28, 2021 19:09+0000+00:00)",
          "fhirVersions": [
            "4.0.1"
          ],
          "dependencies": {
            "hl7.fhir.r4.core": "4.0.1",
            "hl7.fhir.uv.bulkdata": "1.0.1",
            "us.nlm.vsac": "0.3.0"
          },
          "author": "HL7 International - US Realm Steering Committee",
          "maintainers": [
            {
              "name": "HL7 International - US Realm Steering Committee",
              "url": "http://www.hl7.org/Special/committees/usrealm/index.cfm"
            }
          ],
          "directories": {
            "lib": "package",
            "example": "example"
          },
          "jurisdiction": "urn:iso:std:iso:3166#US"
        }
        """;

    [Fact]
    public void TestParseUsCore400()
    {
        FhirNpmPackageDetails d = FhirNpmPackageDetails.Parse(_hl7_fhir_us_core_4_0_0);

        d.ShouldNotBeNull();
        d.Name.ShouldBe("hl7.fhir.us.core");
        d.Version.ShouldBe("4.0.0");
        d.FhirVersionList.ShouldNotBeNull();
        d.FhirVersionList.ShouldHaveSingleItem();
        d.FhirVersionList.First().ShouldBe("4.0.1");
        d.FhirVersions.ShouldNotBeNull();
        d.FhirVersions.ShouldHaveSingleItem();
        d.FhirVersions.First().ShouldBe("4.0.1");
        d.Dependencies.ShouldNotBeNull();
        d.Dependencies.ShouldHaveCount(3);
        d.Dependencies.ContainsKey("hl7.fhir.r4.core").ShouldBeTrue();
        d.Dependencies["hl7.fhir.r4.core"].ShouldBe("4.0.1");
        d.Dependencies.ContainsKey("hl7.fhir.uv.bulkdata").ShouldBeTrue();
        d.Dependencies["hl7.fhir.uv.bulkdata"].ShouldBe("1.0.1");
        d.Dependencies.ContainsKey("us.nlm.vsac").ShouldBeTrue();
        d.Dependencies["us.nlm.vsac"].ShouldBe("0.3.0");
        d.Directories.ShouldNotBeNull();
        d.Directories.ShouldHaveCount(2);
        d.Directories.ContainsKey("lib").ShouldBeTrue();
        d.Directories["lib"].ShouldBe("package");
        d.Directories.ContainsKey("example").ShouldBeTrue();
        d.Directories["example"].ShouldBe("example");
    }
}

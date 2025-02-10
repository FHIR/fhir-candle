* Work in Progress
    * Updated NuGet dependencies:
        * `» fhir-candle`
            * `  MailKit                                       4.9.0  -> 4.10.0`
            * `  OpenTelemetry                                 1.11.0 -> 1.11.1`
            * `  OpenTelemetry.Exporter.Console                1.11.0 -> 1.11.1`
            * `  OpenTelemetry.Exporter.OpenTelemetryProtocol  1.11.0 -> 1.11.1`
            * `  OpenTelemetry.Extensions.Hosting              1.11.0 -> 1.11.1`
            * `  OpenTelemetry.Instrumentation.AspNetCore      1.10.1 -> 1.11.0`
            * `  System.IdentityModel.Tokens.Jwt               8.3.1  -> 8.4.0`
        * `» fhir-candle.Tests`
            * `  Hl7.Fhir.R4                5.11.2  -> 5.11.3`
            * `  Hl7.Fhir.R4B               5.11.2  -> 5.11.3`
            * `  Hl7.Fhir.R5                5.11.2  -> 5.11.3`
            * `  Microsoft.NET.Test.Sdk     17.12.0 -> 17.13.0`
            * `  xunit.runner.visualstudio  3.0.1   -> 3.0.2`
        * `» FhirCandle.Common`
            * `  Microsoft.IdentityModel.Tokens  8.3.1 -> 8.4.0`
        * `» FhirCandle.R4`
            * `  Hl7.Fhir.R4  5.11.2 -> 5.11.3`
        * `» FhirCandle.R4B`
            * `  Hl7.Fhir.R4B  5.11.2 -> 5.11.3`
        * `» FhirCandle.R5`
            * `  Hl7.Fhir.R5  5.11.2 -> 5.11.3`
      * Updates to subscription RI

* v2025.206
    * Added support for compartment-based searching.
        * Default listeners automatically support compartments defined in core.
        * Clients can store `CompartmentDefinition` resources, which update or add to existing compartments.
    * Fixed issue with `FhirRequestContext` incorrectly allowing some `HEAD` requests that are not cacheable.
    * First pass of SMART scope-based filtering for search.
        * Limited to testing of `match` results.
        * Does **not** filter inclusions yet.
        * Does **not** test granular scopes yet.

* v2025.129
    * Added commit log
    * Added CSS fonts for monospace content.
    * Removed `FluentAssertions` (`6.12.0`) and replaced with `Shouldly` (`4.3.0`) due to license changes.
    * Changed FHIR JSON serialization and parsing to System.Text.Json versions.
    * Updated NuGet dependencies
        * Updated `BlazorMonaco` from `3.2.0` to `3.3.0`
        * Updated `Firely.Fhir.Packages` from `4.7.0` to `4.9.0`
        * Updated `Hl7.Fhir.R4` from `5.9.1` to `5.11.2`
        * Updated `Hl7.Fhir.R4B` from `5.9.1` to `5.11.2`
        * Updated `Hl7.Fhir.R5` from `5.9.1` to `5.11.2`
        * Updated `MailKit` from `4.7.1.1` to `4.9.0`
        * Updated `Microsoft.AspNetCore.Components.Web` from `8.0.8` to `8.0.12`
        * Updated `Microsoft.Extensions.Hosting.Abstractions` from `8.0.0` to `9.0.1`
        * Updated `Microsoft.FluentUI.AspNetCore.Components` from `4.9.3` to `4.11.3`
        * Updated `Microsoft.FluentUI.AspNetCore.Components.Emoji` from `4.6.0` to `4.11.3`
        * Updated `Microsoft.FluentUI.AspNetCore.Components.Icons` from `4.9.3` to `4.11.3`
        * Updated `Microsoft.IdentityModel.Tokens` from `8.0.2` to `8.3.1`
        * Updated `Microsoft.NET.Test.Sdk` from `17.10.0` to `17.12.0`
        * Updated `OpenTelemetry` from `1.9.0` to `1.11.0`
        * Updated `OpenTelemetry.Exporter.Console` from `1.9.0` to `1.11.0`
        * Updated `OpenTelemetry.Exporter.OpenTelemetryProtocol` from `1.9.0` to `1.11.0`
        * Updated `OpenTelemetry.Extensions.Hosting` from `1.9.0` to `1.11.0`
        * Updated `OpenTelemetry.Instrumentation.AspNetCore` from `1.9.0` to `1.11.0`
        * Updated `System.IdentityModel.Tokens.Jwt` from `8.0.2` to `8.3.1`
        * Updated `xunit` from `2.9.0` to `2.9.3`
        * Updated `xunit.runner.visualstudio` from `2.8.2` to `3.0.1`
    * Added initial support for `transaction` Bundle processing (note: rollback is NOT implemented).
    * Added support for processing `batch` and `transaction` bundles in pre-loaded content (instead of storing them).
    * Fixed issue causing the same folder content to be loaded twice in certain combinations of command line arguments.
    * Added GC collection after the initial content load to reduce memory usage.
    * Added support for `_sort` in search.
    * Merged [PR 20](https://github.com/FHIR/fhir-candle/commit/d8d7645e1b11ac918361238537f10135cc9ce5ab)
        * Initial fix for [Issue #18](https://github.com/FHIR/fhir-candle/issues/18) via configuration option.
        * Initial support for compartment searches.
    * Additional fix for [Issue #18](https://github.com/FHIR/fhir-candle/issues/18)
        * Modified FHIR Controller to not return a body when returning a `304` status.
    * Added initial support for reverse chaining.
    * Fixed issue with POSTed searches not reading parameters correctly.

* v2024.910 - [PR 14](https://github.com/FHIR/fhir-candle/commit/70a8b38a40649160b3711e9a5a7ad4307e8e9d9a)
    * [f220fcc](https://github.com/FHIR/fhir-candle/commit/f220fccc24647311d43fb7807d910cc1613f7f27) Fix: Inverted string search test in some combinations.
* v2024.909 - [PR 13](https://github.com/FHIR/fhir-candle/commit/31fbbecd122f38003d44d2ff2f284ed864a3ed96)
    * [77beafc](https://github.com/FHIR/fhir-candle/commit/77beafc36fbb1b5b80f76a27312032efe26aa729) Fix: Only allow source content loads from a single directory if there are no subdirectories for tenant/endpoint names.

* Previous content
    * See [commit log](https://github.com/FHIR/fhir-candle/commits/main/)

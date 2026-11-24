# fhir-candle
[![Tests](https://github.com/FHIR/fhir-candle/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/FHIR/fhir-candle/actions/workflows/build-and-test.yml)
[![NuGet version](https://img.shields.io/nuget/v/fhir-candle.svg)](https://www.nuget.org/packages/fhir-candle)
[![Release workflow](https://github.com/FHIR/fhir-candle/actions/workflows/nuget-tool.yml/badge.svg)](https://github.com/FHIR/fhir-candle/actions/workflows/nuget-tool.yml)
[![Deploy to `argo.run`](https://github.com/FHIR/fhir-candle/actions/workflows/argo-ris.yml/badge.svg)](https://github.com/FHIR/fhir-candle/actions/workflows/argo-ris.yml)

When you need a small FHIR.

fhir-candle is a small in-memory FHIR server that can be used for testing and development. It is NOT intended to be used for production workloads.

The project is intended to serve as a platform for rapid development and testing for FHIR - both for features in the core specification as well as Implementation Guide development.

While there are many existing OSS FHIR servers, somewhere between most and all of them are intended to support production workloads.  In my own work on Reference Implementations, I often found it challenging to add the types of features I wanted due to the conflicts that causes.  To that end, here are some principles I generally use while developing this project:
* No database / persisted state
* Fast startup
* Dynamically apply changes (e.g., search parameters)
* House features that would not be appropriate in production
    * E.g., provide feedback on SMART tokens to help developers

## FHIR Foundation Project Statement
* Maintainers: Gino Canessa
* Issues / Discussion: Any issues should be submitted on [GitHub](https://github.com/FHIR/fhir-candle/issues). Discussion can be performed here on GitHub, or on the [dotnet stream on chat.fhir.org](https://chat.fhir.org/#narrow/stream/179171-dotnet).
* License: This software is offered under the [MIT License](LICENSE).
* Contribution Policy: See [Contributing](#contributing).
* Security Information: See [Security](#security).

## Contributing

There are many ways to contribute:
* [Submit bugs](https://github.com/FHIR/fhir-candle/issues) and help us verify fixes as they are checked in.
* Review the [source code changes](https://github.com/FHIR/fhir-candle/pulls).
* Engage with users and developers on the [dotnet stream on FHIR Zulip](https://chat.fhir.org/#narrow/stream/179171-dotnet)
* Contribute features or bug fixes - see [Contributing](CONTRIBUTING.MD) for details.

To ensure a welcoming environment, we follow the [HL7 Code of Conduct](https://www.hl7.org/legal/code-of-conduct.cfm) and expect contributors to do the same.


### Security

To report a security issue, please use the GitHub Security Advisory ["Report a Vulnerability"](https://github.com/FHIR/fhir-candle/security/advisories/new) tab.

For more information, please see the [Security Readme](SECURITY.MD).


# Documentation

See the [changelog](changelog.md) for release notes. Full documentation lives in the [`docs/`](docs/) tree:

- **Users** — running, configuring, and calling the server: [docs/user/](docs/user/)
  - [Getting started](docs/user/getting-started.md)
  - [FHIR tenants](docs/user/tenants.md)
  - [Loading initial data](docs/user/loading-data.md)
  - [Strict mode (`--strict`)](docs/user/strict-mode.md)
  - [Subscriptions reference implementation](docs/user/subscriptions-ri.md)
  - [Using OpenTelemetry](docs/user/opentelemetry.md)
  - [`$validate` operation](docs/user/operations/validate.md)
- **Contributors** — architecture and internals: [docs/technical/](docs/technical/)
  - [Release process](docs/technical/releasing.md) — published tagged releases,
    package qualification, NuGet trusted publishing, and versioned containers.

The sections below are a quick-start; deeper detail lives in `docs/`.

## Get Started with .NET

[Install the .NET 10 or 11 SDK](https://get.dot.net) and run this command:

```
dotnet tool install --global fhir-candle
```

Note that this software is still under heavy development.

Start a FHIR server and open the browser by running:

```
fhir-candle -o
```

## Get Started with Docker

[Install Docker](https://docs.docker.com/engine/install/) and run these commands:

```
docker pull ghcr.io/fhir/fhir-candle:latest
docker run -p 8080:5826 -e Public_Url=http://localhost:8080 ghcr.io/fhir/fhir-candle:latest
```

This maps container port 5826 to host port 8080 and advertises the host URL in FHIR links. Once running, access http://localhost:8080/ for the UI, or the default endpoints:
* http://localhost:8080/fhir/r4/ for FHIR R4
* http://localhost:8080/fhir/r4b/ for FHIR R4B
* http://localhost:8080/fhir/r5/ for FHIR R5

## Get Started by cloning this repository

```
dotnet run --project src/fhir-candle/fhir-candle.csproj --framework net11.0 --no-launch-profile
```

Building from source requires the .NET 11 SDK. Projects target .NET 11 and
.NET 10; .NET 8 and 9 are no longer supported. Container images use .NET 11.

For arguments, build, and release-output details on all platforms, see
[Getting started](docs/user/getting-started.md).

## More configuration and features

- [FHIR tenants](docs/user/tenants.md) — choose FHIR versions/endpoints with `--r4` / `--r4b` / `--r5`.
- [Loading initial data](docs/user/loading-data.md) — seed resources with `--fhir-source`.
- [Strict mode (`--strict`)](docs/user/strict-mode.md) — strict spec-conformant REST posture for conformance testing.
- [Subscriptions reference implementation](docs/user/subscriptions-ri.md) — run the FHIR Subscriptions RI stack.
- [Using OpenTelemetry](docs/user/opentelemetry.md) — export traces via `--otel-otlp-endpoint`.
- [`$validate` operation](docs/user/operations/validate.md) — structural resource validation.

# To-Do
Note: items are unsorted within their priorities

## High priority
* Feature/module definitions for selective loading
    Build interfaces for Hosted Services, etc.
    Add module tag to Operation, etc.
    Conditional loading based on discovery within types
* Persistent 'unsubscribe' list
* Finish search evaluators (remaining modifier combinations)
* Save/restore points
* Add loading packages/profiles to CapabilityStatement
* Configuration and content loading via GH repos
* SQL-on-FHIR ViewDefinition and runner support
* Additional MCP support

## Mid Priority
* Add a US Core Patient Data Feed page
* Complete SMART support
* Additional Transaction support
* OpenAPI generation
* Contained resources
* Subscription websocket support
* Runtime Operation definitions with static/template or JS scripted responses.
* Explore Native AOT for further size reduction
* Add 'core' operations (e.g., `$meta-add`).
* Extend operation support within `transaction` bundles

## The long tail
* Versioned Resource support
* Non-terminology validation
* Link to terminology server for full validation
* `_filter` support
* Runtime named queries
* GraphQL support

## More Information

FHIR&reg; is the registered trademark of HL7 and is used with the permission of HL7. 

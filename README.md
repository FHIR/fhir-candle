# fhir-candle
[![Tests](https://github.com/FHIR/fhir-candle/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/FHIR/fhir-candle/actions/workflows/build-and-test.yml)
[![Publish dotnet tool](https://img.shields.io/nuget/v/fhir-candle.svg)](https://github.com/FHIR/fhir-candle/actions/workflows/nuget-tool.yml)
[![Publish Docker image on GitHub CR](https://github.com/FHIR/fhir-candle/actions/workflows/ghcr-docker.yml/badge.svg)](https://github.com/FHIR/fhir-candle/actions/workflows/ghcr-docker.yml)
[![Deploy to subscriptions.argo.run](https://github.com/FHIR/fhir-candle/actions/workflows/argo-subscriptions.yml/badge.svg)](https://github.com/FHIR/fhir-candle/actions/workflows/argo-subscriptions.yml)

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
* Contribute features or bug fixes - see [Contributing](CONTRIBUTING.md) for details.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.


### Security

To report a security issue, please use the GitHub Security Advisory ["Report a Vulnerability"](https://github.com/FHIR/fhir-candle/security/advisories/new) tab.

For more information, please see the [Security Readme](SECURITY.MD).


# Documentation

## Get Started

[Install .NET 8 or newer](https://get.dot.net) and run this command:

```
dotnet tool install --global fhir-candle
```

Note that this software is still under heavy development.

Start a FHIR server and open the browser by running:
```
fhir-candle -o
```

### Cloning this repository

To run the default server from the command line:
```
dotnet run --project src/fhir-candle/fhir-candle.csproj
```

To pass arguments when using `dotnet run`, add an extra `--`.  For example, to see help:
```
dotnet run --project src/fhir-candle/fhir-candle.csproj -- --help
```

To build a release version of the project:
```
dotnet build src/fhir-candle/fhir-candle.csproj -c Release
```


The output of the release build can be run (from the root directory of the repo)
* on all platforms:
```
dotnet ./src/fhir-candle/bin/Release/net7.0/fhir-candle.dll
```
* if you built on Windows:
```
.\src\fhir-candle\bin\Release\net7.0\fhir-candle.exe
```
* if you built on Linux or MacOs:
```
./src/fhir-candle/bin/Release/net7.0/fhir-candle
```

### FHIR Tenants

By default, this software loads three FHIR 'tenants':
* a FHIR R4 endpoint at `/r4`,
* a FHIR R4B endpoint at `/r4b`, and
* a FHIR R5 endpoint at `/r5`.

The tenants can be controlled by command line arguments - note that manually specifying any tenants
overrides the default configuration and will *only* load the ones specified.  To load only an R4
endpoint at 'fhir', the arguments would include `--r4 fhir`.  You can specify multiple tenants for
the same version, for example `--r5 fhir --r5 also-fhir` will create two endpoints.

### Loading Initial Data

The server will load initial data specified by the `--fhir-source` argument.  If the path specified
is a relative path, the software will look for the directory starting at the current running path.

If the system is loading multiple tenants, it will check the path for additional directories based
on the tenant names.  For example, a path like `data` passed into the default server will look for
`data/r4`, `data/r4b`, and `data/r5`.  If tenant directories are not found, all tenants will try to
load resources from the specified path.

### Subscriptions Reference Implementation

This project also contains the reference stack for FHIR Subscriptions.  To use the default landing page
of the subscriptions RI, the following command can be used:
```
fhir-candle --reference-implementation subscriptions --load-package hl7.fhir.uv.subscriptions-backport#1.1.0 --load-examples false --protect-source true -m 1000
```


# To-Do
Note: items are unsorted within their priorities

## High priority
* Composite search parameter support
* Reverse chaining (`_has`)
* Feature/module definitions for selective loading
    Build interfaces for Hosted Services, etc.
    Add module tag to Operation, etc.
    Conditional loading based on discovery within types
* Persistent 'unsubscribe' list
* Finish search evaluators (remaining modifier combinations)
* Save/restore points
* Versioned Resource support
* Resource display / edit in UI
* Subscription RI scenario/walkthrough
* Resource editor design improvements
* Add loading packages/profiles to CapabilityStatement

## Mid Priority
* SMART support
* Transaction support
* Proxy header support
* Conditional interaction support (e.g., `conditional-update`, `if-match`)
* OpenAPI generation
* Compartments
* Contained resources
* Subscription websocket support

## The long tail
* Non-terminology validation
* Link to terminology server for full validation
* `_filter` support
* Runtime named queries
* GraphQL support

## More Information



FHIR&reg; is the registered trademark of HL7 and is used with the permission of HL7. 

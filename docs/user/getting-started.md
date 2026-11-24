# Getting started

fhir-candle is a small in-memory FHIR server for testing and development. It is
**not** intended for production workloads. There are three ways to install and
run it: as a global .NET tool, via Docker, or by cloning this repository.

## Get started with .NET

[Install the .NET 10 or 11 SDK](https://get.dot.net) and run this command:

```
dotnet tool install --global fhir-candle
```

> Note that this software is still under heavy development.

Start a FHIR server and open the browser by running:

```
fhir-candle -o
```

## Get started with Docker

[Install Docker](https://docs.docker.com/engine/install/) and run these
commands:

```
docker pull ghcr.io/fhir/fhir-candle:latest
docker run -p 8080:5826 -e Public_Url=http://localhost:8080 ghcr.io/fhir/fhir-candle:latest
```

This maps container port `5826` to host port `8080`. `Public_Url` makes
absolute links (including search continuations) point to the host port. You can access
`http://localhost:8080/` in the browser to use fhir-candle's UI, or access the
default endpoints:

- `http://localhost:8080/fhir/r4/` for FHIR R4
- `http://localhost:8080/fhir/r4b/` for FHIR R4B
- `http://localhost:8080/fhir/r5/` for FHIR R5

The image defaults to `-m 1000` (a [per-tenant resource limit](tenants.md#resource-limit)).
Arguments after the image name **replace** that default. To keep the limit
and run only an R4 endpoint named `test`:

```
docker run -p 8080:5826 -e Public_Url=http://localhost:8080 ghcr.io/fhir/fhir-candle:latest -m 1000 --r4 test
```

Both `latest` and `minimal` images use .NET 11 on Ubuntu 26.04 and support
Linux amd64 and arm64. The `minimal` tag uses the chiseled-extra runtime,
which retains ICU and time-zone data but has no shell or package manager.
Published releases also provide `<version>` and `<version>-minimal` image
tags for selecting a specific release. Merges to `main` do not update
`latest` or `minimal`; those aliases advance on published GitHub Releases.
Existing Kubernetes deployments using `ghcr.io/fhir/fhir-candle:latest`
receive the .NET 11 image on their next rollout; the port and arguments are
unchanged.

## Get started by cloning this repository

Install the .NET 11 SDK. All projects target .NET 11 and .NET 10; .NET 8 and 9
are no longer supported.

To run the default three tenants without the development launch profile
(which selects R5 and requests its core package):

```
dotnet run --project src/fhir-candle/fhir-candle.csproj --framework net11.0 --no-launch-profile
```

To pass arguments when using `dotnet run`, add an extra `--`. For example, to see
help:

```
dotnet run --project src/fhir-candle/fhir-candle.csproj --framework net11.0 -- --help
```

If fhir-candle rejects its command-line arguments, it exits with a nonzero
status. `--help` and `--version` exit with status zero without starting the
host. Startup failures reported by the host action are separate from
argument rejection. The executable forwards the host action's result in the
platform's exit-code representation; on Unix, the caller observes only the
low eight bits.

To build a release version of the project:

```
dotnet build src/fhir-candle/fhir-candle.csproj -c Release --framework net11.0
```

The output of the release build can be run from the root directory of the repo:

- on all platforms:

  ```
  dotnet ./src/fhir-candle/bin/Release/net11.0/fhir-candle.dll
  ```

- if you built on Windows:

  ```
  .\src\fhir-candle\bin\Release\net11.0\fhir-candle.exe
  ```

- if you built on Linux or macOS:

  ```
  ./src/fhir-candle/bin/Release/net11.0/fhir-candle
  ```

## Next steps

In **FHIR Contents**, **Admin — no sign-in** inspects the synthetic store
without applying SMART scopes. To compare the same resource ID with what a
client can read, select **View as SMART caller** and use its issued access
token for that tenant. See [Compare the store and caller views](tenants.md#compare-the-store-and-caller-views).

- [FHIR tenants](tenants.md) — choose which FHIR versions and endpoints to load.
- [Loading initial data](loading-data.md) — seed the server with resources.
- [Strict mode (`--strict`)](strict-mode.md) — run a spec-conformant REST
  posture for conformance testing.
- [`$validate` operation](operations/validate.md) — structural resource
  validation.

# Getting started

fhir-candle is a small in-memory FHIR server for testing and development. It is
**not** intended for production workloads. There are three ways to install and
run it: as a global .NET tool, via Docker, or by cloning this repository.

## Get started with .NET

[Install .NET 8 or newer](https://get.dot.net) and run this command:

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
docker run -p 8080:5826 ghcr.io/fhir/fhir-candle:latest
```

This runs the Docker image with the default configuration, mapping port `5826`
from the container to port `8080` on the host. Once running, you can access
`http://localhost:8080/` in the browser to use fhir-candle's UI, or access the
default endpoints:

- `http://localhost:8080/fhir/r4/` for FHIR R4
- `http://localhost:8080/fhir/r4b/` for FHIR R4B
- `http://localhost:8080/fhir/r5/` for FHIR R5

Additional arguments can be passed directly via the `docker run` command. For
example, to run the server with only an R4 endpoint named `test`:

```
docker run -p 8080:5826 ghcr.io/fhir/fhir-candle:latest --r4 test
```

## Get started by cloning this repository

To run the default server from the command line:

```
dotnet run --project src/fhir-candle/fhir-candle.csproj
```

To pass arguments when using `dotnet run`, add an extra `--`. For example, to see
help:

```
dotnet run --project src/fhir-candle/fhir-candle.csproj -- --help
```

To build a release version of the project:

```
dotnet build src/fhir-candle/fhir-candle.csproj -c Release
```

The output of the release build can be run from the root directory of the repo:

- on all platforms:

  ```
  dotnet ./src/fhir-candle/bin/Release/net8.0/fhir-candle.dll
  ```

- if you built on Windows:

  ```
  .\src\fhir-candle\bin\Release\net8.0\fhir-candle.exe
  ```

- if you built on Linux or macOS:

  ```
  ./src/fhir-candle/bin/Release/net8.0/fhir-candle
  ```

## Next steps

- [FHIR tenants](tenants.md) — choose which FHIR versions and endpoints to load.
- [Loading initial data](loading-data.md) — seed the server with resources.
- [Strict mode (`--strict`)](strict-mode.md) — run a spec-conformant REST
  posture for conformance testing.
- [`$validate` operation](operations/validate.md) — structural resource
  validation.

ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:11.0
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0 AS build-env
ARG TARGETPLATFORM
ARG BUILDPLATFORM
ARG TARGETARCH

WORKDIR /app

# Copy everything else and build
COPY . ./

RUN dotnet restore --ucr -a $TARGETARCH src/fhir-candle/fhir-candle.csproj
RUN dotnet publish --framework net11.0 -a $TARGETARCH src/fhir-candle/fhir-candle.csproj -c Release -o out --no-restore

# Build runtime image
FROM ${RUNTIME_IMAGE}
EXPOSE 5826
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "fhir-candle.dll"]
CMD ["-m", "1000"]

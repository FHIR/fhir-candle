FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
ARG TARGETPLATFORM
ARG BUILDPLATFORM
ARG TARGETARCH

WORKDIR /app

# Copy everything else and build
COPY . ./

RUN dotnet restore --ucr -a $TARGETARCH src/fhir-candle/fhir-candle.csproj
RUN dotnet publish --framework net9.0 -a $TARGETARCH src/fhir-candle/fhir-candle.csproj -c Release -o out --no-restore

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "fhir-candle.dll"]
CMD ["-m", "1000"]

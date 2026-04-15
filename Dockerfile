FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
ARG TARGETPLATFORM
ARG BUILDPLATFORM
ARG TARGETARCH

WORKDIR /app

# Copy everything else and build
COPY . ./

RUN dotnet restore --ucr -a $TARGETARCH src/fhir-candle/fhir-candle.csproj
RUN dotnet publish --framework net10.0 -a $TARGETARCH src/fhir-candle/fhir-candle.csproj -c Release -o out --no-restore

# Build runtime image
#FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
FROM mcr.microsoft.com/dotnet/aspnet:10.0
EXPOSE 5826
WORKDIR /app
RUN mkdir -p ~/.fhir/packages
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "fhir-candle.dll"]
CMD ["-m", "1000"]

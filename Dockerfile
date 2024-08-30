FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
ARG TARGETPLATFORM
ARG BUILDPLATFORM
#RUN echo "I am running on $BUILDPLATFORM, building for $TARGETPLATFORM" > /log
WORKDIR /app

# Copy everything else and build
COPY . ./
#
## Build with platform-specific .Net RID
#RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
        #dotnet restore src/fhir-candle/fhir-candle.csproj --arch x64; \
        #dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0 --arch x64; \
    #elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
        #dotnet restore src/fhir-candle/fhir-candle.csproj --arch arm64; \
        #dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0 --arch arm64; \
    #elif [ "$TARGETPLATFORM" = "windows/x64" ]; then \
        #dotnet restore src/fhir-candle/fhir-candle.csproj --arch x64; \
        #dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0 --arch x64; \
    #elif [ "$TARGETPLATFORM" = "windows/arm64" ]; then \
        #dotnet restore src/fhir-candle/fhir-candle.csproj --arch arm64; \
        #dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0 --arch arm64; \
    #elif [ "$TARGETPLATFORM" = "darwin/x64" ]; then \
        #dotnet restore src/fhir-candle/fhir-candle.csproj --arch x64; \
        #dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0 --arch x64; \
    #elif [ "$TARGETPLATFORM" = "darwin/arm64" ]; then \
        #dotnet restore src/fhir-candle/fhir-candle.csproj --arch arm64; \
        #dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0 --arch arm64; \
    #else \
        ##echo "Unsupported architecture: $TARGETPLATFORM"; \
        ##exit 1; \
        #dotnet restore src/fhir-candle/fhir-candle.csproj; \
        #dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0; \
    #fi;

#RUN dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0;
RUN dotnet restore src/fhir-candle/fhir-candle.csproj
RUN dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "fhir-candle.dll"]
CMD ["-m", "1000"]

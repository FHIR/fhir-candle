FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy everything else and build
COPY . ./

# Build with platform-specific .Net RID
RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
        dotnet restore src/fhir-candle/fhir-candle.csproj --arch linux-x64; \
        dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0 --arch linux-x64; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
        dotnet restore src/fhir-candle/fhir-candle.csproj --arch linux-arm64; \
        dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0 --arch linux-arm64; \
    elif [ "$TARGETPLATFORM" = "windows/x64" ]; then \
        dotnet restore src/fhir-candle/fhir-candle.csproj --arch win-x64; \
        dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0 --arch win-x64; \
    elif [ "$TARGETPLATFORM" = "windows/arm64" ]; then \
        dotnet restore src/fhir-candle/fhir-candle.csproj --arch win-arm64; \
        dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0 --arch win-arm64; \
    elif [ "$TARGETPLATFORM" = "darwin/x64" ]; then \
        dotnet restore src/fhir-candle/fhir-candle.csproj --arch osx-x64; \
        dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0 --arch osx-x64; \
    elif [ "$TARGETPLATFORM" = "darwin/arm64" ]; then \
        dotnet restore src/fhir-candle/fhir-candle.csproj --arch osx-arm64; \
        dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0 --arch osx-arm64; \
    else \
        dotnet restore src/fhir-candle/fhir-candle.csproj --framework net8.0; \
        dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0; \
    fi;

#RUN dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --framework net8.0;
#RUN dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "fhir-candle.dll"]
CMD ["-m", "1000"]

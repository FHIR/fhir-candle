FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy everything else and build
COPY . ./
RUN dotnet restore

# Build with platform-specific .Net RID
RUN if [ "$TARGETPLATFORM" = "linux/amd64" ]; then \
        dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --arch linux-x64; \
    elif [ "$TARGETPLATFORM" = "linux/arm64" ]; then \
        dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --arch linux-arm64; \
    elif [ "$TARGETPLATFORM" = "windows/x64" ]; then \
        dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --arch win-x64; \
    elif [ "$TARGETPLATFORM" = "windows/arm64" ]; then \
        dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --arch win-arm64; \
    elif [ "$TARGETPLATFORM" = "darwin/x64" ]; then \
        dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --arch osx-x64; \
    elif [ "$TARGETPLATFORM" = "darwin/arm64" ]; then \
        dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out --arch osx-arm64; \
    else \
        dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out; \
    fi;
#RUN dotnet publish src/fhir-candle/fhir-candle.csproj -c Release -o out

# Build runtime image
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "fhir-candle.dll"]
CMD ["-m", "1000"]

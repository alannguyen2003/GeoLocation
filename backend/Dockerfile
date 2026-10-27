# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so this layer is cached until a project file changes.
COPY src/LocationTracker.Api/LocationTracker.Api.csproj src/LocationTracker.Api/
COPY src/LocationTracker.Business/LocationTracker.Business.csproj src/LocationTracker.Business/
COPY src/LocationTracker.Data/LocationTracker.Data.csproj src/LocationTracker.Data/
RUN dotnet restore src/LocationTracker.Api/LocationTracker.Api.csproj

COPY src/ src/
RUN dotnet publish src/LocationTracker.Api/LocationTracker.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The app reads PORT at startup (Render sets it to 10000), so no port is baked in here.
# Clear the base image's default so it does not compete with the app's own UseUrls.
ENV ASPNETCORE_HTTP_PORTS=

USER app
ENTRYPOINT ["dotnet", "LocationTracker.Api.dll"]

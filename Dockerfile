# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish KeyClockWebAPI.csproj -c Release -o out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# Create a non-root user (works with Ubuntu/Debian base images)
RUN useradd -r -u 1001 -m -d /home/appuser -s /bin/bash -g root appuser && \
    chown -R appuser:root /app && \
    chmod -R g+rw /app
USER appuser

EXPOSE 8080
ENTRYPOINT ["dotnet", "KeyClockWebAPI.dll"]
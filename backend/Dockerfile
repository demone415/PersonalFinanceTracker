# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution-level files + project manifests first so `restore` is layer-cached
# and only re-runs when a .csproj or central package version changes.
COPY Directory.Build.props Directory.Packages.props FinanceTracker.slnx ./
COPY src/FinanceTracker.Domain/FinanceTracker.Domain.csproj          src/FinanceTracker.Domain/
COPY src/FinanceTracker.Application/FinanceTracker.Application.csproj src/FinanceTracker.Application/
COPY src/FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj src/FinanceTracker.Infrastructure/
COPY src/FinanceTracker.Api/FinanceTracker.Api.csproj                src/FinanceTracker.Api/
RUN dotnet restore src/FinanceTracker.Api/FinanceTracker.Api.csproj

# Copy the rest of the sources and publish a self-contained framework-dependent build.
COPY . .
RUN dotnet publish src/FinanceTracker.Api/FinanceTracker.Api.csproj \
    -c Release -o /app --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app ./

# Kestrel listens on 8080 inside the container (mapped to 5000 on the host).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FinanceTracker.Api.dll"]

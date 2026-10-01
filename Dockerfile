# syntax=docker/dockerfile:1.6

# ----------------------------------------------------------------------------
# Stage 1: restore + publish
# Builds a self-contained output ready to be copied into the runtime image.
# ----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copy NuGet manifest files first so Docker can cache the restore layer
# independently from source changes.
COPY CandidateAssessment.sln ./
COPY src/CandidateAssessment.Domain/CandidateAssessment.Domain.csproj             src/CandidateAssessment.Domain/
COPY src/CandidateAssessment.Application/CandidateAssessment.Application.csproj   src/CandidateAssessment.Application/
COPY src/CandidateAssessment.Infrastructure/CandidateAssessment.Infrastructure.csproj src/CandidateAssessment.Infrastructure/
COPY src/CandidateAssessment.Api/CandidateAssessment.Api.csproj                 src/CandidateAssessment.Api/
COPY tests/CandidateAssessment.UnitTests/CandidateAssessment.UnitTests.csproj   tests/CandidateAssessment.UnitTests/
COPY tests/CandidateAssessment.IntegrationTests/CandidateAssessment.IntegrationTests.csproj tests/CandidateAssessment.IntegrationTests/
COPY dotnet-tools.json ./

RUN dotnet restore CandidateAssessment.sln

# Copy the rest of the source and publish a release build.
COPY src/      src/
COPY tests/    tests/
RUN dotnet publish src/CandidateAssessment.Api/CandidateAssessment.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ----------------------------------------------------------------------------
# Stage 2: runtime
# ASP.NET Core 6 runtime + non-root user + writable volume for SQLite.
# ----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app

# SQLite needs a writable directory. /app/data is mounted as a volume in
# docker-compose so the database file persists across container restarts.
RUN mkdir -p /app/data /app/logs && \
    chown -R app:app /app

COPY --from=build --chown=app:app /app/publish .

# Run as the non-root user already present in the base image.
USER app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/candidateassessment.db" \
    Serilog__WriteToFile=true \
    Serilog__FilePath="/app/logs/app-.log"

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "CandidateAssessment.Api.dll"]

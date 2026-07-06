# syntax=docker/dockerfile:1
#
# Shared multi-stage Dockerfile for all FairSpot backend services.
#
# Build args:
#   SERVICE      - service folder name (e.g. Booking)
#   PROJECT      - project folder / assembly name (e.g. FPS.Booking)
#   PORT         - HTTP port the container listens on
#
# Example (build context must be code/server/):
#   docker build \
#     --build-arg SERVICE=Booking \
#     --build-arg PROJECT=FPS.Booking \
#     --build-arg PORT=5131 \
#     -t fairspot-booking:local \
#     code/server

ARG SERVICE=Booking
ARG PROJECT=FPS.Booking
ARG PORT=8080

# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SERVICE
ARG PROJECT

WORKDIR /src

# Copy solution manifests so restore can resolve all projects.
COPY FPS.sln FPS.slnx global.json ./

# Copy shared kernel — referenced by every service.
COPY Shared/ ./Shared/

# Copy the target service (Tests project is included so the solution restore
# succeeds; it is not included in the published output).
COPY ${SERVICE}/ ./${SERVICE}/

RUN dotnet restore ${SERVICE}/${PROJECT}/${PROJECT}.csproj

RUN dotnet publish ${SERVICE}/${PROJECT}/${PROJECT}.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    --no-self-contained

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG PROJECT
ARG PORT

WORKDIR /app

COPY --from=build /app/publish .

# Capture build-time ARG as runtime ENV so ENTRYPOINT can reference it.
ENV FPS_ASSEMBLY=${PROJECT}.dll

# Bind to all interfaces on the declared port. TLS is terminated at the Envoy
# gateway; containers never need HTTPS internally.
ENV ASPNETCORE_URLS=http://+:${PORT}
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE ${PORT}

# exec form requires a fixed string; use sh -c to expand the env var.
ENTRYPOINT ["sh", "-c", "exec dotnet $FPS_ASSEMBLY"]

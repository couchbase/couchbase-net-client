# Docker image for the Couchbase .NET FIT performer.
#
# Build from the repository ROOT with:
#   docker build -f fit/Couchbase.FitPerformer/Dockerfile --build-arg DOTNET_VERSION=10.0 -t couchbase-net-fit-performer .

ARG DOTNET_VERSION=10.0
# Short SDK name supplied by sdk-docker-build-action (build-arg SDK=dotnet).
# Declared here only so the build arg is consumed and doesn't warn, but unused otherwise.
ARG SDK

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build-env
# Re-declare ARG to make it available in this build stage.
ARG DOTNET_VERSION=10.0

WORKDIR /src
# Copy the whole repository (SDK + performer) so the performer's
# ProjectReferences to ..\..\src\... resolve.
COPY . .

WORKDIR /src/fit/Couchbase.FitPerformer
RUN dotnet restore Couchbase.FitPerformer.csproj

FROM build-env AS publish
ARG DOTNET_VERSION=10.0
RUN dotnet publish Couchbase.FitPerformer.csproj \
    -c Release \
    -f net${DOTNET_VERSION} \
    -o /app/out \
    --no-restore \
    --verbosity quiet

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app

ENV LOG_LEVEL="info"

COPY --from=publish /app/out .
ENTRYPOINT ["dotnet", "Couchbase.FitPerformer.dll", "disableConsoleRead=true", "includeClientLogs=true"]

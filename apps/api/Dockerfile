# The Support Desk API. Build from the repository root:
#   docker build -f apps/api/Dockerfile -t supportdesk-api .
# or let docker compose do it (the "api" service).

# ---- build: the full SDK, never shipped ---------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from nothing but the files that decide the package graph. These change
# rarely, so this layer - the slow one - is reused for every code-only change.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/
RUN dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj

# Then the source, and publish without touching the network again.
COPY .editorconfig ./
COPY apps/api/src/ apps/api/src/
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

# ---- runtime: just the ASP.NET Core runtime and the published app -------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Plain HTTP inside the container; TLS belongs to whatever sits in front of it.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Owned by root and only readable by the app: the process cannot rewrite its own binaries.
COPY --from=build /app/publish .

# The non-root user the .NET images provide (APP_UID is set by the base image).
USER $APP_UID

# Connection string, signing key and environment name arrive as environment variables at
# run time; nothing about any environment is baked in.
ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]

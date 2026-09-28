# One image, one deployment, N replicas. Every replica is identical and serves traffic: the
# service owns no background role and no single-writer resource — writes are row updates guarded
# by the database and the outbox.

ARG DOTNET_VERSION=10.0

# ── Build ─────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-noble AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
ARG GITHUB_USER
ARG GITHUB_TOKEN

WORKDIR /src

# GitHub Packages hosts Kariyer.Messaging.Contracts. Passed as build args (not BuildKit
# secrets) because podman-compose cannot translate an environment-sourced compose secret
# into a podman build --secret flag. The ARGs above are visible to the RUN below as
# environment variables, which is what resolves nuget.config's %GITHUB_USER%/%GITHUB_TOKEN%.
COPY nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/Kariyer.Recruiting.Domain/*.csproj ./src/Kariyer.Recruiting.Domain/
COPY src/Kariyer.Recruiting.Api/*.csproj ./src/Kariyer.Recruiting.Api/

# Restore before copying sources so a code-only change reuses the package layer.
RUN dotnet restore src/Kariyer.Recruiting.Api/Kariyer.Recruiting.Api.csproj \
        -a "${TARGETARCH:-amd64}"

COPY src/ ./src/

# Framework-dependent, explicitly. Passing -a makes `dotnet publish` default to
# self-contained, which would ship an entire copy of the runtime INSIDE an image that
# already is the runtime — roughly doubling it for nothing.
RUN dotnet publish src/Kariyer.Recruiting.Api/Kariyer.Recruiting.Api.csproj \
    -c "${BUILD_CONFIGURATION}" \
    -a "${TARGETARCH:-amd64}" \
    --no-restore \
    --self-contained false \
    -o /app

# ── Runtime ───────────────────────────────────────────────────────────────────
# aspnet, and here it is load-bearing rather than incidental: unlike the SEO service, this
# one's whole purpose is its request path. /api/cms/pages/resolve is hit by every visitor to
# a CMS page and by the prerenderer for every bot.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble AS runtime

# Non-root. This process holds an authenticated admin CRUD surface over the content that
# ranks the site; the blast radius of any bug should stop well short of the container
# filesystem.
USER $APP_UID
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=1

# NOTE: DOTNET_SYSTEM_GLOBALIZATION_INVARIANT is deliberately NOT set, unlike in the SEO and
# freshness images. This service slugifies admin-supplied Turkish titles into permanent public
# URLs; see Directory.Build.props for why that makes ICU worth its size here.

EXPOSE 8080

COPY --from=build /app .

# Liveness only. Readiness needs Postgres and RabbitMQ and belongs to the orchestrator, which
# can take a pod out of rotation without killing it — a container runtime that fails a health
# check RESTARTS the container, so wiring readiness here would turn a brief database blip into
# a restart storm across every replica at once.
#
# Re-enters the same binary rather than shelling out to curl, which these images do not carry.
HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
    CMD ["dotnet", "/app/Kariyer.Recruiting.Api.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "/app/Kariyer.Recruiting.Api.dll"]

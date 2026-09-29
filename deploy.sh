#!/usr/bin/env bash
set -euo pipefail

TARGET_DIR="/var/www/kariyer-recruiting-service"
DO_BUILD=false
SERVICES=()

for arg in "$@"; do
    if [[ "$arg" == "--build" ]]; then
        DO_BUILD=true
    else
        SERVICES+=("$arg")
    fi
done

log_info() { echo -e "\e[1;34m[INFO $(date +'%Y-%m-%d %H:%M:%S')]\e[0m $1"; }
log_success() { echo -e "\e[1;32m[SUCCESS $(date +'%Y-%m-%d %H:%M:%S')]\e[0m $1"; }
log_error() { echo -e "\e[1;31m[ERROR $(date +'%Y-%m-%d %H:%M:%S')]\e[0m $1" >&2; exit 1; }

if [[ ! -d "$TARGET_DIR" ]]; then
    log_error "Target directory '$TARGET_DIR' does not exist."
fi

cd "$TARGET_DIR" || log_error "Failed to enter directory $TARGET_DIR"

git pull --rebase || log_error "Git pull --rebase failed!"

# Load environment so build args ($GITHUB_USER, $GITHUB_TOKEN) are available to compose build
if [[ -f .env.development ]]; then
    log_info "Loading build environment from .env.development..."
    set -a
    # shellcheck disable=SC1091
    source .env.development
    set +a
fi

BUILD_ARGS=()
if [[ "$DO_BUILD" == true ]]; then
    BUILD_ARGS=("--build")
fi

ENV_FILE_ARG=()
if [[ -f .env.development ]]; then
    ENV_FILE_ARG=("--env-file" ".env.development")
fi

if [[ ${#SERVICES[@]} -eq 0 ]]; then
    log_info "Deploying ALL services..."
    docker compose "${ENV_FILE_ARG[@]}" -f docker-compose.dev.yml down || true
    docker compose "${ENV_FILE_ARG[@]}" -f docker-compose.dev.yml up -d "${BUILD_ARGS[@]}" --force-recreate || log_error "Docker compose up failed!"
else
    log_info "Deploying specific services: ${SERVICES[*]}..."
    docker compose "${ENV_FILE_ARG[@]}" -f docker-compose.dev.yml stop "${SERVICES[@]}" || true
    docker compose "${ENV_FILE_ARG[@]}" -f docker-compose.dev.yml up -d "${BUILD_ARGS[@]}" --force-recreate --no-deps "${SERVICES[@]}" || log_error "Docker compose up failed!"
fi

docker image prune -f || true

log_success "Recruiting service deployment completed successfully."

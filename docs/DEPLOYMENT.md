# Deployment

## Image

One image, N identical replicas. The service owns no background role and no single-writer
resource, so nothing needs partitioning across replicas.

```bash
docker build \
  --build-arg GITHUB_USER="$GITHUB_USER" \
  --build-arg GITHUB_TOKEN="$GITHUB_TOKEN" \
  -t kariyer-recruiting-service:latest .
```

The build args resolve `nuget.config`'s `%GITHUB_USER%` / `%GITHUB_TOKEN%` for
`Kariyer.Messaging.Contracts`. They are build args rather than BuildKit secrets because
podman-compose cannot translate an environment-sourced compose secret into `podman build
--secret`.

## Configuration

Everything comes from environment variables (`.env.example` is the template). The ones with no
safe default:

| Variable | Notes |
|---|---|
| `ConnectionStrings__Postgres` | the shared database; the service migrates only `recruiting` |
| `ConnectionStrings__RabbitMQ` | empty disables publishing (`Messaging__Enabled=false` is the explicit form) |
| `Auth__ExternalProviderUrl` | Supabase project URL; `/auth/v1` is appended |
| `Garnet__ConnectionString` | `host:6379`; `Garnet__Enabled=false` runs without a cache |
| `Recruiting__PublicSiteUrl` | the candidate-facing site, used in e-mails |
| `Recruiting__PublicApiUrl` | this service's own public base, e.g. `https://api.kariyerzamani.com/api/recruiting` — the candidate's accept/decline links resolve here, so a wrong value ships dead links in every invitation |
| `Recruiting__ConfirmationSigningKey` | signs those links; **startup fails without it** outside Development. Rotating it invalidates every unanswered invitation. |
| `Recruiting__AllowedOrigins` | the portal's origin; without it the browser blocks every request, gateway or not |
| `RateLimiting__WritesPerMinute` | default 60, per authenticated subject |
| `RateLimiting__ConfirmationsPerMinute` | default 10, per interview on the anonymous accept/decline routes |

## Migrations

`Persistence__MigrateOnStartup` is **false** in production. Apply the reviewed script instead:

```bash
psql "$RECRUITING_DB" -f docs/schema.sql
```

`docs/schema.sql` is idempotent. It is generated — the recipe is in its header, and CI fails if
the committed file has drifted from the migrations.

The service needs no DDL rights in production. It needs `SELECT` on `public.job_application`,
`public.company_job`, `public.employee`, `public.company` and `public.company_employee`, and
full rights on `recruiting`.

## Health

| Endpoint | Meaning |
|---|---|
| `/health/live` | the process is up; touches nothing |
| `/health/ready` | Postgres is reachable |
| `/metrics` | Prometheus scrape |

The container `HEALTHCHECK` re-enters the binary (`dotnet Kariyer.Recruiting.Api.dll
--healthcheck`); these images carry no curl. It probes liveness only — a readiness probe that
restarts the container would turn a database blip into a restart loop.

## Behind the identity gateway

This service is never exposed directly: kariyer-identity-security-service (YARP) is the only way
in, reaching it at `http://127.0.0.1:5340` over host networking. Four consequences worth knowing
before the first deploy:

- **Every request arrives from loopback.** Anything that partitions or logs by client IP is
  meaningless here, which is why the rate limiter partitions by the token's subject and, on the
  anonymous candidate routes, by the interview uid. Do not "fix" this by trusting
  `X-Forwarded-For` unless the gateway is configured to set it and is the only possible hop.
- **The token is validated here, not there.** The gateway forwards `Authorization` and this
  service validates the Supabase JWT itself, so the route can be `anonymous` at the gateway (the
  cms entries do the same) — that is also what lets the candidate's accept/decline routes work.
- **CORS still belongs to this service.** The preflight is proxied through, so
  `Recruiting__AllowedOrigins` must list the portal's origin even though the browser only ever
  talks to the gateway's host.
- **`/health/live`, `/health/ready` and `/metrics` sit outside the `/api/recruiting` prefix**, so
  they are not reachable through the gateway. That is deliberate — the container health check and
  the Prometheus scrape use localhost.

## Gateway changes (already made)

`kariyer-identity-security-service` carries three changes for this service; they ship with that
service, not this one:

1. **`recruitingRoute` / `recruitingCluster`** in `appsettings.json` and
   `appsettings.Development.json` — `/api/recruiting/{**catch-all}` → `http://127.0.0.1:5340`,
   `Order: 1`, `AuthorizationPolicy: "anonymous"`. Anonymous at the gateway is deliberate and
   matches `cmsRoute`: this service validates the Supabase JWT itself, and the candidate's
   accept/decline routes have no token at all. `nodeBackendRoute` cannot shadow it — it is
   `Order: 2` and additionally gated on an `X-Proxy-To-Node` header.
2. **The portal's origin in `StrictFrontendPolicy`** (`Program.cs`) — `http://localhost:5199` is
   in; **the production portal origin still has to be added there** before it ships. That CORS
   list is the gateway's, and it is what a browser actually hits: a missing origin comes back as
   a preflight with no `Access-Control-Allow-Origin`, every request fails, and the portal looks
   signed out rather than broken.
3. Nothing else. There is no `FallbackPolicy`, so a route without `AuthorizationPolicy` is not
   gated, and the only transform in the proxy is a Host rewrite for Supabase — `Authorization`
   reaches this service untouched.

One thing outside both repos: whatever terminates TLS in front of the gateway must send
`/api/recruiting/*` to it. `/api/cms/*` already arrives, so a rule that forwards `/api/*` covers
this; a path-by-path configuration needs a new entry.

## Rollout order

1. Apply `docs/schema.sql`. It now also creates the MassTransit outbox tables
   (`recruiting."OutboxMessage"`, `"OutboxState"`, `"InboxState"`) — publishing fails without them.
2. Deploy the service. `Messaging__Enabled=true` is safe once the mail side is ready:
   `kariyer-mail-service` consumes the three interview events from contracts 1.3.0, bound to
   `recruiting.interview.invited`, `.rescheduled` and `.cancelled`. It needs three templates
   authored and three slugs set — `EmailTemplates__InterviewInvitedTemplateSlug`,
   `__InterviewRescheduledTemplateSlug`, `__InterviewCancelledTemplateSlug`. A missing slug or
   template makes that consumer throw by design, so configure them before the first invitation.
   The invitation and reschedule templates must carry `AcceptUrl` and `DeclineUrl`, or the
   candidate has no way to answer.
3. Point `kariyer-basvuru-web` at the service.

The invitation e-mail's accept/decline links resolve to this service, so it must be reachable
from the public internet on `Recruiting__PublicApiUrl` before invitations go out. Those two
routes are the only anonymous ones; everything else needs a company token.

Nothing in the Node application changes, and nothing here writes to its tables. The legacy
`job_application.application_status` keeps its five values until that migration is taken on its
own terms.

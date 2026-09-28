# Architecture

Read `PLAN.md` first: it holds the boundary, the stage vocabulary and the decisions. This file
describes how the running service is put together.

## Shape

`Kariyer.Recruiting.Domain` holds the rules — the stage machine, interview validation, note
rules, Turkish folding, and the aggregates (`ApplicationPipeline`, `Interview`,
`ApplicationNote`, `ActivityEntry`, `SavedFilter`) with their ports. It references nothing:
no EF, no ASP.NET, no MassTransit. Everything in it is unit-testable without a container, which
is why 46 of the 61 tests need no database.

`Kariyer.Recruiting.Api` is the host. `Common/` carries the cross-cutting wiring
(configuration, persistence, caching, security, messaging, telemetry, web) and `Features/`
carries one folder per use case: an `IEndpoint` that maps the route and a handler that does the
work. Endpoints and handlers are discovered by assembly scan, so adding an operation means
adding a folder — no central registry, no merge conflict.

## Data

One Postgres, shared with the Node application and the other .NET services, and one schema of
its own. `recruiting` holds six tables; everything else is read through keyless projections
(`ToView`) that EF excludes from migrations:

| Projection | Node-owned table | Read for |
|---|---|---|
| `JobApplicationProjection` | `public.job_application` | the applications themselves |
| `CompanyJobProjection` | `public.company_job` | job ownership + title |
| `EmployeeProjection` | `public.employee` | candidate identity |
| `CompanyProjection` | `public.company` | token → company resolution |
| `CompanyEmployeeProjection` | `public.company_employee` | the interviewer directory |

`SchemaTests` asserts that a migration run creates nothing in `public`. If that test ever fails,
the boundary has been broken.

### Stage resolution

An application the pipeline has never touched has no row in `recruiting.application_pipeline`.
Rather than backfilling every application, the list projects
`coalesce(pipeline.stage, recruiting.kz_stage_from_legacy(job_application.application_status))`,
and the first write materialises the row. The mapping lives in SQL because the list filters,
sorts and pages on it; a C# mapping would force client evaluation of every application in the
company.

### Search

`recruiting.kz_fold(text)` lowercases and strips Turkish diacritics inside Postgres, so "Simsek"
matches "Şimşek". `TurkishCasing.Normalize` applies the identical fold to the search term in
process, and an integration test asserts the two agree — a drift between them would silently
stop matching.

Filtering, ordering and paging all run against the joined entities *before* the projection: the
stage is a correlated subquery, and neither the fold nor an ORDER BY translates over a projected
column.

## Caching

Garnet over RESP (`StackExchange.Redis`), the connection policy copied from the SEO service:
`AbortOnConnectFail = false` and bounded timeouts, because a cache that is down must degrade
latency and never correctness. Reads fall through to Postgres on a miss; every cache failure is
logged and swallowed.

| Key | TTL |
|---|---|
| `recruiting:job:{jobUid}:apps:{filterHash}` | 60 s |
| `recruiting:job:{jobUid}:stats` | 120 s |
| `recruiting:job:{jobUid}:interviews` | 60 s |
| `recruiting:company:{companyUid}:members` | 15 min |
| `recruiting:identity:{sub}` | 10 min |

Writes invalidate through `CacheInvalidator` only. A stage change or a note save drops
`recruiting:job:{jobUid}*`; an interview write drops the board and the list pages. Prefix deletes
walk `SCAN`, never `KEYS`.

The cross-job Adaylar list is deliberately **not** cached: it is paged through continuously and
its filters change per keystroke, so almost every entry would be written once and never read.

## Messaging

MassTransit over RabbitMQ with the EF outbox. An event is staged in the same transaction as the
rows that justify it, so a committed stage change cannot fail to be announced and a rolled-back
one cannot announce itself. Contracts come from `Kariyer.Messaging.Contracts` ≥ 1.3.0 —
consumed from GitHub Packages, never as a project reference to the local checkout.

Published: `ApplicationStageChangedEvent`, `InterviewInvitedEvent`, `InterviewRescheduledEvent`,
`InterviewCancelledEvent`.

An invitation publishes two events (the invitation and the stage move to `INTERVIEW`). The mail
service must react to the invitation and treat `INTERVIEW` on the stage event as silent, or one
invitation sends two e-mails.

## The candidate's answer

`AcceptUrl` / `DeclineUrl` on the invitation events point back at this service:
`GET /api/recruiting/interviews/{uid}/confirmation/{accept|decline}?token=…`. The page is rendered
by the service rather than a SPA so the link works in any mail client, with no session and no
JavaScript, and the candidate has no account here at all — the HMAC token in the link, which binds
the interview, the answer and an expiry, is the whole authorisation.

The GET only shows what is being answered. Mail clients and link scanners follow every URL in a
message, so nothing is written until the form on that page is posted. `Recruiting:ConfirmationSigningKey`
signs the tokens and is required outside Development; rotating it invalidates unanswered invitations.

## Security

Supabase JWT bearer: issuer and audience validated, `MapInboundClaims = false`, zero clock skew.
`CompanyContextResolver` turns the token's `sub` into the company through the company projection
and refuses anything that is not a completed, approved company. Every query is scoped by the
resolved company; no route accepts a company identifier from the caller. The resolution is
cached for ten minutes.

Mutating routes are rate limited (`RateLimitExtensions`): 60 writes per minute per authenticated
subject, and 10 answers per minute per interview on the candidate's anonymous accept/decline
routes. The partitions matter more than the numbers — this service runs behind the identity
gateway on loopback, so an IP partition would put the whole estate in one bucket, and the
anonymous routes have no subject to partition by at all. A rejected request gets the standard
error envelope with `TOO_MANY_REQUESTS` and a `Retry-After` header. CSRF needs no separate
defence: the only credential is a bearer token the caller has to attach deliberately, and no
cookie is read.

Roles are not modelled, and are not planned. The platform's auth has no role to map onto —
`company_employee` carries a free-text `position`, and a Supabase account is only ever an
employee, a company, a community or an admin — so the authorisation boundary is the company, and
that boundary is enforced on every route. See `PLAN.md` "Roles are not coming" for what that
decision closes.

## Failure behaviour

| Dependency down | Effect |
|---|---|
| Garnet | every read goes to Postgres; writes unaffected |
| RabbitMQ | writes still commit; events wait in the outbox |
| Postgres | `/health/ready` goes red, `/health/live` stays green |

`/health/live` never touches a dependency: a container runtime restarts a container that fails
its health check, and a brief database blip must not become a restart loop.
